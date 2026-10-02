using UnityEngine;

namespace TowerDefense.Bosses
{
    /// <summary>
    /// Periodic speed burst: every cooldown seconds the enemy dashes forward at
    /// speedMultiplier for duration seconds. Waits while blocked, so a hero can
    /// still hold it, but it charges again the moment it is released.
    /// </summary>
    [CreateAssetMenu(fileName = "Charge", menuName = "Tower Defense/Abilities/Charge")]
    public class ChargeAbility : EnemyAbilityData
    {
        [Min(0f)] public float initialDelay = 3f;
        [Min(0.1f)] public float cooldown = 6f;
        [Min(0.05f)] public float duration = 1.2f;
        public float speedMultiplier = 2.5f;

        public override EnemyAbilityBehaviour Attach(Enemy enemy) => AttachBehaviour<ChargeAbilityBehaviour>(enemy);
    }
}
