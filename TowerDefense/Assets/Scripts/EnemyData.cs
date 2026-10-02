using UnityEngine;

/// <summary>
/// One asset per enemy type (Grunt, Knight, Bat, etc).
/// Create via Assets > Create > Tower Defense > Enemy Data.
/// WaveManager references these directly - no more digging stats out of
/// prefab components at spawn time.
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Tower Defense/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string enemyName = "Grunt";
    public GameObject prefab;          // visual/model prefab, must have Enemy.cs on it
    public Sprite icon;

    [Header("Base Stats (before wave scaling)")]
    public float baseHealth = 10f;
    public float baseSpeed = 2f;
    public int baseGoldReward = 5;
    public int damageToBase = 1;
    public float meleeDamage = 2f;
    public float attackInterval = 1f;

    [Header("Type")]
    public ArmorType armor = ArmorType.None;
    public EnemyMoveType moveType = EnemyMoveType.Ground;

    [Header("Wave Gating")]
    [Tooltip("This enemy type won't appear in the spawn pool before this wave number.")]
    public int unlockWave = 1;

    [Header("Boss Only")]
    public bool isBoss = false;
    public float bossHealthMultiplier = 8f;
    public int bossGoldMultiplier = 20;
}
