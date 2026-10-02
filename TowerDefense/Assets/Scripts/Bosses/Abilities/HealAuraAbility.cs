using UnityEngine;

namespace TowerDefense.Bosses
{
    /// <summary>
    /// Every interval, heals nearby living enemies by a fraction of their own
    /// max health (so it scales with the wave curve). The shaman's ability.
    /// </summary>
    [CreateAssetMenu(fileName = "HealAura", menuName = "Tower Defense/Abilities/Heal Aura")]
    public class HealAuraAbility : EnemyAbilityData
    {
        [Min(0.1f)] public float radius = 2.5f;
        [Min(0.1f)] public float interval = 2.5f;
        [Tooltip("Heal per pulse, as a fraction of each target's max health.")]
        [Range(0f, 1f)] public float healPercent = 0.08f;
        public bool includeSelf = false;
        [Tooltip("Bosses have huge pools: healing them by percent is usually too strong.")]
        public bool healBosses = false;

        public override EnemyAbilityBehaviour Attach(Enemy enemy) => AttachBehaviour<HealAuraAbilityBehaviour>(enemy);
    }
}
