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
    [Tooltip("0 = single target. >0 = every enemy within this radius of the impact takes full damage.")]
    public float splashRadius = 0f;

    [Header("On-Hit Effects")]
    public bool appliesSlow = false;
    public float slowMultiplier = 0.5f;
    public float slowDuration = 1.5f;
    public bool appliesPoison = false;
    public float poisonDps = 2f;
    public float poisonDuration = 3f;

    [Header("Linear Upgrades (level 1->2)")]
    public float level2DamageMult = 1.5f;
    public float level2RangeMult = 1.1f;
    public float level2FireRateMult = 1.15f;
    public int level2Cost = 40;

    [Header("Branch A (e.g. 'Sniper' - damage/range focus)")]
    public string pathAName = "Sniper";
    public float pathADamageMult = 2f;
    public float pathARangeMult = 1.25f;
    public float pathAFireRateMult = 1f;
    public bool pathAAppliesSlow = false;
    public int pathACost = 80;

    [Header("Branch B (e.g. 'Barrage' - speed/utility focus)")]
    public string pathBName = "Barrage";
    public float pathBFireRateMult = 1.6f;
    public float pathBDamageMult = 1f;
    public float pathBRangeMult = 1f;
    public bool pathBAppliesSlow = true;
    public int pathBCost = 80;

    [Header("Final Tier (level 3->4, applies on top of chosen branch)")]
    public float level4DamageMult = 1.4f;
    public float level4FireRateMult = 1.2f;
    public float level4RangeMult = 1f;
    public int level4Cost = 140;

    [Header("Projectile")]
    [Tooltip("Arc height for lobbed shots (artillery). 0 = straight homing shot. Overrides the projectile prefab when > 0.")]
    public float projectileArcHeight = 0f;

    /// <summary>Engine-free copy of the stats/upgrade curve for TowerUpgradeMath.</summary>
    public TowerUpgradeSpec ToUpgradeSpec()
    {
        return new TowerUpgradeSpec
        {
            range = range, fireRate = fireRate, damage = damage, baseCost = baseCost,
            appliesSlow = appliesSlow, appliesPoison = appliesPoison,
            level2DamageMult = level2DamageMult, level2RangeMult = level2RangeMult,
            level2FireRateMult = level2FireRateMult, level2Cost = level2Cost,
            pathADamageMult = pathADamageMult, pathARangeMult = pathARangeMult, pathAFireRateMult = pathAFireRateMult,
            pathAAppliesSlow = pathAAppliesSlow, pathACost = pathACost,
            pathBDamageMult = pathBDamageMult, pathBRangeMult = pathBRangeMult, pathBFireRateMult = pathBFireRateMult,
            pathBAppliesSlow = pathBAppliesSlow, pathBCost = pathBCost,
            level4DamageMult = level4DamageMult, level4RangeMult = level4RangeMult,
            level4FireRateMult = level4FireRateMult, level4Cost = level4Cost,
        };
    }
}
