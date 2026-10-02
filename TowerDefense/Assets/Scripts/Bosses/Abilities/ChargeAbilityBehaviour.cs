using UnityEngine;

namespace TowerDefense.Bosses
{
    /// <summary>Runtime half of ChargeAbility. Scaled time, so it pauses and fast-forwards with the game.</summary>
    public class ChargeAbilityBehaviour : EnemyAbilityBehaviour
    {
        private ChargeAbility config;
        private float cooldownLeft;
        private float chargeLeft;
        private bool charging;
        private float appliedMultiplier = 1f;

        public bool IsCharging => charging;
        public override bool IsActive => charging;

        public override void Bind(Enemy owner, EnemyAbilityData data)
        {
            base.Bind(owner, data);
            config = data as ChargeAbility;
            charging = false;
            cooldownLeft = config != null ? config.initialDelay : 0f;
        }

        private void Update()
        {
            if (config == null || !OwnerAlive) return;
            float dt = Time.deltaTime;

            if (charging)
            {
                chargeLeft -= dt;
                if (chargeLeft <= 0f) EndCharge();
                return;
            }

            cooldownLeft -= dt;
            // Hold the charge while blocked: dashing in place would waste it.
            if (cooldownLeft <= 0f && !Owner.IsBlocked) BeginCharge();
        }

        private void BeginCharge()
        {
            charging = true;
            chargeLeft = config.duration;
            appliedMultiplier = Mathf.Max(0.01f, config.speedMultiplier);
            Owner.SpeedMultiplier *= appliedMultiplier;
            RaiseTriggered();
        }

        private void EndCharge()
        {
            charging = false;
            Owner.SpeedMultiplier /= appliedMultiplier;
            appliedMultiplier = 1f;
            cooldownLeft = config.cooldown;
        }
    }
}
