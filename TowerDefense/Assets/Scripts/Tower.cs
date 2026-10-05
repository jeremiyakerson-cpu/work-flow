using System;
using TowerDefense.Core;
using TowerDefense.Pooling;
using UnityEngine;

/// <summary>
/// Attach to every tower prefab, with a TowerData asset assigned in the
/// Inspector or passed to Init(). All stats/upgrade curves live in the data
/// asset and the math lives in TowerDefense.Core.TowerUpgradeMath - this
/// script is just the runtime behavior, so one Tower.cs serves every tower
/// type. Uses 2D physics (Physics2D) - matches the 2D URP setup.
/// </summary>
public class Tower : MonoBehaviour
{
    [Header("Data")]
    public TowerData data;

    [Header("Scene Refs")]
    public Transform firePoint;

    [Header("Behaviour")]
    public TargetPriority targetPriority = TargetPriority.First;
    [Range(0f, 1f)] public float sellRefundFraction = 0.7f;

    public enum UpgradePath { None, PathA, PathB }
    public UpgradePath chosenPath { get; private set; } = UpgradePath.None;
    public int upgradeLevel { get; private set; } = 1;

    /// <summary>Any tower fired (for muzzle VFX / audio).</summary>
    public static event Action<Tower> AnyFired;
    /// <summary>Any tower changed level or branch.</summary>
    public static event Action<Tower> AnyUpgraded;
    public event Action<Tower> Upgraded;

    // Live stats - recomputed from the data asset for the current (level, branch).
    // The asset itself is never modified.
    private TowerUpgradeSpec spec;
    private TowerStats stats;
    private int totalInvested;
    private bool initialized;

    private float fireCooldown = 0f;
    private Enemy currentEnemy;

    // Reused query buffers: no allocations in Update.
    private readonly Enemy[] inRange = new Enemy[EnemyQuery.BufferSize];
    private readonly TargetCandidate[] candidates = new TargetCandidate[EnemyQuery.BufferSize];

    public float Range => stats.Range;
    public float FireRate => stats.FireRate;
    public float Damage => stats.Damage;
    public bool AppliesSlow => stats.AppliesSlow;
    public bool AppliesPoison => stats.AppliesPoison;
    /// <summary>Damage x fire rate at the current level, before resistances. For tooltips.</summary>
    public float Dps => stats.Dps;
    public int TotalInvested => totalInvested;
    public Transform CurrentTarget => currentEnemy != null ? currentEnemy.transform : null;
    /// <summary>The enemy being shot at this frame, or null.</summary>
    public Enemy CurrentEnemy => currentEnemy;
    public bool IsInitialized => initialized;
    /// <summary>Last level reached by plain Upgrade() calls; level 4 is the specialization.</summary>
    public const int MaxLinearLevel = TowerUpgradeMath.MaxLinearLevel;
    /// <summary>The specialized (elite) level. Nothing comes after it.</summary>
    public const int MaxLevel = TowerUpgradeMath.MaxLevel;
    public bool IsMaxLevel => upgradeLevel >= MaxLevel;
    /// <summary>True once ChooseBranch has turned this tower into its elite variant (level 4).</summary>
    public bool IsSpecialized => upgradeLevel >= MaxLevel && chosenPath != UpgradePath.None;
    /// <summary>True at level 3 before a branch is chosen: ChooseBranch is the next (and last) step.</summary>
    public bool CanSpecialize => initialized && upgradeLevel == MaxLinearLevel && chosenPath == UpgradePath.None;
    /// <summary>Splash radius of the current level (0 = single target).</summary>
    public float SplashRadius => stats.SplashRadius;
    /// <summary>Poison damage per second of the current level (0 if the tower doesn't poison).</summary>
    public float PoisonDps => stats.PoisonDps;
    /// <summary>Core-side view of chosenPath.</summary>
    public UpgradeBranch Branch => ToBranch(chosenPath);

    private void Awake()
    {
        // Prefabs with data assigned in the Inspector initialise themselves.
        // Towers built at runtime get Init(data) from TowerPlacement after Instantiate.
        if (data != null) Init(data);
    }

    /// <summary>Copy base stats from the data asset (resets upgrades). Safe to call again after Instantiate.</summary>
    public void Init(TowerData towerData)
    {
        data = towerData;
        if (data == null)
        {
            Debug.LogError($"{name}: Tower has no TowerData assigned.");
            initialized = false;
            return;
        }

        spec = data.ToUpgradeSpec();
        upgradeLevel = 1;
        chosenPath = UpgradePath.None;
        RecalculateStats();
        fireCooldown = 0f;
        currentEnemy = null;
        initialized = true;
    }

