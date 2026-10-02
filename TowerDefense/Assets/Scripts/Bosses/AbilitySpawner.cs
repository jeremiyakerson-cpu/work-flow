using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Bosses
{
    /// <summary>
    /// Mid-path spawning for summon and split abilities. Spawns go through
    /// WaveManager.SpawnExtra so they count toward the current wave, start at
    /// the source enemy's waypoint, and get prefab/abilities like any enemy.
    /// </summary>
    public static class AbilitySpawner
    {
        /// <summary>
        /// Health/speed/reward an enemy type would get in the current wave,
        /// mirroring WaveManager's curve (per-type base stats scale the curve).
        /// </summary>
        public static void WaveScaledStats(WaveManager wm, EnemyData data, out float health, out float speed, out int reward)
        {
            int wave = Mathf.Max(1, wm.CurrentWave);
            float curveHealth = wm.baseHealth * wm.difficultyMultiplier * Mathf.Pow(wm.healthGrowthPerWave, wave - 1);
            float curveSpeed = wm.baseSpeed * Mathf.Pow(wm.speedGrowthPerWave, wave - 1);
            float curveReward = wm.baseGoldReward * (1f + 0.05f * (wave - 1));
            health = curveHealth * (data.baseHealth / 10f);
            speed = curveSpeed * (data.baseSpeed / 2f);
            reward = Mathf.Max(0, Mathf.RoundToInt(curveReward * (data.baseGoldReward / 5f)));
        }

        /// <summary>True if a spawn can be placed on this source's path right now.</summary>
        public static bool CanSpawnFrom(IReadOnlyList<Vector3> path, int waypointIndex)
        {
            return WaveManager.Instance != null && path != null && path.Count > 0 && waypointIndex < path.Count;
        }

        /// <summary>
        /// Spawn one enemy at position on path, heading for waypointIndex.
        /// Returns null if there is no WaveManager or the data cannot spawn.
        /// </summary>
        public static Enemy Spawn(EnemyData data, IReadOnlyList<Vector3> path, int waypointIndex, Vector3 position,
                                  float health, float speed, int reward)
        {
            WaveManager wm = WaveManager.Instance;
            if (wm == null || data == null || !CanSpawnFrom(path, waypointIndex)) return null;
            return wm.SpawnExtra(data, path, Mathf.Max(0, waypointIndex), position,
                                 Mathf.Max(1f, health), Mathf.Max(0.1f, speed), Mathf.Max(0, reward));
        }

        /// <summary>Offset for the i-th of count spawns on a small ring, so a group doesn't stack on one pixel.</summary>
        public static Vector3 RingOffset(int i, int count, float radius)
        {
            if (count <= 1 || radius <= 0f) return Vector3.zero;
            float angle = i * (2f * Mathf.PI / count);
            return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }
    }
}
