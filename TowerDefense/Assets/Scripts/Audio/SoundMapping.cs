namespace TowerDefense.Audio
{
    /// <summary>Engine-free rules mapping gameplay facts to sounds (kept here so they are unit-tested).</summary>
    public static class SoundMapping
    {
        /// <summary>
        /// Shot sound for a tower, from its data asset's base traits (not branch
        /// upgrades, so an archer that gains slow on a branch still sounds like an archer).
        /// Priority: splash (cannon) &gt; slow (frost) &gt; poison &gt; magic (zap) &gt; physical (arrow).
        /// </summary>
        public static SoundId ForTowerShot(bool magicDamage, bool poisonDamage, float splashRadius, bool appliesSlow, bool appliesPoison)
        {
            if (splashRadius > 0f) return SoundId.CannonShot;
            if (appliesSlow) return SoundId.Frost;
            if (appliesPoison || poisonDamage) return SoundId.Poison;
            if (magicDamage) return SoundId.MagicZap;
            return SoundId.ArrowShot;
        }

        /// <summary>Death sound: bosses get the big explosion.</summary>
        public static SoundId ForEnemyDeath(bool isBoss) => isBoss ? SoundId.BossDeath : SoundId.EnemyDeath;
    }
}
