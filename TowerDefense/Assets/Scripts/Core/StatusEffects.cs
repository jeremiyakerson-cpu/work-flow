namespace TowerDefense.Core
{
    /// <summary>
    /// Slow effect state. Rules (no stacking, strongest wins):
    /// a stronger slow replaces the current one and its timer; an equal slow
    /// refreshes to the longer remaining time; a weaker slow is ignored while
    /// a stronger one is active (so a weak long slow can't extend a strong one).
    /// </summary>
    public struct SlowEffect
    {
        /// <summary>Speed multiplier while active, 1 when not slowed.</summary>
        public float Multiplier;
        public float TimeRemaining;

        public static SlowEffect None => new SlowEffect { Multiplier = 1f, TimeRemaining = 0f };

        public bool IsActive => TimeRemaining > 0f && Multiplier < 1f;
        /// <summary>The multiplier to apply to movement right now.</summary>
        public float CurrentMultiplier => IsActive ? Multiplier : 1f;

        /// <summary>Apply a slow (multiplier clamped to 0..1). Returns true if it changed the state.</summary>
        public bool Apply(float multiplier, float duration)
        {
            if (!(duration > 0f) || float.IsNaN(multiplier)) return false;
            if (multiplier < 0f) multiplier = 0f;
            if (multiplier >= 1f) return false;

            if (!IsActive || multiplier < Multiplier)
            {
                Multiplier = multiplier;
                TimeRemaining = duration;
                return true;
            }
            if (multiplier == Multiplier && duration > TimeRemaining)
            {
                TimeRemaining = duration;
                return true;
            }
            return false;
        }

        public void Tick(float deltaTime)
        {
            if (TimeRemaining <= 0f || !(deltaTime > 0f)) return;
            TimeRemaining -= deltaTime;
            if (TimeRemaining <= 0f) this = None;
        }
    }

    /// <summary>
    /// Poison (damage over time) state. Doesn't stack: a stronger poison replaces
    /// the current one, an equal one refreshes to the longer duration, a weaker
    /// one is ignored while active. Tick returns the exact damage for the step,
    /// so total damage is dps x duration regardless of frame rate.
    /// </summary>
    public struct PoisonEffect
    {
        public float DamagePerSecond;
        public float TimeRemaining;

        public bool IsActive => TimeRemaining > 0f && DamagePerSecond > 0f;

        public bool Apply(float damagePerSecond, float duration)
        {
            if (!(duration > 0f) || !(damagePerSecond > 0f)) return false;

            if (!IsActive || damagePerSecond > DamagePerSecond)
            {
                DamagePerSecond = damagePerSecond;
                TimeRemaining = duration;
                return true;
            }
            if (damagePerSecond == DamagePerSecond && duration > TimeRemaining)
            {
                TimeRemaining = duration;
                return true;
            }
            return false;
        }

        /// <summary>Advance time; returns raw poison damage dealt during this step (before resistances).</summary>
        public float Tick(float deltaTime)
        {
            if (!IsActive || !(deltaTime > 0f)) return 0f;
            float step = deltaTime < TimeRemaining ? deltaTime : TimeRemaining;
            float damage = DamagePerSecond * step;
            TimeRemaining -= step;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                DamagePerSecond = 0f;
            }
            return damage;
        }

        public void Clear()
        {
            DamagePerSecond = 0f;
            TimeRemaining = 0f;
        }
    }
}
