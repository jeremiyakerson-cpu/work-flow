using UnityEngine;

/// <summary>
/// Attach to every tower prefab, with a TowerData asset assigned in the
/// Inspector. All stats/upgrade curves live in the data asset - this script
/// is just the runtime behavior, so one Tower.cs serves every tower type.
/// Uses 2D physics (Physics2D) - matches the 2D URP setup for a KR-style game.
/// </summary>
public class Tower : MonoBehaviour
{
    [Header("Data")]
    public TowerData data;

    [Header("Scene Refs")]
    public Transform firePoint;

    public enum UpgradePath { None, PathA, PathB }
    public UpgradePath chosenPath { get; private set; } = UpgradePath.None;
    public int upgradeLevel { get; private set; } = 1;

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

    private float fireCooldown = 0f;
    private Transform currentTarget;

    private void Awake()
    {
        if (data == null)
        {
            Debug.LogError($"{name}: Tower has no TowerData assigned.");
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
    }

    private void Update()
    {
        if (data == null) return;

        FindTarget();

        if (currentTarget != null)
        {
            fireCooldown -= Time.deltaTime;
            if (fireCooldown <= 0f)
            {
                Fire();
                fireCooldown = 1f / fireRate;
            }
        }
    }

    private void FindTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, range, LayerMask.GetMask("Enemy"));
        Transform best = null;
        float bestProgress = -1f;

        foreach (var hit in hits)
        {
            Enemy e = hit.GetComponent<Enemy>();
            if (e == null) continue;

            float progress = 1f - e.HealthPercent(); // placeholder - swap for waypoint index for true "furthest along"
            if (progress > bestProgress)
            {
                bestProgress = progress;
                best = hit.transform;
            }
        }

        currentTarget = best;
    }

    private void Fire()
    {
        if (currentTarget == null) return;

        if (data.projectilePrefab != null && firePoint != null)
        {
            GameObject proj = Instantiate(data.projectilePrefab, firePoint.position, Quaternion.identity);
            Projectile p = proj.GetComponent<Projectile>();
            if (p != null)
                p.Init(currentTarget, damage, data.damageType, this);
        }
        else
        {
            ApplyHit(currentTarget.GetComponent<Enemy>());
        }
    }

    /// <summary>Called by Projectile on impact, or directly for hitscan towers.</summary>
    public void ApplyHit(Enemy e)
    {
        if (e == null) return;
        e.TakeDamage(damage, data.damageType);
        if (appliesSlow) e.ApplySlow(slowMultiplier, slowDuration);
        if (appliesPoison) e.ApplyPoison(poisonDps, poisonDuration);
    }

    // ---------------- Upgrades (all curves pulled from TowerData) ----------------

    public bool CanUpgrade() => upgradeLevel < 4;
    public int NextUpgradeCost() => nextUpgradeCost;

    /// <summary>Level 1->2: standard linear upgrade, no choice involved.</summary>
    public bool Upgrade()
    {
        if (upgradeLevel != 1) return false;
        if (!GameManager.Instance.SpendGold(nextUpgradeCost)) return false;

        damage *= data.level2DamageMult;
        range *= data.level2RangeMult;
        fireRate *= data.level2FireRateMult;
        upgradeLevel = 2;
        nextUpgradeCost = data.pathACost; // both branches cost the same in this template - split if desired
        return true;
    }

    /// <summary>
    /// Level 2->3: the KR-style specialization choice. Call with PathA or
    /// PathB from your upgrade UI (show data.pathAName / data.pathBName as
    /// button labels so the UI reads whatever the designer named the branch).
    /// </summary>
    public bool ChooseBranch(UpgradePath path)
    {
        if (upgradeLevel != 2 || chosenPath != UpgradePath.None) return false;
        int cost = path == UpgradePath.PathA ? data.pathACost : data.pathBCost;
        if (!GameManager.Instance.SpendGold(cost)) return false;

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
        return true;
    }

    /// <summary>Level 3->4: final tier, amplifies whichever branch was chosen.</summary>
    public bool UpgradeFinal()
    {
        if (upgradeLevel != 3) return false;
        if (!GameManager.Instance.SpendGold(nextUpgradeCost)) return false;

        damage *= data.level4DamageMult;
        fireRate *= data.level4FireRateMult;
        upgradeLevel = 4;
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
