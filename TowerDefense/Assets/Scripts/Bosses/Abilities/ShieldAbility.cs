using UnityEngine;

namespace TowerDefense.Bosses
{
    /// <summary>
    /// On a cooldown, raises a shield that makes the enemy ignore all damage
    /// (Enemy.Invulnerable) for a short window. Slows/poison still apply,
    /// but poison ticks are absorbed while the shield is up.
    /// </summary>
    [CreateAssetMenu(fileName = "Shield", menuName = "Tower Defense/Abilities/Shield")]
    public class ShieldAbility : EnemyAbilityData
    {
        [Min(0f)] public float initialDelay = 2f;
        [Min(0.1f)] public float cooldown = 7f;
        [Min(0.05f)] public float duration = 1.5f;

        public override EnemyAbilityBehaviour Attach(Enemy enemy) => AttachBehaviour<ShieldAbilityBehaviour>(enemy);
    }
}
