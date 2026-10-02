using UnityEngine;

namespace TowerDefense.Bosses
{
    /// <summary>
    /// Runtime half of SplitOnDeathAbility. Listens to Enemy.Died, which fires
    /// before the owner despawns, so its position/path/waypoint are still valid.
    /// </summary>
    public class SplitOnDeathAbilityBehaviour : EnemyAbilityBehaviour
    {
        private SplitOnDeathAbility config;
        private Enemy subscribed;

        public override void Bind(Enemy owner, EnemyAbilityData data)
        {
            base.Bind(owner, data);
            config = data as SplitOnDeathAbility;
            Unsubscribe();
            if (owner != null)
            {
                owner.Died += OnOwnerDied;
                subscribed = owner;
            }
        }

        private void OnOwnerDied(Enemy dead)
        {
            Unsubscribe(); // split exactly once
            if (config == null || config.child == null || dead == null) return;

            var path = dead.Path;
            int waypoint = dead.WaypointIndex;
            if (!AbilitySpawner.CanSpawnFrom(path, waypoint)) return;

            AbilitySpawner.WaveScaledStats(WaveManager.Instance, config.child, out _, out float speed, out int reward);
            float health = dead.maxHealth * config.childHealthFraction;
            reward = Mathf.RoundToInt(reward * config.rewardScale);

            Vector3 origin = dead.transform.position;
            int spawned = 0;
            for (int i = 0; i < config.count; i++)
            {
                Vector3 pos = origin + AbilitySpawner.RingOffset(i, config.count, config.spawnRadius);
                if (AbilitySpawner.Spawn(config.child, path, waypoint, pos, health, speed, reward) != null) spawned++;
            }
            if (spawned > 0) RaiseTriggered();
        }

        private void Unsubscribe()
        {
            if (subscribed != null) subscribed.Died -= OnOwnerDied;
            subscribed = null;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
