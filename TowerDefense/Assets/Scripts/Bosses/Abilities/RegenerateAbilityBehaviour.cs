using UnityEngine;

namespace TowerDefense.Bosses
{
    /// <summary>Runtime half of RegenerateAbility.</summary>
    public class RegenerateAbilityBehaviour : EnemyAbilityBehaviour
    {
        private RegenerateAbility config;
        private float sinceDamage;
        private bool regenerating;

        public override bool IsActive => regenerating;

        public override void Bind(Enemy owner, EnemyAbilityData data)
        {
            base.Bind(owner, data);
            config = data as RegenerateAbility;
            sinceDamage = 0f;
            regenerating = false;
            if (owner != null) owner.Damaged += OnDamaged;
        }

        private void OnDamaged(Enemy enemy, float amount)
        {
            sinceDamage = 0f;
            regenerating = false;
        }

        private void Update()
        {
            if (config == null || !OwnerAlive) return;
            sinceDamage += Time.deltaTime;
            if (sinceDamage < config.regenDelay || Owner.HealthPercent() >= 1f)
            {
                regenerating = false;
                return;
            }

            if (!regenerating)
            {
                regenerating = true;
                RaiseTriggered();
            }
            Owner.Heal(Owner.maxHealth * config.healPercentPerSecond * Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (Owner != null) Owner.Damaged -= OnDamaged;
        }
    }
}
