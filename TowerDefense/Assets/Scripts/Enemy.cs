using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to every enemy prefab. Handles path-following movement, health/armor,
/// being "blocked" by heroes/barricades (the KR chokepoint mechanic), status
/// effects (slow/poison), and notifying the GameManager on death or leak.
/// Base stats normally come from an EnemyData asset via Init(EnemyData, ...).
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

    public event Action<Enemy, float> Damaged;
    public event Action<Enemy> Died;
    public event Action<Enemy> Leaked;

    public EnemyData Data { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsBoss { get; set; }
    public float CurrentHealth => currentHealth;
    public bool IsBlocked => blocker != null;
    public bool IsFlying => moveType == EnemyMoveType.Flying;
    public IReadOnlyList<Vector3> Path => path;
    public int WaypointIndex => waypointIndex;
    /// <summary>World distance travelled along the path. Towers target the highest value for "First".</summary>
    public float PathProgress { get; private set; }

    /// <summary>Ability-driven speed scale (enrage, charge). Multiplies with slow effects.</summary>
    public float SpeedMultiplier { get; set; } = 1f;
    /// <summary>While true, TakeDamage is ignored (shield abilities).</summary>
    public bool Invulnerable { get; set; }

    private float currentHealth;
    private IReadOnlyList<Vector3> path;
    private int waypointIndex = 0;

    // --- Blocking (chokepoint) state ---
    private Transform blocker;
    private float attackTimer;

    // --- Status effects ---
    private float slowMultiplier = 1f;
    private float slowTimeRemaining;
    private float poisonDps;
    private float poisonTimeRemaining;

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
        maxHealth = scaledHealth;
        currentHealth = scaledHealth;
        moveSpeed = scaledSpeed;
        goldReward = scaledReward;

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
        PathProgress = DistanceAlongPathTo(waypointIndex, transform.position);

        if (enemyData != null && enemyData.abilities != null)
        {
            foreach (var ability in enemyData.abilities)
                if (ability != null) ability.Attach(this);
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
        IsDead = false;
        IsBoss = false;
        SpeedMultiplier = 1f;
        Invulnerable = false;
        blocker = null;
        attackTimer = 0f;
        slowMultiplier = 1f;
        slowTimeRemaining = 0f;
        poisonDps = 0f;
        poisonTimeRemaining = 0f;
        PathProgress = 0f;
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
        TickStatusEffects();
        if (IsDead) return;

        if (blocker != null)
        {
            AttackBlocker();
            return;
        }

        if (path == null || waypointIndex >= path.Count) return;

        Vector3 target = path[waypointIndex];
        float speed = moveSpeed * slowMultiplier * SpeedMultiplier;
        Vector3 before = transform.position;
        transform.position = Vector3.MoveTowards(before, target, speed * Time.deltaTime);
        PathProgress += Vector3.Distance(before, transform.position);

        if (Vector3.Distance(transform.position, target) < 0.05f)
        {
            waypointIndex++;
            if (waypointIndex >= path.Count)
                ReachEnd();
        }
    }

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
        if (IsDead || Invulnerable || amount <= 0f) return;
        float finalDamage = amount * ResistanceMultiplier(type);
        currentHealth -= finalDamage;
        Damaged?.Invoke(this, finalDamage);
        AnyDamaged?.Invoke(this, finalDamage, type);
        if (currentHealth <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    private float ResistanceMultiplier(DamageType type)
    {
        if (type == DamageType.Magic || type == DamageType.Poison)
            return armor == ArmorType.Heavy ? 1.25f : 1f;

        switch (armor)
        {
            case ArmorType.Light: return 0.85f;
            case ArmorType.Heavy: return 0.55f;
            default: return 1f;
        }
    }

    public void ApplySlow(float multiplier, float duration)
    {
        if (multiplier < slowMultiplier)
        {
            slowMultiplier = multiplier;
            slowTimeRemaining = duration;
        }
        else
        {
            slowTimeRemaining = Mathf.Max(slowTimeRemaining, duration);
        }
    }

    public void ApplyPoison(float damagePerSecond, float duration)
    {
        poisonDps = Mathf.Max(poisonDps, damagePerSecond);
        poisonTimeRemaining = duration;
    }

    public bool IsSlowed => slowTimeRemaining > 0f;
    public bool IsPoisoned => poisonTimeRemaining > 0f;

    private void TickStatusEffects()
    {
        if (slowTimeRemaining > 0f)
        {
            slowTimeRemaining -= Time.deltaTime;
            if (slowTimeRemaining <= 0f) slowMultiplier = 1f;
        }

        if (poisonTimeRemaining > 0f)
        {
            poisonTimeRemaining -= Time.deltaTime;
            TakeDamage(poisonDps * Time.deltaTime, DamageType.Poison);
            if (poisonTimeRemaining <= 0f) poisonDps = 0f;
        }
    }

    // ---------------- Blocking (chokepoints) ----------------

    public bool TryGetBlocked(Transform blockerTransform)
    {
        if (IsDead) return false;
        if (moveType == EnemyMoveType.Flying) return false;
        if (blocker != null) return false;

        blocker = blockerTransform;
        attackTimer = 0f;
        return true;
    }

    public void ReleaseFromBlock()
    {
        blocker = null;
    }

    private void AttackBlocker()
    {
        // Blocker destroyed or deactivated (hero died) - resume walking.
        if (blocker == null || !blocker.gameObject.activeInHierarchy)
        {
            blocker = null;
            return;
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            attackTimer = attackInterval;
            IBlockable target = blocker.GetComponent<IBlockable>();
            target?.ReceiveMeleeDamage(meleeDamage);
        }
    }

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

    private void Despawn()
    {
        foreach (var ability in GetComponents<EnemyAbilityBehaviour>())
            Destroy(ability);
        Damaged = null;
        Died = null;
        Leaked = null;
        Destroy(gameObject);
    }

    public float HealthPercent() => maxHealth > 0f ? currentHealth / maxHealth : 0f;
}

/// <summary>
/// Anything an enemy can be blocked by and fight against (heroes, barricades).
/// </summary>
public interface IBlockable
{
    void ReceiveMeleeDamage(float amount);
}
