using UnityEngine;

namespace TowerDefense.Bosses
{
    /// <summary>
    /// Heals a fraction of max health per second once the enemy has gone
    /// regenDelay seconds without taking damage. Any damage (including each
    /// poison tick) resets the delay: poison is the hard counter.
    /// </summary>
    [CreateAssetMenu(fileName = "Regenerate", menuName = "Tower Defense/Abilities/Regenerate")]
    public class RegenerateAbility : EnemyAbilityData
    {
        [Tooltip("Fraction of max health restored per second while regenerating.")]
        [Range(0f, 1f)] public float healPercentPerSecond = 0.04f;
        [Tooltip("Seconds without damage before regeneration starts.")]
        [Min(0f)] public float regenDelay = 2f;

        public override EnemyAbilityBehaviour Attach(Enemy enemy) => AttachBehaviour<RegenerateAbilityBehaviour>(enemy);
    }
}
