using TowerDefense.Levels;
using UnityEngine;

namespace TowerDefense.Content
{
    /// <summary>
    /// Art-direction hints for the Visuals workstream's procedural templates.
    /// EnemyData carries tint/visualScale itself; TowerData has no tint field,
    /// so tower colours live here, keyed by id (falls back on damage type).
    /// </summary>
    public static class ContentVisualHints
    {
        /// <summary>Signature colour of a tower (roof/crystal/banner).</summary>
        public static Color TowerTint(TowerData tower)
        {
            if (tower == null) return Color.white;
            switch (tower.id)
            {
                case ContentIds.Archer: return LevelFactory.ToColor(0x5FA845);
                case ContentIds.Mage: return LevelFactory.ToColor(0x7A4FD6);
                case ContentIds.Artillery: return LevelFactory.ToColor(0x8A6A4A);
                case ContentIds.Frost: return LevelFactory.ToColor(0x6EC6F0);
                case ContentIds.Alchemist: return LevelFactory.ToColor(0x9BD44A);
            }
            switch (tower.damageType)
            {
                case DamageType.Magic: return LevelFactory.ToColor(0x7A4FD6);
                case DamageType.Poison: return LevelFactory.ToColor(0x9BD44A);
                default: return LevelFactory.ToColor(0x8A6A4A);
            }
        }

        /// <summary>
        /// Whether a tower should fire a visible projectile (assign
        /// TowerData.projectilePrefab) rather than hitscan. Splash towers need a
        /// projectile so the blast lands where the shell does; mage bolts and
        /// flasks read better as projectiles too. Archers/frost may stay hitscan
        /// with a tracer, but a projectile is fine for them as well.
        /// </summary>
        public static bool WantsProjectile(TowerData tower)
        {
            if (tower == null) return false;
            if (tower.splashRadius > 0f) return true;
            return tower.id == ContentIds.Mage || tower.id == ContentIds.Archer;
        }

        /// <summary>Projectile colour: the tower tint, brightened.</summary>
        public static Color ProjectileTint(TowerData tower) => Color.Lerp(TowerTint(tower), Color.white, 0.35f);
    }
}
