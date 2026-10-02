using UnityEngine;

namespace TowerDefense.Bosses
{
    /// <summary>
    /// Periodically spawns minions at the summoner's position on its path.
    /// Minions count toward the wave (WaveManager.SpawnExtra) and get the
    /// current wave's stats for their type, times healthScale.
    /// </summary>
    [CreateAssetMenu(fileName = "Summon", menuName = "Tower Defense/Abilities/Summon")]
    public class SummonAbility : EnemyAbilityData
    {
        [Tooltip("Enemy type to summon (needs a prefab/template like any enemy).")]
        public EnemyData minion;
        [Min(1)] public int countPerCast = 3;
        [Min(0f)] public float initialDelay = 4f;
        [Min(0.5f)] public float cooldown = 8f;
        [Tooltip("Scales the minion's wave-curve health.")]
        [Min(0.05f)] public float healthScale = 1f;
        [Tooltip("Scales the minion's gold. Keep low so summoners can't be farmed.")]
        [Min(0f)] public float rewardScale = 0.5f;
        [Tooltip("Stop casting while this many of this summoner's minions are alive.")]
        [Min(1)] public int maxAlive = 9;
        [Tooltip("Minions appear on a ring of this radius around the summoner.")]
        [Min(0f)] public float spawnRadius = 0.45f;

        public override EnemyAbilityBehaviour Attach(Enemy enemy) => AttachBehaviour<SummonAbilityBehaviour>(enemy);
    }
}
