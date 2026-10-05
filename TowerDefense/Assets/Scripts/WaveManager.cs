using System;
using System.Collections;
using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Pooling;
using UnityEngine;

/// <summary>
/// The heart of the "never-ending" wave system. Each wave is planned by the
/// engine-free <see cref="WavePlanner"/> (seeded, deterministic) from the curve
/// fields below, then this component executes the plan: spawning pooled enemies
/// on time and on the right path, tracking who is alive, and running the
/// between-wave countdown. Enemy identity (armor/moveType/prefab) comes from
/// EnemyData assets; the curve scales their stats. Tune the curves in the
/// Inspector - no code changes needed to rebalance. Supports several paths per
/// map and an optional win condition (wavesToWin) for campaign levels.
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

    [Header("Curve Limits & Extras")]
    [Tooltip("Kill reward grows by this fraction of the base per wave (0.05 = +5%/wave).")]
    public float rewardGrowthPerWave = 0.05f;
    [Tooltip("Cap on the compounded speed multiplier (0 = uncapped). Keeps endless waves catchable by projectiles.")]
    public float maxSpeedScale = 3f;
    [Tooltip("Cap on enemies per regular wave (0 = uncapped). Health keeps compounding past the cap.")]
    public int maxEnemiesPerWave = 0;
    [Tooltip("Boss speed relative to the wave's speed curve.")]
    public float bossSpeedMultiplier = 0.6f;
    [Tooltip("Regular enemies escorting a boss, as a fraction of a normal wave (0 = boss alone).")]
    [Range(0f, 2f)] public float bossEscortFraction = 0f;
    [Tooltip("An enemy type with these base stats gets exactly the curve's values; others scale relative to it.")]
    public float referenceHealth = 10f;
    public float referenceSpeed = 2f;
    public int referenceReward = 5;

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

    [Header("Randomness & Pooling")]
    [Tooltip("Wave composition seed. 0 = a new random seed every run; any other value replays the same waves.")]
    public int seed = 0;
    [Tooltip("Inactive enemies created per enemy type when a level is applied, to avoid hitches on wave 1.")]
    public int prewarmPerType = 6;

    /// <summary>
    /// Difficulty applied on top of the curve above (ApplyLevel / SetDifficulty).
    /// Health, speed, rewards and boss health all scale through the curve, so plans,
    /// previews and summons (ScaledStats) agree. Normal changes nothing.
    /// </summary>
    public DifficultyMode Difficulty { get; private set; } = DifficultyMode.Normal;
    /// <summary>The rules for <see cref="Difficulty"/>.</summary>
    public DifficultyRules DifficultyRules => TowerDefense.Core.Difficulty.Rules(Difficulty);
    /// <summary>False on modes without the early-call bonus (Impossible): calling early still works but pays 0.</summary>
    public bool EarlyCallBonusEnabled => DifficultyRules.EarlyCallBonusEnabled;
    /// <summary>Gold CallNextWaveEarly would pay right now (0 when not counting down or the bonus is disabled).</summary>
    public int EarlyCallBonusPreview =>
        IsCountingDown && !skipCountdown ? DifficultyRules.EarlyCallBonus(TimeUntilNextWave, earlyCallGoldPerSecond) : 0;

    public int CurrentWave { get; private set; } = 0;
    public bool WaveInProgress { get; private set; } = false;
    public bool IsRunning { get; private set; } = false;
    public int EnemiesAlive => activeEnemies.Count;
    /// <summary>Every living enemy spawned by this manager (including summons). Don't modify.</summary>
    public IReadOnlyList<Enemy> ActiveEnemies => activeEnemies;
    /// <summary>Seconds until the next wave auto-starts; 0 while a wave is active.</summary>
    public float TimeUntilNextWave { get; private set; }
    public bool IsCountingDown => IsRunning && !WaveInProgress && activeEnemies.Count == 0 && TimeUntilNextWave > 0f;
    /// <summary>The seed actually used for wave plans this run (resolved from `seed`).</summary>
    public int ActiveSeed
    {
        get
        {
            if (activeSeed == 0) activeSeed = seed != 0 ? seed : NewRandomSeed();
            return activeSeed;
        }
    }
    /// <summary>The plan of the wave currently (or most recently) spawning. Null before wave 1.</summary>
    public WavePlan CurrentPlan { get; private set; }

    /// <summary>Fired when a wave begins spawning: (wave number, is boss wave).</summary>
    public event Action<int, bool> WaveSpawning;
    /// <summary>Fired for every enemy spawned, including summons.</summary>
    public event Action<Enemy> EnemySpawned;

    private readonly List<IReadOnlyList<Vector3>> paths = new List<IReadOnlyList<Vector3>>();
    private readonly List<Vector3> fallbackPath = new List<Vector3>();
    private readonly List<Enemy> activeEnemies = new List<Enemy>();
    private readonly List<EnemyTypeInfo> enemyTypes = new List<EnemyTypeInfo>();
    private readonly List<EnemyTypeInfo> bossTypes = new List<EnemyTypeInfo>();
    private readonly WaveCurve curve = new WaveCurve();
    private bool skipCountdown;
    private int activeSeed;
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

    // Unity stops coroutines on disable; keep IsRunning truthful so StartWaves works again.
    private void OnDisable()
    {
        StopWaves();
    }

    /// <summary>Use world-space paths (spawn first, exit last). Enemies are spread across them.</summary>
    public void SetPaths(IEnumerable<IReadOnlyList<Vector3>> worldPaths)
    {
        paths.Clear();
        if (worldPaths == null) return;
        foreach (var p in worldPaths)
            if (p != null && p.Count >= 2) paths.Add(p);
    }

    /// <summary>Configure paths, enemy pools and rules from a LevelData asset, on Normal difficulty.</summary>
    public void ApplyLevel(LevelData level) => ApplyLevel(level, DifficultyMode.Normal);

    /// <summary>Configure paths, enemy pools and rules from a LevelData asset on a difficulty.</summary>
    public void ApplyLevel(LevelData level, DifficultyMode mode)
    {
        if (level == null) return;
        SetDifficulty(mode);
        var worldPaths = new List<IReadOnlyList<Vector3>>();
        if (level.paths != null)
        {
            foreach (var def in level.paths)
            {
                if (def == null || def.points == null) continue;
                var pts = new List<Vector3>(def.points.Count);
                foreach (var p in def.points) pts.Add(new Vector3(p.x, p.y, 0f));
                worldPaths.Add(pts);
            }
        }
        SetPaths(worldPaths);
        if (level.enemyPool != null && level.enemyPool.Count > 0) enemyPool = new List<EnemyData>(level.enemyPool);
        if (level.bossPool != null) bossPool = new List<EnemyData>(level.bossPool);
        bossEveryNWaves = level.bossEveryNWaves;
        wavesToWin = level.wavesToWin;
        difficultyMultiplier = level.difficultyMultiplier;
        PrewarmPools(prewarmPerType);
    }

    /// <summary>
    /// Change the difficulty (normally via ApplyLevel before waves start). Takes effect
    /// from the next planned wave; enemies already on the field keep their stats.
    /// </summary>
    public void SetDifficulty(DifficultyMode mode)
    {
        Difficulty = TowerDefense.Core.Difficulty.IsDefined(mode) ? mode : DifficultyMode.Normal;
    }

    public IReadOnlyList<IReadOnlyList<Vector3>> Paths => paths;

    public void StartWaves()
    {
        if (IsRunning) return;
        if (!isActiveAndEnabled)
        {
            Debug.LogWarning("WaveManager.StartWaves called while the WaveManager is inactive.");
            return;
        }
        IsRunning = true;
        loop = StartCoroutine(RunForever());
    }

    public void StopWaves()
    {
        if (loop != null) StopCoroutine(loop);
        loop = null;
        IsRunning = false;
        WaveInProgress = false;
        skipCountdown = false;
    }

    /// <summary>
    /// Back to "before wave 1" for a replay without reloading the scene: stops
    /// the loop, removes every living enemy (no rewards/leaks) and picks a new
    /// seed if `seed` is 0. Call StartWaves afterwards.
    /// </summary>
    public void ResetWaves()
    {
        StopWaves();
        ClearEnemies();
        CurrentWave = 0;
        CurrentPlan = null;
        TimeUntilNextWave = 0f;
        activeSeed = 0;
    }

    /// <summary>Use a specific seed from now on (daily challenge, replays). 0 = random.</summary>
    public void SetSeed(int newSeed)
    {
        seed = newSeed;
        activeSeed = 0;
    }

    /// <summary>Remove every living enemy without rewards or leaks.</summary>
    public void ClearEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (i >= activeEnemies.Count) continue; // a despawn can remove more than one entry
            Enemy e = activeEnemies[i];
            if (e != null) e.DespawnSilently();
        }
        activeEnemies.Clear();
    }

    /// <summary>
    /// Skip the between-wave countdown (KR "call early"). Returns bonus gold
    /// awarded for the time skipped, 0 if no countdown was running or the
    /// difficulty has no early-call bonus (the wave is still called early).
    /// </summary>
    public int CallNextWaveEarly()
    {
        if (!IsCountingDown || skipCountdown) return 0;
        int bonus = DifficultyRules.EarlyCallBonus(TimeUntilNextWave, earlyCallGoldPerSecond);
        if (bonus > 0 && GameManager.Instance != null) GameManager.Instance.AddGold(bonus);
        skipCountdown = true;
        return bonus;
    }

    public bool IsBossWave(int waveNumber) =>
        WavePlanner.IsBossWave(waveNumber, bossEveryNWaves, CountSpawnable(bossPool));

    /// <summary>
    /// The deterministic plan for any wave number with the current settings
    /// (same result the spawner will use). Handy for "next wave" previews.
    /// </summary>
    public WavePlan PlanWave(int waveNumber)
    {
        BuildCurve();
        BuildTypes(enemyPool, enemyTypes);
        BuildTypes(bossPool, bossTypes);
        DifficultyRules rules = DifficultyRules;
        if (!rules.IsIdentity)
            for (int i = 0; i < bossTypes.Count; i++) bossTypes[i] = rules.ApplyToBoss(bossTypes[i]);
        return WavePlanner.Plan(waveNumber, curve, enemyTypes, bossTypes, Mathf.Max(1, paths.Count), ActiveSeed);
    }

    /// <summary>Resolve a plan entry's EnemyData (null if the pools changed since planning).</summary>
    public EnemyData GetEnemyData(in WaveSpawn spawn)
    {
        List<EnemyData> pool = spawn.IsBoss ? bossPool : enemyPool;
        return pool != null && spawn.TypeIndex >= 0 && spawn.TypeIndex < pool.Count ? pool[spawn.TypeIndex] : null;
    }

    /// <summary>Create inactive enemies ahead of time for every type in both pools.</summary>
    public void PrewarmPools(int perType)
    {
        if (perType <= 0) return;
        if (enemyPool != null)
            foreach (var d in enemyPool) if (d != null && d.prefab != null) GameObjectPool.Prewarm(d.prefab, perType);
        if (bossPool != null)
            foreach (var d in bossPool) if (d != null && d.prefab != null) GameObjectPool.Prewarm(d.prefab, 1);
    }

    private bool GameEnded => GameManager.Instance != null && (GameManager.Instance.IsGameOver || GameManager.Instance.IsVictory);

    private IEnumerator RunForever()
    {
        // Initial grace period before wave 1 so the player can build.
        TimeUntilNextWave = delayBetweenWaves;
        skipCountdown = false;
        while (TimeUntilNextWave > 0f && !skipCountdown)
        {
            yield return null;
            if (GameEnded) { EndLoop(); yield break; }
            TimeUntilNextWave -= Time.deltaTime;
        }
        TimeUntilNextWave = 0f;
        skipCountdown = false;

        // This loop never ends in endless mode - difficulty keeps compounding via the curves above.
        // Everything runs inside this one coroutine so StopWaves really stops spawning too.
        while (true)
        {
            if (GameEnded) { EndLoop(); yield break; }

            CurrentWave++;
            WavePlan plan = PlanWave(CurrentWave);
            CurrentPlan = plan;
            WaveInProgress = true;
            if (GameManager.Instance != null) GameManager.Instance.OnWaveStarted(CurrentWave);
            WaveSpawning?.Invoke(CurrentWave, plan.IsBossWave);

            // Spawn on schedule. Overshoot carries into the next delay, so the
            // cadence stays exact at any frame rate or game speed.
            float wait = 0f;
            for (int i = 0; i < plan.Spawns.Count; i++)
            {
                WaveSpawn s = plan.Spawns[i];
                wait += s.Delay;
                while (wait > 0f)
                {
                    yield return null;
                    if (GameEnded) { EndLoop(); yield break; }
                    wait -= Time.deltaTime;
                }
                SpawnFromPlan(s);
            }
            WaveInProgress = false;

            while (activeEnemies.Count > 0)
            {
                yield return null;
                if (GameEnded) { EndLoop(); yield break; }
            }
            if (GameEnded) { EndLoop(); yield break; }

            if (GameManager.Instance != null) GameManager.Instance.OnWaveCleared(CurrentWave);
            if (wavesToWin > 0 && CurrentWave >= wavesToWin)
            {
                if (GameManager.Instance != null) GameManager.Instance.TriggerVictory();
                EndLoop();
                yield break;
            }

            TimeUntilNextWave = delayBetweenWaves;
            skipCountdown = false;
            while (TimeUntilNextWave > 0f && !skipCountdown)
            {
                yield return null;
                if (GameEnded) { EndLoop(); yield break; }
                TimeUntilNextWave -= Time.deltaTime;
            }
            TimeUntilNextWave = 0f;
            skipCountdown = false;
        }
    }

    private void EndLoop()
    {
        loop = null;
        IsRunning = false;
        WaveInProgress = false;
        TimeUntilNextWave = 0f;
    }

    private void SpawnFromPlan(in WaveSpawn s)
    {
        EnemyData data = GetEnemyData(s);
        if (data == null) return;
        IReadOnlyList<Vector3> path = PathFor(s.PathIndex);
        Vector3 origin = path.Count > 0 ? path[0] : (spawnPoint != null ? spawnPoint.position : transform.position);
        Enemy e = SpawnInternal(data, path, s.Health, s.Speed, s.Reward, 0, origin);
        if (e != null && s.IsBoss) e.IsBoss = true;
    }

    private IReadOnlyList<Vector3> PathFor(int index)
    {
        if (paths.Count > 0) return paths[Mathf.Abs(index) % paths.Count];
        fallbackPath.Clear();
        if (pathWaypoints != null)
            foreach (var t in pathWaypoints) if (t != null) fallbackPath.Add(t.position);
        // Enemies keep a reference to their path, so hand out a copy.
        return new List<Vector3>(fallbackPath);
    }

    /// <summary>
    /// Health/speed/reward a regular enemy of this type gets on a wave, from the
    /// same curve the wave planner uses. Summon/split abilities call this so their
    /// minions scale exactly like wave spawns.
    /// </summary>
    public void ScaledStats(EnemyData data, int waveNumber, out float health, out float speed, out int reward)
    {
        BuildCurve();
        int wave = Mathf.Max(1, waveNumber);
        var types = new[] { data != null ? data.ToTypeInfo() : EnemyTypeInfo.Unavailable };
        WaveSpawn s = WavePlanner.MakeSpawn(curve, types, 0, wave,
                                            WavePlanner.HealthAt(curve, wave), WavePlanner.SpeedAt(curve, wave), 0f, 0);
        health = s.Health;
        speed = s.Speed;
        reward = s.Reward;
    }

    private void BuildCurve()
    {
        curve.BaseHealth = baseHealth;
        curve.HealthGrowthPerWave = healthGrowthPerWave;
        curve.BaseSpeed = baseSpeed;
        curve.SpeedGrowthPerWave = speedGrowthPerWave;
        curve.MaxSpeedScale = maxSpeedScale;
        curve.BaseGoldReward = baseGoldReward;
        curve.RewardGrowthPerWave = rewardGrowthPerWave;
        curve.DifficultyMultiplier = difficultyMultiplier;
        curve.BaseEnemyCount = baseEnemyCount;
        curve.EnemyCountGrowthPerWave = enemyCountGrowthPerWave;
        curve.MaxEnemiesPerWave = maxEnemiesPerWave;
        curve.SpawnInterval = spawnInterval;
        curve.BossEveryNWaves = bossEveryNWaves;
        curve.BossSpeedMultiplier = bossSpeedMultiplier;
        curve.BossEscortFraction = bossEscortFraction;
        curve.ReferenceHealth = referenceHealth;
        curve.ReferenceSpeed = referenceSpeed;
        curve.ReferenceReward = referenceReward;
        DifficultyRules.ApplyTo(curve); // health / speed / reward multipliers; no-op on Normal
    }

    private static void BuildTypes(List<EnemyData> pool, List<EnemyTypeInfo> into)
    {
        into.Clear();
        if (pool == null) return;
        foreach (var d in pool) into.Add(d != null && d.prefab != null ? d.ToTypeInfo() : EnemyTypeInfo.Unavailable);
    }

    // Same rule the planner applies (BuildTypes): entries need data and a prefab.
    private static int CountSpawnable(List<EnemyData> pool)
    {
        if (pool == null) return 0;
        int n = 0;
        foreach (var d in pool) if (d != null && d.prefab != null && d.spawnWeight > 0f) n++;
        return n;
    }

    private static int NewRandomSeed()
    {
        int s = unchecked((int)DateTime.UtcNow.Ticks ^ Environment.TickCount);
        return s != 0 ? s : 1;
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

        GameObject go = GameObjectPool.Spawn(enemyData.prefab, position, Quaternion.identity);
        if (go == null) return null;
        Enemy e = go.GetComponent<Enemy>();
        if (e == null)
        {
            Debug.LogError($"EnemyData '{enemyData.enemyName}' prefab has no Enemy.cs attached.");
            GameObjectPool.Despawn(go);
            return null;
        }

        // Register before Init: abilities attached during Init may spawn summons
        // (or even kill this enemy) immediately.
        if (!activeEnemies.Contains(e)) activeEnemies.Add(e);
        e.Init(enemyData, path, health, speed, reward, startWaypointIndex, position);
        if (!e.IsDead) EnemySpawned?.Invoke(e);
        return e;
    }

    /// <summary>Called by Enemy when it dies, leaks or is removed. Safe to call more than once.</summary>
    public void UnregisterEnemy(Enemy e)
    {
        activeEnemies.Remove(e);
    }

    /// <summary>Obsolete: enemies now unregister themselves (UnregisterEnemy). Kept for old callers; does nothing.</summary>
    [Obsolete("Enemies unregister themselves; counting is automatic.")]
    public void NotifyEnemyGone() { }
}
