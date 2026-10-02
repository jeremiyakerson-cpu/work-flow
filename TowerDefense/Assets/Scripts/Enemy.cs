using System;
using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Pooling;
using UnityEngine;

/// <summary>
/// Attach to every enemy prefab. Handles path-following movement, health/armor,
/// being "blocked" by heroes/barricades (the KR chokepoint mechanic), status
/// effects (slow/poison), and notifying the GameManager on death or leak.
/// Base stats normally come from an EnemyData asset via Init(EnemyData, ...).
/// Enemies are pooled: on death/leak they return to GameObjectPool, and the
/// next Init fully resets them. Hold on to an Enemy across frames? Compare
/// <see cref="SpawnId"/> as well as IsDead, because the same object comes back
/// as a different enemy.
/// </summary>
public class Enemy : MonoBehaviour
{
    [Header("Runtime Stats (set by WaveManager on spawn)")]
    public float maxHealth = 10f;
    public float moveSpeed = 2f;
    public int goldReward = 5;
    public int damageToBase = 1;
    public ArmorType armor = ArmorType.None;
    public EnemyMoveType moveType = EnemyMoveType.Ground;
    public float meleeDamage = 2f;
    public float attackInterval = 1f;

    /// <summary>Any enemy took damage (after resistances): enemy, amount, type. For damage numbers / hit flashes.</summary>
    public static event Action<Enemy, float, DamageType> AnyDamaged;
    /// <summary>Any enemy was killed (not leaked). For gold popups, death VFX, audio.</summary>
    public static event Action<Enemy> AnyDied;
    /// <summary>Any enemy reached the exit.</summary>
    public static event Action<Enemy> AnyLeaked;

    /// <summary>Per-enemy events. Cleared when the enemy despawns, so subscribers never leak onto a recycled enemy.</summary>
    public event Action<Enemy, float> Damaged;
    public event Action<Enemy> Died;
    public event Action<Enemy> Leaked;

