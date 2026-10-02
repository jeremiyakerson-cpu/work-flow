using System.Collections.Generic;
using TowerDefense.Core;
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
    [Tooltip("Stable key for saves and content lookups, e.g. \"grunt\". Never rename once shipped.")]
    public string id = "grunt";
    public string enemyName = "Grunt";
    [TextArea] public string description;
    public GameObject prefab;          // visual/model prefab, must have Enemy.cs on it
    public Sprite icon;
    [Tooltip("Visual hints for procedurally generated art when no prefab art exists.")]
    public Color tint = Color.white;
    public float visualScale = 1f;

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
    [Tooltip("Relative chance to be picked among unlocked types (1 = normal, 0 = never in regular waves).")]
    [Min(0f)] public float spawnWeight = 1f;

    [Header("Boss Only")]
    public bool isBoss = false;
    public float bossHealthMultiplier = 8f;
    public int bossGoldMultiplier = 20;

    [Header("Abilities (bosses and elites)")]
    [Tooltip("Each entry attaches runtime behaviour to the spawned enemy (charge, summon, enrage...).")]
    public List<EnemyAbilityData> abilities = new List<EnemyAbilityData>();

    /// <summary>Engine-free view of this type for the WavePlanner.</summary>
    public EnemyTypeInfo ToTypeInfo()
    {
        return new EnemyTypeInfo(baseHealth, baseSpeed, baseGoldReward, unlockWave, spawnWeight,
                                 moveType == EnemyMoveType.Flying, bossHealthMultiplier, bossGoldMultiplier);
    }
}