    private void RecalculateStats()
    {
        UpgradeBranch b = ToBranch(chosenPath);
        stats = TowerUpgradeMath.StatsAt(spec, upgradeLevel, b);
        totalInvested = TowerUpgradeMath.TotalInvested(spec, upgradeLevel, b);
    }

    private void Update()
    {
        if (!initialized) return;

        FindTarget();

        fireCooldown -= Time.deltaTime;
        if (currentEnemy == null)
        {
            // Idle towers are ready to shoot, but don't bank a burst of shots.
            if (fireCooldown < 0f) fireCooldown = 0f;
            return;
        }
        if (fireCooldown <= 0f)
        {
            Fire();
            // += keeps the cadence exact at high game speeds instead of drifting by a frame per shot.
            fireCooldown += 1f / Mathf.Max(0.01f, stats.FireRate);
            if (fireCooldown < 0f) fireCooldown = 0f;
        }
    }

    private void FindTarget()
    {
        Vector2 origin = transform.position;
        int n = EnemyQuery.OverlapEnemies(origin, stats.Range, inRange);
        for (int i = 0; i < n; i++)
        {
            Enemy e = inRange[i];
            Vector2 d = (Vector2)e.transform.position - origin;
            candidates[i] = new TargetCandidate(e.PathProgress, e.RemainingPathDistance, e.CurrentHealth,
                                                d.x * d.x + d.y * d.y, e.IsFlying);
        }

        int best = TargetSelector.SelectIndex(candidates, n, targetPriority, data.canTargetGround, data.canTargetFlying);
        currentEnemy = best >= 0 ? inRange[best] : null;
        Array.Clear(inRange, 0, n); // don't pin pooled enemies in the buffer
    }

    private void Fire()
    {
        if (currentEnemy == null) return;
        AnyFired?.Invoke(this);

        if (data.projectilePrefab != null)
        {
            Vector3 origin = firePoint != null ? firePoint.position : transform.position;
            GameObject proj = GameObjectPool.Spawn(data.projectilePrefab, origin, Quaternion.identity);
            if (proj != null && proj.TryGetComponent(out Projectile p))
                p.Init(currentEnemy.transform, stats.Damage, data.damageType, this);
            else if (proj != null)
                GameObjectPool.Despawn(proj);
        }
        else
        {
            ApplyHit(currentEnemy);
        }
    }

    // ---------------- Hits ----------------

    /// <summary>
    /// Everything a hit needs, captured at fire time so a projectile still lands
    /// correctly if the tower is sold or upgraded while it's in flight.
    /// </summary>
    public struct HitInfo
    {
        public float damage;
        public DamageType damageType;
        public float splashRadius;
        public bool canTargetGround;
        public bool canTargetFlying;
        public bool appliesSlow;
        public float slowMultiplier;
        public float slowDuration;
        public bool appliesPoison;
        public float poisonDps;
        public float poisonDuration;

        /// <summary>Plain damage, no splash or effects, hits anything.</summary>
        public static HitInfo Simple(float damage, DamageType type) => new HitInfo
        {
            damage = damage, damageType = type, canTargetGround = true, canTargetFlying = true,
        };
    }

    /// <summary>Snapshot of this tower's current hit (damage, type, splash, effects).</summary>
    public HitInfo CreateHitInfo()
    {
        if (data == null) return HitInfo.Simple(stats.Damage, DamageType.Physical);
        return new HitInfo
        {
            damage = stats.Damage,
            damageType = data.damageType,
            splashRadius = stats.SplashRadius,
            canTargetGround = data.canTargetGround,
            canTargetFlying = data.canTargetFlying,
            appliesSlow = stats.AppliesSlow,
            slowMultiplier = data.slowMultiplier,
            slowDuration = data.slowDuration,
            appliesPoison = stats.AppliesPoison,
            poisonDps = stats.PoisonDps,
            poisonDuration = data.poisonDuration,
        };
    }

    /// <summary>Called by Projectile on impact, or directly for hitscan towers. Handles splash.</summary>
    public void ApplyHit(Enemy e)
    {
        if (e == null) return;
        ResolveHit(CreateHitInfo(), e.transform.position, e);
    }

    /// <summary>Land a hit at a point: splash around it, or hit directTarget if it's still alive.</summary>
    public void ApplyHitAt(Vector3 point, Enemy directTarget)
    {
        ResolveHit(CreateHitInfo(), point, directTarget);
    }

    /// <summary>
    /// Shared hit resolution (towers and orphaned projectiles). Splash hits every
    /// targetable living enemy within hit.splashRadius of point (each once);
    /// otherwise only directTarget is hit, if it's still alive.
    /// </summary>
    public static void ResolveHit(in HitInfo hit, Vector3 point, Enemy directTarget)
    {
        if (hit.splashRadius > 0f)
        {
            Enemy[] buffer = EnemyQuery.RentBuffer();
            try
            {
                int n = EnemyQuery.OverlapEnemies(point, hit.splashRadius, buffer);
                for (int i = 0; i < n; i++)
                {
                    Enemy e = buffer[i];
                    if (e.IsFlying ? !hit.canTargetFlying : !hit.canTargetGround) continue;
                    ApplyHitSingle(hit, e);
                }
            }
            finally
            {
                EnemyQuery.ReturnBuffer(buffer);
            }
        }
        else if (directTarget != null)
        {
            ApplyHitSingle(hit, directTarget);
        }
    }