    public EnemyData Data { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsBoss { get; set; }
    public float CurrentHealth => currentHealth;
    public bool IsBlocked => blocker != null;
    /// <summary>The hero/barricade currently holding this enemy, or null.</summary>
    public Transform Blocker => blocker;
    public bool IsFlying => moveType == EnemyMoveType.Flying;
    public IReadOnlyList<Vector3> Path => path;
    public int WaypointIndex => waypointIndex;
    /// <summary>World distance travelled along the path. Towers target the highest value for "First".</summary>
    public float PathProgress { get; private set; }
    /// <summary>Total length of the current path (world units).</summary>
    public float PathLength { get; private set; }
    /// <summary>World distance left to the exit (0 at the exit). Lower = more urgent.</summary>
    public float RemainingPathDistance => Mathf.Max(0f, PathLength - PathProgress);
    /// <summary>
    /// Unit direction the enemy is walking (or facing its blocker while fighting).
    /// Keeps the last direction while standing still, so sprites don't snap back.
    /// </summary>
    public Vector2 MoveDirection { get; private set; } = Vector2.right;
    /// <summary>Changes on every Init. A stored (enemy, SpawnId) pair goes stale when the enemy is recycled.</summary>
    public int SpawnId { get; private set; }

    /// <summary>Ability-driven speed scale (enrage, charge). Multiplies with slow effects.</summary>
    public float SpeedMultiplier { get; set; } = 1f;
    /// <summary>While true, TakeDamage is ignored (shield abilities).</summary>
    public bool Invulnerable { get; set; }

    public bool IsSlowed => slow.IsActive;
    public bool IsPoisoned => poison.IsActive;
    /// <summary>Current slow factor on movement (1 = not slowed). For slowed-tint visuals.</summary>
    public float SlowMultiplier => slow.CurrentMultiplier;

    private static int nextSpawnId;
    private static readonly List<EnemyAbilityBehaviour> abilityScratch = new List<EnemyAbilityBehaviour>();

    private float currentHealth;
    private IReadOnlyList<Vector3> path;
    private int waypointIndex = 0;
    private Collider2D[] colliders;

    // Prefab-authored identity stats, restored on every Init so a recycled enemy
    // initialised without EnemyData doesn't inherit its previous life's armor etc.
    private bool defaultsCaptured;
    private int defaultDamageToBase;
    private ArmorType defaultArmor;
    private EnemyMoveType defaultMoveType;
    private float defaultMeleeDamage;
    private float defaultAttackInterval;

    // --- Blocking (chokepoint) state ---
    private Transform blocker;
    private IBlockable blockerTarget;
    private float attackTimer;

    // --- Status effects (rules live in TowerDefense.Core) ---
    private SlowEffect slow = SlowEffect.None;
    private PoisonEffect poison;

    private void Awake()
    {
        colliders = GetComponentsInChildren<Collider2D>(true);
        CaptureDefaults();
    }

    private void CaptureDefaults()
    {
        if (defaultsCaptured) return;
        defaultsCaptured = true;
        defaultDamageToBase = damageToBase;
        defaultArmor = armor;
        defaultMoveType = moveType;
        defaultMeleeDamage = meleeDamage;
        defaultAttackInterval = attackInterval;
    }

    /// <summary>
    /// Preferred init path: pull identity stats (armor/moveType/meleeDamage)
    /// from the EnemyData asset, apply the WaveManager's scaled health/speed/
    /// reward on top. startWaypointIndex/startPosition let summons spawn mid-path.
    /// </summary>
    public void Init(EnemyData enemyData, IReadOnlyList<Vector3> waypointPath, float scaledHealth, float scaledSpeed, int scaledReward,
                     int startWaypointIndex = 0, Vector3? startPosition = null)
    {
        ResetRuntimeState();
        Data = enemyData;
        path = waypointPath;
        // Guard against NaN/zero from bad data: a 0-hp enemy could never die.
        maxHealth = scaledHealth > 0.01f ? scaledHealth : 0.01f;
        currentHealth = maxHealth;
        moveSpeed = scaledSpeed > 0f ? scaledSpeed : 0f;
        goldReward = Mathf.Max(0, scaledReward);

        if (enemyData != null)
        {
            damageToBase = enemyData.damageToBase;
            armor = enemyData.armor;
            moveType = enemyData.moveType;
            meleeDamage = enemyData.meleeDamage;
            attackInterval = enemyData.attackInterval;
            IsBoss = enemyData.isBoss;
        }

        waypointIndex = Mathf.Max(0, startWaypointIndex);
        if (startPosition.HasValue)
            transform.position = startPosition.Value;
        else if (path != null && path.Count > 0)
            transform.position = path[Mathf.Min(waypointIndex, path.Count - 1)];
        PathLength = DistanceAlongPathTo(int.MaxValue, transform.position);
        PathProgress = DistanceAlongPathTo(waypointIndex, transform.position);
        FaceNextWaypoint();

        if (path == null || path.Count == 0)
            Debug.LogWarning($"{name}: enemy initialised without a path; it will stand still.");

        if (enemyData != null && enemyData.abilities != null)
        {
            foreach (var ability in enemyData.abilities)
                if (ability != null && !IsDead) ability.Attach(this);
        }
    }

    /// <summary>Init from an EnemyData asset with scene-authored Transform waypoints.</summary>
    public void Init(EnemyData enemyData, List<Transform> waypointPath, float scaledHealth, float scaledSpeed, int scaledReward)
    {
        Init(enemyData, ToPositions(waypointPath), scaledHealth, scaledSpeed, scaledReward);
    }

    /// <summary>Legacy/manual init if you're not using EnemyData for a given prefab.</summary>
    public void Init(List<Transform> waypointPath, float health, float speed, int reward, int leakDamage,
                      ArmorType armorType = ArmorType.None, EnemyMoveType move = EnemyMoveType.Ground)
    {
        Init((EnemyData)null, ToPositions(waypointPath), health, speed, reward);
        damageToBase = leakDamage;
        armor = armorType;
        moveType = move;
    }

    private void ResetRuntimeState()
    {
        CaptureDefaults();
        damageToBase = defaultDamageToBase;
        armor = defaultArmor;
        moveType = defaultMoveType;
        meleeDamage = defaultMeleeDamage;
        attackInterval = defaultAttackInterval;
        SpawnId = ++nextSpawnId;
        IsDead = false;
        IsBoss = false;
        SpeedMultiplier = 1f;
        Invulnerable = false;
        blocker = null;
        blockerTarget = null;
        attackTimer = 0f;
        slow = SlowEffect.None;
        poison.Clear();
        PathProgress = 0f;
        PathLength = 0f;
        MoveDirection = Vector2.right;
        transform.rotation = Quaternion.identity;
        // A previous life (or a death animation) may have disabled colliders.
        if (colliders != null)
            foreach (var c in colliders)
                if (c != null) c.enabled = true;
    }

    private static List<Vector3> ToPositions(List<Transform> waypoints)
    {
        var positions = new List<Vector3>();
        if (waypoints == null) return positions;
        foreach (var t in waypoints)
            if (t != null) positions.Add(t.position);
        return positions;
    }

    private void Update()
    {
        if (IsDead) return;
        TickStatusEffects(Time.deltaTime);
        if (IsDead) return;

        if (blocker != null)
        {
            AttackBlocker();
            return;
        }

        if (path == null || waypointIndex >= path.Count) return;
        Move(moveSpeed * slow.CurrentMultiplier * Mathf.Max(0f, SpeedMultiplier) * Time.deltaTime);
    }

    // Walks `step` world units along the path, carrying leftover distance past
    // waypoints so fast enemies don't lose time (or cut corners) at each turn.
    private void Move(float step)
    {
        Vector3 pos = transform.position;
        while (step > 0f && waypointIndex < path.Count)
        {
            Vector3 target = path[waypointIndex];
            Vector3 delta = target - pos;
            float dist = delta.magnitude;
            if (dist > 1e-5f) MoveDirection = new Vector2(delta.x / dist, delta.y / dist);

            if (dist <= step)
            {
                pos = target;
                step -= dist;
                PathProgress += dist;
                waypointIndex++;
            }
            else
            {
                pos += delta * (step / dist);
                PathProgress += step;
                step = 0f;
            }
        }
        transform.position = pos;
        if (PathProgress > PathLength) PathProgress = PathLength;

        if (waypointIndex >= path.Count)
            ReachEnd();
    }

    private void FaceNextWaypoint()
    {
        if (path == null || waypointIndex >= path.Count) return;
        Vector3 delta = path[waypointIndex] - transform.position;
        if (delta.sqrMagnitude < 1e-8f && waypointIndex + 1 < path.Count) delta = path[waypointIndex + 1] - transform.position;
        Vector2 d = new Vector2(delta.x, delta.y);
        if (d.sqrMagnitude > 1e-8f) MoveDirection = d.normalized;
    }

    // Distance along the path to `position` when heading for waypoint `index`.
    // index >= path.Count gives the full path length.
    private float DistanceAlongPathTo(int index, Vector3 position)
    {
        if (path == null || path.Count == 0) return 0f;
        float d = 0f;
        int last = Mathf.Min(index, path.Count - 1);
        for (int i = 1; i <= last; i++) d += Vector3.Distance(path[i - 1], path[i]);
        if (index > 0 && index < path.Count) d -= Vector3.Distance(position, path[index]);
        return Mathf.Max(0f, d);
    }

    // ---------------- Damage ----------------

    public void TakeDamage(float amount, DamageType type = DamageType.Physical)
    {
        if (IsDead || Invulnerable || !(amount > 0f)) return;
        float finalDamage = DamageTable.Default.Apply(amount, type, armor);
        if (!(finalDamage > 0f)) return;
        currentHealth -= finalDamage;
        Damaged?.Invoke(this, finalDamage);
        AnyDamaged?.Invoke(this, finalDamage, type);
        if (currentHealth <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || !(amount > 0f)) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    /// <summary>Resistance multiplier this enemy takes from a damage type (for tooltips).</summary>
    public float ResistanceMultiplier(DamageType type) => DamageTable.Default.Multiplier(type, armor);

    /// <summary>Slow movement. Strongest slow wins; a weaker one is ignored while it lasts (see Core.SlowEffect).</summary>
    public void ApplySlow(float multiplier, float duration)
    {
        if (IsDead) return;
        slow.Apply(multiplier, duration);
    }

    /// <summary>Damage over time. Doesn't stack: strongest wins, equal refreshes (see Core.PoisonEffect).</summary>
    public void ApplyPoison(float damagePerSecond, float duration)
    {
        if (IsDead) return;
        poison.Apply(damagePerSecond, duration);
    }

    private void TickStatusEffects(float dt)
    {
        slow.Tick(dt);
        float poisonDamage = poison.Tick(dt);
        if (poisonDamage > 0f) TakeDamage(poisonDamage, DamageType.Poison);
    }

    // ---------------- Blocking (chokepoints) ----------------

    public bool TryGetBlocked(Transform blockerTransform)
    {
        if (IsDead || blockerTransform == null) return false;
        if (moveType == EnemyMoveType.Flying) return false;
        if (blocker != null) return false;

        blocker = blockerTransform;
        blockerTarget = blockerTransform.GetComponent<IBlockable>();
        attackTimer = 0f;
        return true;
    }

    public void ReleaseFromBlock()
    {
        blocker = null;
        blockerTarget = null;
    }

    private void AttackBlocker()
    {
        // Blocker destroyed or deactivated (hero died) - resume walking.
        if (blocker == null || !blocker.gameObject.activeInHierarchy)
        {
            ReleaseFromBlock();
            return;
        }

        Vector3 toBlocker = blocker.position - transform.position;
        if (toBlocker.x * toBlocker.x + toBlocker.y * toBlocker.y > 1e-6f)
            MoveDirection = new Vector2(toBlocker.x, toBlocker.y).normalized;

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            attackTimer = Mathf.Max(0.05f, attackInterval);
            // The blocker may release us (or die) inside ReceiveMeleeDamage; that's fine.
            if (blockerTarget != null && !(blockerTarget is UnityEngine.Object o && o == null))
                blockerTarget.ReceiveMeleeDamage(meleeDamage);
        }
    }

    // ---------------- Death / leak / despawn ----------------

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddGold(goldReward);
            GameManager.Instance.OnEnemyKilled(this);
        }
        Died?.Invoke(this);
        AnyDied?.Invoke(this);
        Despawn();
    }

    private void ReachEnd()
    {
        if (IsDead) return;
        IsDead = true;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.DamageBase(damageToBase);
            GameManager.Instance.OnEnemyLeaked(this);
        }
        Leaked?.Invoke(this);
        AnyLeaked?.Invoke(this);
        Despawn();
    }

    /// <summary>
    /// Remove this enemy without a kill reward or a leak (e.g. a splitter
    /// replacing itself with children). Raises no Died/Leaked events.
    /// </summary>
    public void DespawnSilently()
    {
        if (IsDead) return;
        IsDead = true;
        Despawn();
    }

    private void Despawn()
    {
        ReleaseFromBlock();

        abilityScratch.Clear();
        GetComponents(abilityScratch);
        foreach (var ability in abilityScratch)
        {
            if (ability == null) continue;
            // Disable first: Destroy is deferred to end of frame, and a disabled
            // behaviour can't run again if this object is respawned this frame.
            ability.enabled = false;
            Destroy(ability);
        }
        abilityScratch.Clear();

        Damaged = null;
        Died = null;
        Leaked = null;
        if (WaveManager.Instance != null) WaveManager.Instance.UnregisterEnemy(this);
        GameObjectPool.Despawn(gameObject);
    }

    // Safety net: an enemy disabled or destroyed by something else must not
    // keep its wave open forever.
    private void OnDisable()
    {
        if (WaveManager.Instance != null) WaveManager.Instance.UnregisterEnemy(this);
    }

    private void OnDestroy()
    {
        if (WaveManager.Instance != null) WaveManager.Instance.UnregisterEnemy(this);
    }

    public float HealthPercent() => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
}

/// <summary>
/// Anything an enemy can be blocked by and fight against (heroes, barricades).
/// </summary>
public interface IBlockable
{
    void ReceiveMeleeDamage(float amount);
}
