using TowerDefense.Core;
using UnityEngine;

/// <summary>
/// One asset per tower type (ArcherTower, MageTower, ArtilleryTower, etc).
/// Create via Assets > Create > Tower Defense > Tower Data.
/// Tower.cs reads from this instead of hardcoding stats, so adding a new
/// tower type is "make a new asset" instead of "write a new script."
/// </summary>
[CreateAssetMenu(fileName = "NewTowerData", menuName = "Tower Defense/Tower Data")]
public class TowerData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable key for saves and content lookups, e.g. \"archer\". Never rename once shipped.")]
    public string id = "archer";
    public string towerName = "Archer Tower";
    [TextArea] public string description;
    public Sprite icon;
    [Tooltip("Prefab (or runtime template) with Tower.cs. Lets the shop build from data alone.")]
    public GameObject towerPrefab;
    public GameObject projectilePrefab;   // leave null for hitscan towers

    [Header("Base Stats (level 1)")]
    public float range = 4f;
    public float fireRate = 1f;
    public float damage = 5f;
    public DamageType damageType = DamageType.Physical;
    public int baseCost = 50;

    [Header("Targeting")]
    public bool canTargetGround = true;
    public bool canTargetFlying = true;   // artillery-style towers set this false
    [Tooltip("Level-1 splash. 0 = single target. >0 = every enemy within this radius of the impact takes full damage. Upgrades add their SplashBonus.")]
    public float splashRadius = 0f;

    [Header("On-Hit Effects")]
    public bool appliesSlow = false;
    public float slowMultiplier = 0.5f;
    public float slowDuration = 1.5f;
    public bool appliesPoison = false;
    [Tooltip("Level-1 poison damage per second. Upgrades multiply it by their PoisonMult.")]
    public float poisonDps = 2f;
    public float poisonDuration = 3f;

    [Header("Linear Upgrade (level 1->2)")]
    public float level2DamageMult = 1.5f;
    public float level2RangeMult = 1.1f;
    public float level2FireRateMult = 1.15f;
    [Tooltip("Multiplies poisonDps (poison towers only). 1 = unchanged.")]
    public float level2PoisonMult = 1f;
    [Tooltip("Added to splashRadius, in world units. Can grant splash to a single-target tower.")]
    public float level2SplashBonus = 0f;
    public int level2Cost = 60;

    [Header("Linear Upgrade (level 2->3)")]
    public float level3DamageMult = 1.4f;
    public float level3RangeMult = 1.1f;
    public float level3FireRateMult = 1.1f;
    public float level3PoisonMult = 1f;
    public float level3SplashBonus = 0f;
    public int level3Cost = 90;

    [Header("Specialization A (level 3->4, e.g. 'Sniper' - damage/range focus)")]
    public string pathAName = "Sniper";
    [Tooltip("One line shown on the specialization button's tooltip.")]
    public string pathADescription = "Heavy shots with extra reach.";
    public float pathADamageMult = 2f;
    public float pathARangeMult = 1.25f;
    public float pathAFireRateMult = 1f;
    public float pathAPoisonMult = 1f;
    public float pathASplashBonus = 0f;
    public bool pathAAppliesSlow = false;
    public int pathACost = 150;

    [Header("Specialization B (level 3->4, e.g. 'Barrage' - speed/utility focus)")]
    public string pathBName = "Barrage";
    public string pathBDescription = "Rapid fire that slows its targets.";
    public float pathBFireRateMult = 1.6f;
    public float pathBDamageMult = 1f;
    public float pathBRangeMult = 1f;
    public float pathBPoisonMult = 1f;
    public float pathBSplashBonus = 0f;
    public bool pathBAppliesSlow = true;
    public int pathBCost = 140;

    [Header("Projectile")]
    [Tooltip("Arc height for lobbed shots (artillery). 0 = straight homing shot. Overrides the projectile prefab when > 0.")]
    public float projectileArcHeight = 0f;

    /// <summary>Engine-free copy of the stats/upgrade curve for TowerUpgradeMath.</summary>
    public TowerUpgradeSpec ToUpgradeSpec()
    {
        return new TowerUpgradeSpec
        {
            range = range, fireRate = fireRate, damage = damage, baseCost = baseCost,
            splashRadius = splashRadius, poisonDps = poisonDps,
            appliesSlow = appliesSlow, appliesPoison = appliesPoison,
            level2DamageMult = level2DamageMult, level2RangeMult = level2RangeMult, level2FireRateMult = level2FireRateMult,
            level2PoisonMult = level2PoisonMult, level2SplashBonus = level2SplashBonus, level2Cost = level2Cost,
            level3DamageMult = level3DamageMult, level3RangeMult = level3RangeMult, level3FireRateMult = level3FireRateMult,
            level3PoisonMult = level3PoisonMult, level3SplashBonus = level3SplashBonus, level3Cost = level3Cost,
            pathADamageMult = pathADamageMult, pathARangeMult = pathARangeMult, pathAFireRateMult = pathAFireRateMult,
            pathAPoisonMult = pathAPoisonMult, pathASplashBonus = pathASplashBonus,
            pathAAppliesSlow = pathAAppliesSlow, pathACost = pathACost,
            pathBDamageMult = pathBDamageMult, pathBRangeMult = pathBRangeMult, pathBFireRateMult = pathBFireRateMult,
            pathBPoisonMult = pathBPoisonMult, pathBSplashBonus = pathBSplashBonus,
            pathBAppliesSlow = pathBAppliesSlow, pathBCost = pathBCost,
        };
    }

    /// <summary>
    /// Inverse of ToUpgradeSpec: copy base stats and the whole upgrade curve
    /// from an engine-free spec (used by the code-built default content).
    /// Identity, targeting, slow/poison durations and branch names are untouched.
    /// </summary>
    public void ApplyUpgradeSpec(in TowerUpgradeSpec s)
    {
        range = s.range; fireRate = s.fireRate; damage = s.damage; baseCost = s.baseCost;
        splashRadius = s.splashRadius; poisonDps = s.poisonDps;
        appliesSlow = s.appliesSlow; appliesPoison = s.appliesPoison;
        level2DamageMult = s.level2DamageMult; level2RangeMult = s.level2RangeMult; level2FireRateMult = s.level2FireRateMult;
        level2PoisonMult = s.level2PoisonMult; level2SplashBonus = s.level2SplashBonus; level2Cost = s.level2Cost;
        level3DamageMult = s.level3DamageMult; level3RangeMult = s.level3RangeMult; level3FireRateMult = s.level3FireRateMult;
        level3PoisonMult = s.level3PoisonMult; level3SplashBonus = s.level3SplashBonus; level3Cost = s.level3Cost;
        pathADamageMult = s.pathADamageMult; pathARangeMult = s.pathARangeMult; pathAFireRateMult = s.pathAFireRateMult;
        pathAPoisonMult = s.pathAPoisonMult; pathASplashBonus = s.pathASplashBonus;
        pathAAppliesSlow = s.pathAAppliesSlow; pathACost = s.pathACost;
        pathBDamageMult = s.pathBDamageMult; pathBRangeMult = s.pathBRangeMult; pathBFireRateMult = s.pathBFireRateMult;
        pathBPoisonMult = s.pathBPoisonMult; pathBSplashBonus = s.pathBSplashBonus;
        pathBAppliesSlow = s.pathBAppliesSlow; pathBCost = s.pathBCost;
    }
}
