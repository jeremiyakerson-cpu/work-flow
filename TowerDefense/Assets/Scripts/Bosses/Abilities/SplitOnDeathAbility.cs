using UnityEngine;

namespace TowerDefense.Bosses
{
    /// <summary>
    /// When killed (not when leaking), the enemy bursts into smaller enemies
    /// at its death position, continuing from its waypoint. Children's health
    /// is a fraction of the parent's max health, so it tracks the wave curve.
    /// </summary>
    [CreateAssetMenu(fileName = "SplitOnDeath", menuName = "Tower Defense/Abilities/Split On Death")]
    public class SplitOnDeathAbility : EnemyAbilityData
    {
        [Tooltip("Enemy type spawned on death (needs a prefab/template like any enemy).")]
        public EnemyData child;
        [Min(1)] public int count = 3;
        [Tooltip("Each child's health as a fraction of the parent's max health.")]
        [Range(0.01f, 1f)] public float childHealthFraction = 0.3f;
        [Tooltip("Scales the child's wave-curve gold.")]
        [Min(0f)] public float rewardScale = 0.5f;
        [Min(0f)] public float spawnRadius = 0.5f;

        public override EnemyAbilityBehaviour Attach(Enemy enemy) => AttachBehaviour<SplitOnDeathAbilityBehaviour>(enemy);
    }
}
