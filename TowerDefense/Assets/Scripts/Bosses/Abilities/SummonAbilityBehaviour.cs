using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Bosses
{
    /// <summary>Runtime half of SummonAbility. Tracks its own minions to honour maxAlive.</summary>
    public class SummonAbilityBehaviour : EnemyAbilityBehaviour
    {
        private SummonAbility config;
        private float cooldownLeft;
        // Allocated once per spawn of the summoner, pruned in place.
        private readonly List<Enemy> minions = new List<Enemy>(12);

        public int MinionsAlive
        {
            get
            {
                PruneDead();
                return minions.Count;
            }
        }

        public override void Bind(Enemy owner, EnemyAbilityData data)
        {
            base.Bind(owner, data);
            config = data as SummonAbility;
            minions.Clear();
            cooldownLeft = config != null ? config.initialDelay : 0f;
        }

        private void Update()
        {
            if (config == null || config.minion == null || !OwnerAlive) return;
            cooldownLeft -= Time.deltaTime;
            if (cooldownLeft > 0f) return;

            cooldownLeft = config.cooldown;
            Cast();
        }

        private void Cast()
        {
            var path = Owner.Path;
            int waypoint = Owner.WaypointIndex;
            if (!AbilitySpawner.CanSpawnFrom(path, waypoint)) return;

            PruneDead();
            int room = config.maxAlive - minions.Count;
            int count = Mathf.Min(config.countPerCast, room);
            if (count <= 0) return;

            AbilitySpawner.WaveScaledStats(WaveManager.Instance, config.minion, out float health, out float speed, out int reward);
            health *= config.healthScale;
            reward = Mathf.RoundToInt(reward * config.rewardScale);

            Vector3 origin = Owner.transform.position;
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                // A spawned minion's own abilities could in theory kill/despawn the owner; stop if so.
                if (!OwnerAlive) break;
                Vector3 pos = origin + AbilitySpawner.RingOffset(i, count, config.spawnRadius);
                Enemy m = AbilitySpawner.Spawn(config.minion, path, waypoint, pos, health, speed, reward);
                if (m == null) continue;
                minions.Add(m);
                spawned++;
            }
            if (spawned > 0) RaiseTriggered();
        }

        private void PruneDead()
        {
            // Unity's == null is true for destroyed enemies; IsDead covers pooled ones.
            for (int i = minions.Count - 1; i >= 0; i--)
                if (minions[i] == null || minions[i].IsDead) minions.RemoveAt(i);
        }
    }
}
