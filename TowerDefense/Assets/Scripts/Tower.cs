using System;
using UnityEngine;

/// <summary>
/// Attach to every tower prefab, with a TowerData asset assigned in the
/// Inspector or passed to Init(). All stats/upgrade curves live in the data
/// asset - this script is just the runtime behavior, so one Tower.cs serves
/// every tower type. Uses 2D physics (Physics2D) - matches the 2D URP setup.
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

    // Live stats - start as a copy of the data asset's base stats, then get
    // multiplied by upgrade tiers. The asset itself is never modified.
    private float range;
    private float fireRate;
    private float damage;
    private bool appliesSlow;
    private float slowMultiplier;
    private float slowDuration;
    private bool appliesPoison;
    private float poisonDps;
    private float poisonDuration;
    private int nextUpgradeCost;
    private int totalInvested;
    private bool initialized;

    private float fireCooldown = 0f;
    private Transform currentTarget;

    public float Range => range;
    public float FireRate => fireRate;
    public float Damage => damage;
    public bool AppliesSlow => appliesSlow;
    public bool AppliesPoison => appliesPoison;
    public int TotalInvested => totalInvested;
    public Transform CurrentTarget => currentTarget;
    public bool IsInitialized => initialized;

    private void Awake()
    {
        // Prefabs with data assigned in the Inspector initialise themselves.
        // Towers built at runtime get Init(data) from TowerPlacement after Instantiate.
        if (data != null) Init(data);
    }

    /// <summary>Copy base stats from the data asset. Safe to call once after Instantiate.</summary>
    public void Init(TowerData towerData)
    {
        data = towerData;
        if (data == null)
        {
            Debug.LogError($"{name}: Tower has no TowerData assigned.");
            initialized = false;
            return;
        }

        range = data.range;
        fireRate = data.fireRate;
        damage = data.damage;
        appliesSlow = data.appliesSlow;
        slowMultiplier = data.slowMultiplier;
        slowDuration = data.slowDuration;
        appliesPoison = data.appliesPoison;
        poisonDps = data.poisonDps;
        poisonDuration = data.poisonDuration;
        nextUpgradeCost = data.level2Cost;
        totalInvested = data.baseCost;
        upgradeLevel = 1;
        chosenPath = UpgradePath.None;
        fireCooldown = 0f;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized) return;

        FindTarget();

        fireCooldown -= Time.deltaTime;
        if (currentTarget != null && fireCooldown <= 0f)
        {
            Fire();
            fireCooldown = 1f / Mathf.Max(0.01f, fireRate);
        }
    }

    private void FindTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, range, LayerMask.GetMask("Enemy"));
        Transform best = null;
        float bestScore = float.NegativeInfinity;

        foreach (var hit in hits)
        {
            Enemy e = hit.GetComponent<Enemy>();
            if (e == null || e.IsDead) continue;
            if (e.IsFlying && !data.canTargetFlying) continue;
            if (!e.IsFlying && !data.canTargetGround) continue;

            float score = Score(e);
            if (score > bestScore)
            {
                bestScore = score;
                best = hit.transform;
            }
        }

        currentTarget = best;
    }

    private float Score(Enemy e)
    {
        switch (targetPriority)
        {
            case TargetPriority.Last: return -e.PathProgress;
            case TargetPriority.Strongest: return e.CurrentHealth;
            case TargetPriority.Weakest: return -e.CurrentHealth;
            case TargetPriority.Closest: return -Vector3.Distance(transform.position, e.transform.position);
            default: return e.PathProgress;
        }
    }

    private void Fire()
    {
        if (currentTarget == null) return;
        AnyFired?.Invoke(this);

        if (data.projectilePrefab != null)
        {
            Vector3 origin = firePoint != null ? firePoint.position : transform.position;
            GameObject proj = Instantiate(data.projectilePrefab, origin, Quaternion.identity);
            Projectile p = proj.GetComponent<Projectile>();
            if (p != null)
                p.Init(currentTarget, damage, data.damageType, this);
        }
        else
        {
            ApplyHit(currentTarget.GetComponent<Enemy>());
        }
    }

    /// <summary>Called by Projectile on impact, or directly for hitscan towers. Handles splash.</summary>
    public void ApplyHit(Enemy e)
    {
        if (e == null) return;
        if (data != null && data.splashRadius > 0f)
            ApplySplash(e.transform.position);
        else
            ApplyHitSingle(e);
    }

    private void ApplySplash(Vector3 center)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, data.splashRadius, LayerMask.GetMask("Enemy"));
        foreach (var hit in hits)
        {
            Enemy e = hit.GetComponent<Enemy>();
            if (e == null || e.IsDead) continue;
            if (e.IsFlying && !data.canTargetFlying) continue;
            ApplyHitSingle(e);
        }
    }

    private void ApplyHitSingle(Enemy e)
    {
        if (e == null || e.IsDead) return;
        if (appliesSlow) e.ApplySlow(slowMultiplier, slowDuration);
        if (appliesPoison) e.ApplyPoison(poisonDps, poisonDuration);
        e.TakeDamage(damage, data.damageType);
    }

    // ---------------- Upgrades (all curves pulled from TowerData) ----------------

    public bool CanUpgrade() => upgradeLevel < 4;
    public int NextUpgradeCost() => nextUpgradeCost;
    /// <summary>Cost of the branch choice at level 2 (each branch can be priced separately).</summary>
    public int BranchCost(UpgradePath path) => path == UpgradePath.PathA ? data.pathACost : data.pathBCost;
    /// <summary>Gold returned when sold: a fraction of everything spent on this tower.</summary>
    public int SellValue() => Mathf.RoundToInt(totalInvested * sellRefundFraction);

    /// <summary>Level 1->2: standard linear upgrade, no choice involved.</summary>
    public bool Upgrade()
    {
        if (upgradeLevel != 1) return false;
        if (!GameManager.Instance.SpendGold(nextUpgradeCost)) return false;

        totalInvested += nextUpgradeCost;
        damage *= data.level2DamageMult;
        range *= data.level2RangeMult;
        fireRate *= data.level2FireRateMult;
        upgradeLevel = 2;
        nextUpgradeCost = Mathf.Min(data.pathACost, data.pathBCost);
        RaiseUpgraded();
        return true;
    }

    /// <summary>
    /// Level 2->3: the KR-style specialization choice. Call with PathA or
    /// PathB from your upgrade UI (show data.pathAName / data.pathBName as
    /// button labels so the UI reads whatever the designer named the branch).
    /// </summary>
    public bool ChooseBranch(UpgradePath path)
    {
        if (upgradeLevel != 2 || chosenPath != UpgradePath.None || path == UpgradePath.None) return false;
        int cost = BranchCost(path);
        if (!GameManager.Instance.SpendGold(cost)) return false;

        totalInvested += cost;
        chosenPath = path;
        upgradeLevel = 3;

        if (path == UpgradePath.PathA)
        {
            damage *= data.pathADamageMult;
            range *= data.pathARangeMult;
        }
        else
        {
            fireRate *= data.pathBFireRateMult;
            appliesSlow = appliesSlow || data.pathBAppliesSlow;
        }

        nextUpgradeCost = data.level4Cost;
        RaiseUpgraded();
        return true;
    }

    /// <summary>Level 3->4: final tier, amplifies whichever branch was chosen.</summary>
    public bool UpgradeFinal()
    {
        if (upgradeLevel != 3) return false;
        if (!GameManager.Instance.SpendGold(nextUpgradeCost)) return false;

        totalInvested += nextUpgradeCost;
        damage *= data.level4DamageMult;
        fireRate *= data.level4FireRateMult;
        upgradeLevel = 4;
        RaiseUpgraded();
        return true;
    }

    private void RaiseUpgraded()
    {
        Upgraded?.Invoke(this);
        AnyUpgraded?.Invoke(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, initialized ? range : (data != null ? data.range : 0f));
    }
}
