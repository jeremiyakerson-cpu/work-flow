using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The heart of the "never-ending" wave system. Generates waves procedurally
/// with a difficulty curve that scales indefinitely, pulling enemy identity
/// (armor/moveType/prefab) from EnemyData assets and applying the wave curve
/// on top. Tune the curves in the Inspector - no code changes needed to
/// rebalance.
/// </summary>
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Path & Spawn")]
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

    [Header("Wave Composition")]
    public int baseEnemyCount = 6;
    public int enemyCountGrowthPerWave = 1;
    public float spawnInterval = 0.6f;
    public float delayBetweenWaves = 5f;

    public int CurrentWave { get; private set; } = 0;
    public bool WaveInProgress { get; private set; } = false;
    private int enemiesAlive = 0;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(RunForever());
    }

    private IEnumerator RunForever()
    {
        // This loop never ends - difficulty keeps compounding via the curves above.
        while (true)
        {
            CurrentWave++;
            yield return StartCoroutine(SpawnWave(CurrentWave));

            while (enemiesAlive > 0)
                yield return null;

            GameManager.Instance.OnWaveCleared(CurrentWave);
            yield return new WaitForSeconds(delayBetweenWaves);
        }
    }

    private IEnumerator SpawnWave(int waveNumber)
    {
        WaveInProgress = true;
        GameManager.Instance.OnWaveStarted(waveNumber);

        bool isBossWave = bossPool.Count > 0 && bossEveryNWaves > 0 && waveNumber % bossEveryNWaves == 0;

        float health = baseHealth * Mathf.Pow(healthGrowthPerWave, waveNumber - 1);
        float speed = baseSpeed * Mathf.Pow(speedGrowthPerWave, waveNumber - 1);
        int reward = Mathf.RoundToInt(baseGoldReward * (1f + 0.05f * (waveNumber - 1)));

        if (isBossWave)
        {
            EnemyData boss = bossPool[Random.Range(0, bossPool.Count)];
            SpawnEnemy(boss, health * boss.bossHealthMultiplier, speed * 0.6f, reward * boss.bossGoldMultiplier);
            enemiesAlive++;
            WaveInProgress = false;
            yield break;
        }

        int count = baseEnemyCount + enemyCountGrowthPerWave * (waveNumber - 1);
        for (int i = 0; i < count; i++)
        {
            EnemyData enemyData = PickEnemyData(waveNumber);
            if (enemyData == null) continue;

            SpawnEnemy(enemyData, health, speed, reward);
            enemiesAlive++;
            yield return new WaitForSeconds(spawnInterval);
        }

        WaveInProgress = false;
    }

    private EnemyData PickEnemyData(int waveNumber)
    {
        List<EnemyData> available = new List<EnemyData>();
        foreach (var data in enemyPool)
        {
            if (waveNumber >= data.unlockWave)
                available.Add(data);
        }
        if (available.Count == 0) return enemyPool.Count > 0 ? enemyPool[0] : null;
        return available[Random.Range(0, available.Count)];
    }

    private void SpawnEnemy(EnemyData enemyData, float health, float speed, int reward)
    {
        GameObject go = Instantiate(enemyData.prefab, spawnPoint.position, Quaternion.identity);
        Enemy e = go.GetComponent<Enemy>();
        if (e == null)
        {
            Debug.LogError($"EnemyData '{enemyData.enemyName}' prefab has no Enemy.cs attached.");
            return;
        }
        e.Init(enemyData, pathWaypoints, health, speed, reward);
    }

    // Called by Enemy.cs on death or leak
    public void NotifyEnemyGone()
    {
        enemiesAlive = Mathf.Max(0, enemiesAlive - 1);
    }
}
