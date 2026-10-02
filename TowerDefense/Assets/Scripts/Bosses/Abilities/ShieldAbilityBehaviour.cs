using UnityEngine;

namespace TowerDefense.Bosses
{
    /// <summary>Runtime half of ShieldAbility. Only clears Invulnerable if it set it.</summary>
    public class ShieldAbilityBehaviour : EnemyAbilityBehaviour
    {
        private ShieldAbility config;
        private float cooldownLeft;
        private float shieldLeft;
        private bool shieldUp;

        public bool IsShielded => shieldUp;
        public override bool IsActive => shieldUp;

        public override void Bind(Enemy owner, EnemyAbilityData data)
        {
            base.Bind(owner, data);
            config = data as ShieldAbility;
            shieldUp = false;
            cooldownLeft = config != null ? config.initialDelay : 0f;
        }

        private void Update()
        {
            if (config == null || !OwnerAlive) return;
            float dt = Time.deltaTime;

            if (shieldUp)
            {
                shieldLeft -= dt;
                if (shieldLeft <= 0f) Lower();
                return;
            }

            cooldownLeft -= dt;
            if (cooldownLeft <= 0f) Raise();
        }

        private void Raise()
        {
            shieldUp = true;
            shieldLeft = config.duration;
            Owner.Invulnerable = true;
            RaiseTriggered();
        }

        private void Lower()
        {
            shieldUp = false;
            Owner.Invulnerable = false;
            cooldownLeft = config.cooldown;
        }
    }
}
