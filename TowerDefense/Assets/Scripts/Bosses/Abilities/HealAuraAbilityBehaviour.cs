using UnityEngine;

namespace TowerDefense.Bosses
{
    /// <summary>
    /// Runtime half of HealAuraAbility. One OverlapCircle per pulse into a
    /// shared buffer on the "Enemy" layer: no per-frame allocations.
    /// </summary>
    public class HealAuraAbilityBehaviour : EnemyAbilityBehaviour
    {
        // Shared by every healer: queries are synchronous, so one buffer is enough.
        private static readonly Collider2D[] Buffer = new Collider2D[48];
        private static bool filterReady;
        private static ContactFilter2D filter;

        private HealAuraAbility config;
        private float pulseLeft;

        public float Radius => config != null ? config.radius : 0f;
        /// <summary>Enemies healed by the most recent pulse (for VFX).</summary>
        public int LastPulseHealed { get; private set; }

        public override void Bind(Enemy owner, EnemyAbilityData data)
        {
            base.Bind(owner, data);
            config = data as HealAuraAbility;
            // First pulse after one interval: a freshly spawned shaman shouldn't heal instantly.
            pulseLeft = config != null ? config.interval : 0f;
            EnsureFilter();
        }

        private static void EnsureFilter()
        {
            if (filterReady) return;
            int mask = LayerMask.GetMask("Enemy");
            filter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = true,
                // Missing layer (misconfigured project): scan everything, the Enemy component check still filters.
                layerMask = mask != 0 ? mask : Physics2D.AllLayers,
            };
            filterReady = true;
        }

        private void Update()
        {
            if (config == null || !OwnerAlive) return;
            pulseLeft -= Time.deltaTime;
            if (pulseLeft > 0f) return;
            pulseLeft = config.interval;
            Pulse();
        }

        private void Pulse()
        {
            int healed = 0;
            int count = Physics2D.OverlapCircle(Owner.transform.position, config.radius, filter, Buffer);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = Buffer[i];
                Buffer[i] = null; // don't keep destroyed colliders alive in the static buffer
                if (hit == null || !hit.TryGetComponent(out Enemy target)) continue;
                if (target.IsDead) continue;
                if (target == Owner && !config.includeSelf) continue;
                if (target.IsBoss && !config.healBosses) continue;
                if (target.HealthPercent() >= 1f) continue;

                target.Heal(target.maxHealth * config.healPercent);
                healed++;
            }

            LastPulseHealed = healed;
            if (healed > 0) RaiseTriggered();
        }
    }
}
