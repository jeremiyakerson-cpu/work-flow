using UnityEngine;

namespace TowerDefense.Bosses
{
    /// <summary>Runtime half of EnrageAbility. Event-driven: checks health whenever the owner is damaged.</summary>
    public class EnrageAbilityBehaviour : EnemyAbilityBehaviour
    {
        private EnrageAbility config;
        private bool enraged;

        public bool IsEnraged => enraged;
        public override bool IsActive => enraged;

        public override void Bind(Enemy owner, EnemyAbilityData data)
        {
            base.Bind(owner, data);
            config = data as EnrageAbility;
            enraged = false;
            if (owner != null) owner.Damaged += OnDamaged;
        }

        private void OnDamaged(Enemy enemy, float amount)
        {
            if (enraged || config == null || !OwnerAlive) return;
            float hp = Owner.HealthPercent();
            // hp <= 0 means this hit is lethal: Die() runs right after Damaged.
            if (hp <= 0f || hp > config.healthThreshold) return;

            enraged = true;
            Owner.SpeedMultiplier *= Mathf.Max(0.01f, config.speedMultiplier);
            Owner.meleeDamage *= Mathf.Max(0f, config.meleeDamageMultiplier);
            Owner.attackInterval /= Mathf.Max(0.01f, config.attackRateMultiplier);
            RaiseTriggered();
        }

        private void OnDestroy()
        {
            if (Owner != null) Owner.Damaged -= OnDamaged;
        }
    }
}