    private static void ApplyHitSingle(in HitInfo hit, Enemy e)
    {
        if (e == null || e.IsDead) return;
        if (hit.appliesSlow) e.ApplySlow(hit.slowMultiplier, hit.slowDuration);
        if (hit.appliesPoison) e.ApplyPoison(hit.poisonDps, hit.poisonDuration);
        e.TakeDamage(hit.damage, hit.damageType);
    }

    // ---------------- Upgrades (curves in TowerData, math in Core) ----------------

    /// <summary>True when another plain (linear) upgrade is available: level 1 or 2.</summary>
    public bool CanUpgrade() => initialized && upgradeLevel < MaxLinearLevel;
    /// <summary>Gold for the next linear upgrade (level 1 or 2). 0 at level 3 (use BranchCost) and level 4.</summary>
    public int NextUpgradeCost() => initialized ? TowerUpgradeMath.NextUpgradeCost(spec, upgradeLevel, ToBranch(chosenPath)) : 0;
    /// <summary>Gold to specialize into <paramref name="path"/> at level 3 (each branch is priced separately). 0 for None.</summary>
    public int BranchCost(UpgradePath path) =>
        initialized && path != UpgradePath.None ? TowerUpgradeMath.BranchCost(spec, ToBranch(path)) : 0;
    /// <summary>Gold returned when sold: a fraction of everything spent on this tower.</summary>
    public int SellValue() => EconomyRules.SellRefund(totalInvested, sellRefundFraction);
    /// <summary>
    /// Stats this tower would have at (level, path), for upgrade-menu previews. Exactly
    /// TowerUpgradeMath.StatsAt, so it throws for unreachable states: pass None for
    /// levels 1-3 and PathA/PathB for level 4.
    /// </summary>
    public TowerStats PreviewStats(int level, UpgradePath path) => TowerUpgradeMath.StatsAt(spec, level, ToBranch(path));

    /// <summary>Designer name of a specialization (data.pathAName / pathBName), or "" for None.</summary>
    public string BranchName(UpgradePath path)
    {
        if (data == null) return "";
        return path == UpgradePath.PathA ? data.pathAName : path == UpgradePath.PathB ? data.pathBName : "";
    }

    /// <summary>One-line description of a specialization, or "" for None.</summary>
    public string BranchDescription(UpgradePath path)
    {
        if (data == null) return "";
        return path == UpgradePath.PathA ? data.pathADescription : path == UpgradePath.PathB ? data.pathBDescription : "";
    }

    /// <summary>Linear upgrade: level 1->2 or 2->3. No choice involved. False at level 3+ (specialize instead).</summary>
    public bool Upgrade()
    {
        if (!CanUpgrade()) return false;
        if (!TrySpend(NextUpgradeCost())) return false;

        upgradeLevel++;
        RecalculateStats();
        RaiseUpgraded();
        return true;
    }

    /// <summary>
    /// Level 3->4: the KR-style specialization, the tower's capstone. Call with
    /// PathA or PathB from your upgrade UI (show data.pathAName / pathBName and
    /// their descriptions so the UI reads whatever the designer named the branch).
    /// Only valid at level 3.
    /// </summary>
    public bool ChooseBranch(UpgradePath path)
    {
        if (!CanSpecialize || (path != UpgradePath.PathA && path != UpgradePath.PathB)) return false;
        if (!TrySpend(BranchCost(path))) return false;

        chosenPath = path;
        upgradeLevel = MaxLevel;
        RecalculateStats();
        RaiseUpgraded();
        return true;
    }

    // No GameManager in the scene (sandbox / test scenes) means upgrades are free.
    private static bool TrySpend(int cost)
    {
        if (GameManager.Instance == null) return true;
        return GameManager.Instance.SpendGold(cost);
    }

    private void RaiseUpgraded()
    {
        Upgraded?.Invoke(this);
        AnyUpgraded?.Invoke(this);
    }

    /// <summary>Tower.UpgradePath -> Core.UpgradeBranch.</summary>
    public static UpgradeBranch ToBranch(UpgradePath path) =>
        path == UpgradePath.PathA ? UpgradeBranch.A : path == UpgradePath.PathB ? UpgradeBranch.B : UpgradeBranch.None;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, initialized ? stats.Range : (data != null ? data.range : 0f));
    }
}
