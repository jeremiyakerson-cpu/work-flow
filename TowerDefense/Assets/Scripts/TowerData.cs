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
    public string towerName = "Archer Tower";
    public Sprite icon;
    public GameObject projectilePrefab;   // leave null for hitscan towers

    [Header("Base Stats (level 1)")]
    public float range = 4f;
    public float fireRate = 1f;
    public float damage = 5f;
    public DamageType damageType = DamageType.Physical;
    public int baseCost = 50;

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
    public int pathACost = 80;

    [Header("Branch B (e.g. 'Barrage' - speed/utility focus)")]
    public string pathBName = "Barrage";
    public float pathBFireRateMult = 1.6f;
    public bool pathBAppliesSlow = true;
    public int pathBCost = 80;

    [Header("Final Tier (level 3->4, applies on top of chosen branch)")]
    public float level4DamageMult = 1.4f;
    public float level4FireRateMult = 1.2f;
    public int level4Cost = 140;
}
