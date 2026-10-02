using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The heart of the "never-ending" wave system. Generates waves procedurally
/// with a difficulty curve that scales indefinitely, pulling enemy identity
/// (armor/moveType/prefab) from EnemyData assets and applying the wave curve
/// on top. Tune the curves in the Inspector - no code changes needed to
/// rebalance. Supports several paths per map (enemies alternate between them)
/// and an optional win condition (wavesToWin) for campaign levels.
/// </summary>
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Path & Spawn")]
    [Tooltip("Scene-authored single path. Ignored once SetPaths/ApplyLevel provides world-space paths.")]
    public List<Transform> pathWaypoints;
    public Transform spawnPoint;

    [Header("Enemy Pool (data-driven)")]
    [Tooltip("All enemy types available across the whole game. Each asset's unlockWave field gates when it starts appearing.")]
    public List<EnemyData> enemyPool;

    [Header("Boss Pool")]
    [Tooltip("EnemyData assets with isBoss = true. One is picked every bossEveryNWaves.")]
    public List<EnemyData> bossPool;
    public int bossEveryNWaves = 10;

    [Header("Base Difficulty Curve")]
    public float baseHealth = 10f;
    public float healthGrowthPerWave = 1.12f;  // multiplicative - compounds every wave
    public float baseSpeed = 2f;
    public float speedGrowthPerWave = 1.01f;
    public int baseGoldReward = 5;
    [Tooltip("Per-level scale on the health curve (LevelData.difficultyMultiplier).")]
    public float difficultyMultiplier = 1f;

    [Header("Wave Composition")]
    public int baseEnemyCount = 6;
    public int enemyCountGrowthPerWave = 1;
    public float spawnInterval = 0.6f;
    public float delayBetweenWaves = 5f;

    [Header("Flow")]
    [Tooltip("Start the endless loop in Start(). Turn off when a level loader calls StartWaves().")]
    public bool autoStart = true;
    [Tooltip("Waves to clear for victory. 0 = endless.")]
    public int wavesToWin = 0;
    [Tooltip("Gold per second of countdown skipped when the player calls the next wave early.")]
    public float earlyCallGoldPerSecond = 2f;

    public int CurrentWave { get; private set; } = 0;
    public bool WaveInProgress { get; private set; } = false;
    public bool IsRunning { get; private set; } = false;
    public int EnemiesAlive => enemiesAlive;
    /// <summary>Seconds until the next wave auto-starts; 0 while a wave is active.</summary>
    public float TimeUntilNextWave { get; private set; }
    public bool IsCountingDown => IsRunning && !WaveInProgress && enemiesAlive == 0 && TimeUntilNextWave > 0f;

    /// <summary>Fired when a wave begins spawning: (wave number, is boss wave).</summary>
    public event Action<int, bool> WaveSpawning;
    /// <summary>Fired for every enemy spawned, including summons.</summary>
    public event Action<Enemy> EnemySpawned;

    private readonly List<IReadOnlyList<Vector3>> paths = new List<IReadOnlyList<Vector3>>();
    private int nextPathIndex;
    private int enemiesAlive = 0;
    private bool skipCountdown;
    private Coroutine loop;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (autoStart) StartWaves();
    }

    /// <summary>Use world-space paths (spawn first, exit last). Enemies alternate between them.</summary>
    public void SetPaths(IEnumerable<IReadOnlyList<Vector3>> worldPaths)
    {
        paths.Clear();
        nextPathIndex = 0;
        if (worldPaths == null) return;
        foreach (var p in worldPaths)
            if (p != null && p.Count >= 2) paths.Add(p);
    }

    /// <summary>Configure paths, enemy pools and rules from a LevelData asset.</summary>
    public void ApplyLevel(LevelData level)
    {
        if (level == null) return;
        var worldPaths = new List<IReadOnlyList<Vector3>>();
        foreach (var def in level.paths)
        {
            if (def == null || def.points == null) continue;
            var pts = new List<Vector3>(def.points.Count);
            foreach (var p in def.points) pts.Add(new Vector3(p.x, p.y, 0f));
            worldPaths.Add(pts);
        }
        SetPaths(worldPaths);
        if (level.enemyPool != null && level.enemyPool.Count > 0) enemyPool = new List<EnemyData>(level.enemyPool);
        if (level.bossPool != null) bossPool = new List<EnemyData>(level.bossPool);
        bossEveryNWaves = level.bossEveryNWaves;
        wavesToWin = level.wavesToWin;
        difficultyMultiplier = level.difficultyMultiplier;
    }

    public IReadOnlyList<IReadOnlyList<Vector3>> Paths => paths;

    public void StartWaves()
    {
        if (IsRunning) return;
        IsRunning = true;
        loop = StartCoroutine(RunForever());
    }

    public void StopWaves()
    {
        if (loop != null) StopCoroutine(loop);
        loop = null;
        IsRunning = false;
    }

    /// <summary>
    /// Skip the between-wave countdown (KR "call early"). Returns bonus gold
    /// awarded for the time skipped, 0 if no countdown was running.
    /// </summary>
    public int CallNextWaveEarly()
    {
        if (!IsCountingDown) return 0;
        int bonus = Mathf.RoundToInt(TimeUntilNextWave * earlyCallGoldPerSecond);
        if (bonus > 0 && GameManager.Instance != null) GameManager.Instance.AddGold(bonus);
        skipCountdown = true;
        return bonus;
    }

    public bool IsBossWave(int waveNumber) =>
        bossPool != null && bossPool.Count > 0 && bossEveryNWaves > 0 && waveNumber % bossEveryNWaves == 0;

    private IEnumerator RunForever()
    {
        // Initial grace period before wave 1 so the player can build.
        yield return Countdown(delayBetweenWaves);

        // This loop never ends in endless mode - difficulty keeps compounding via the curves above.
        while (true)
        {
            CurrentWave++;
            yield return StartCoroutine(SpawnWave(CurrentWave));

            while (enemiesAlive > 0)
                yield return null;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnWaveCleared(CurrentWave);
                if (wavesToWin > 0 && CurrentWave >= wavesToWin)
                {
                    GameManager.Instance.TriggerVictory();
                    IsRunning = false;
                    yield break;
                }
            }

            yield return Countdown(delayBetweenWaves);
        }
    }

    private IEnumerator Countdown(float seconds)
    {
        TimeUntilNextWave = seconds;
        skipCountdown = false;
        while (TimeUntilNextWave > 0f && !skipCountdown)
        {
            TimeUntilNextWave -= Time.deltaTime;
            yield return null;
        }
        TimeUntilNextWave = 0f;
        skipCountdown = false;
    }

    private IEnumerator SpawnWave(int waveNumber)
    {
        WaveInProgress = true;
        bool isBossWave = IsBossWave(waveNumber);
        if (GameManager.Instance != null) GameManager.Instance.OnWaveStarted(waveNumber);
        WaveSpawning?.Invoke(waveNumber, isBossWave);

        float health = baseHealth * difficultyMultiplier * Mathf.Pow(healthGrowthPerWave, waveNumber - 1);
        float speed = baseSpeed * Mathf.Pow(speedGrowthPerWave, waveNumber - 1);
        int reward = Mathf.RoundToInt(baseGoldReward * (1f + 0.05f * (waveNumber - 1)));

        if (isBossWave)
        {
            EnemyData boss = bossPool[UnityEngine.Random.Range(0, bossPool.Count)];
            SpawnEnemy(boss, health * boss.bossHealthMultiplier, speed * 0.6f, reward * boss.bossGoldMultiplier);
            WaveInProgress = false;
            yield break;
        }

        int count = baseEnemyCount + enemyCountGrowthPerWave * (waveNumber - 1);
        for (int i = 0; i < count; i++)
        {
            EnemyData enemyData = PickEnemyData(waveNumber);
            if (enemyData == null) continue;

            // Per-type base stats scale the wave curve, so a "tank" stays tankier than a "runner".
            float typeHealth = health * (enemyData.baseHealth / 10f);
            float typeSpeed = speed * (enemyData.baseSpeed / 2f);
            int typeReward = Mathf.Max(1, Mathf.RoundToInt(reward * (enemyData.baseGoldReward / 5f)));
            SpawnEnemy(enemyData, typeHealth, typeSpeed, typeReward);
            yield return new WaitForSeconds(spawnInterval);
        }

        WaveInProgress = false;
    }

    private EnemyData PickEnemyData(int waveNumber)
    {
        if (enemyPool == null || enemyPool.Count == 0) return null;
        List<EnemyData> available = new List<EnemyData>();
        foreach (var data in enemyPool)
        {
            if (data != null && waveNumber >= data.unlockWave)
                available.Add(data);
        }
        if (available.Count == 0) return enemyPool[0];
        return available[UnityEngine.Random.Range(0, available.Count)];
    }

    private IReadOnlyList<Vector3> NextPath()
    {
        if (paths.Count > 0)
        {
            var p = paths[nextPathIndex % paths.Count];
            nextPathIndex++;
            return p;
        }
        var fallback = new List<Vector3>();
        if (pathWaypoints != null)
            foreach (var t in pathWaypoints) if (t != null) fallback.Add(t.position);
        return fallback;
    }

    private Enemy SpawnEnemy(EnemyData enemyData, float health, float speed, int reward)
    {
        IReadOnlyList<Vector3> path = NextPath();
        Vector3 origin = path.Count > 0 ? path[0] : (spawnPoint != null ? spawnPoint.position : transform.position);
        return SpawnInternal(enemyData, path, health, speed, reward, 0, origin);
    }

    /// <summary>
    /// Spawn an extra enemy mid-wave (boss summons, splitters). Counts toward
    /// the wave so it must die or leak before the wave clears.
    /// </summary>
    public Enemy SpawnExtra(EnemyData enemyData, IReadOnlyList<Vector3> path, int startWaypointIndex, Vector3 position,
                            float health, float speed, int reward)
    {
        return SpawnInternal(enemyData, path, health, speed, reward, startWaypointIndex, position);
    }

    private Enemy SpawnInternal(EnemyData enemyData, IReadOnlyList<Vector3> path, float health, float speed, int reward,
                                int startWaypointIndex, Vector3 position)
    {
        if (enemyData == null || enemyData.prefab == null)
        {
            Debug.LogError($"EnemyData '{(enemyData != null ? enemyData.enemyName : "null")}' has no prefab.");
            return null;
        }

        GameObject go = Instantiate(enemyData.prefab, position, Quaternion.identity);
        Enemy e = go.GetComponent<Enemy>();
        if (e == null)
        {
            Debug.LogError($"EnemyData '{enemyData.enemyName}' prefab has no Enemy.cs attached.");
            Destroy(go);
            return null;
        }

        // Count before Init: abilities attached during Init may spawn summons immediately.
        enemiesAlive++;
        e.Init(enemyData, path, health, speed, reward, startWaypointIndex, position);
        EnemySpawned?.Invoke(e);
        return e;
    }

    // Called by GameManager on enemy death or leak
    public void NotifyEnemyGone()
    {
        enemiesAlive = Mathf.Max(0, enemiesAlive - 1);
    }
}
