using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One hand-authored map: paths, build slots, hero start, economy and which
/// enemies appear. Positions are world units; the play area spans
/// (0,0) to worldSize, landscape (default 32 x 18 = 16:9).
/// Endless mode cycles through levels while WaveManager keeps escalating.
/// </summary>
[CreateAssetMenu(fileName = "NewLevelData", menuName = "Tower Defense/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable key for saves, e.g. \"meadow\". Never rename once shipped.")]
    public string id = "level";
    public string displayName = "New Level";
    [TextArea] public string description;

    [Header("Layout (world units)")]
    public Vector2 worldSize = new Vector2(32f, 18f);
    [Tooltip("Each path runs from spawn (first point) to the exit (last point). Enemies are spread across paths.")]
    public List<PathDefinition> paths = new List<PathDefinition>();
    public List<Vector2> buildSlots = new List<Vector2>();
    public Vector2 heroStart;

    [Header("Rules")]
    public int startingGold = 250;
    public int startingLives = 20;
    [Tooltip("Waves to survive to win the level. 0 = endless.")]
    public int wavesToWin = 15;
    [Tooltip("Multiplies WaveManager's health curve for this map.")]
    public float difficultyMultiplier = 1f;

    [Header("Enemies")]
    public List<EnemyData> enemyPool = new List<EnemyData>();
    public List<EnemyData> bossPool = new List<EnemyData>();
    public int bossEveryNWaves = 5;

    [Header("Theme")]
    public Color groundColor = new Color(0.36f, 0.55f, 0.29f);
    public Color pathColor = new Color(0.76f, 0.64f, 0.42f);
    public Color accentColor = new Color(0.25f, 0.42f, 0.2f);
}

/// <summary>A single enemy route through the level, spawn first, exit last.</summary>
[Serializable]
public class PathDefinition
{
    public List<Vector2> points = new List<Vector2>();
}
