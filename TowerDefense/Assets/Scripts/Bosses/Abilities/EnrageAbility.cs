using UnityEngine;

namespace TowerDefense.Bosses
{
    /// <summary>
    /// Below a health threshold the enemy permanently speeds up and hits harder
    /// (KR "berserk"). One-shot per spawn.
    /// </summary>
    [CreateAssetMenu(fileName = "Enrage", menuName = "Tower Defense/Abilities/Enrage")]
    public class EnrageAbility : EnemyAbilityData
    {
        [Tooltip("Enrage once health falls to or below this fraction of max.")]
        [Range(0.05f, 1f)] public float healthThreshold = 0.4f;
        [Tooltip("Multiplies Enemy.SpeedMultiplier (stacks with slows and charges).")]
        public float speedMultiplier = 1.5f;
        [Tooltip("Multiplies melee damage against heroes/barricades.")]
        public float meleeDamageMultiplier = 2f;
        [Tooltip("Divides the melee attack interval (2 = attacks twice as often).")]
        public float attackRateMultiplier = 1.5f;

        public override EnemyAbilityBehaviour Attach(Enemy enemy) => AttachBehaviour<EnrageAbilityBehaviour>(enemy);
    }
}
