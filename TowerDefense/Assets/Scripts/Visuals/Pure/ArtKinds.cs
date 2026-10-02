namespace TowerDefense.Visuals.Pure
{
    /// <summary>Procedural tower looks.</summary>
    public enum TowerArtKind { Archer, Mage, Artillery, Frost, Poison, Generic }

    /// <summary>Procedural projectile looks.</summary>
    public enum ProjectileArtKind { Arrow, Orb, Shell, Shard, Flask }

    /// <summary>Procedural enemy silhouettes.</summary>
    public enum EnemyArtKind { Grunt, Rogue, Brute, Flyer, Boss }

    /// <summary>
    /// Maps data fields to art kinds without touching engine types, so the rules
    /// are unit-testable. Id keywords win; otherwise the combat flags decide.
    /// </summary>
    public static class ArtClassifier
    {
        public static TowerArtKind Tower(string id, bool magicDamage, bool poisonDamage, bool splash, bool slows, bool poisons)
        {
            string k = id == null ? string.Empty : id.ToLowerInvariant();
            if (Has(k, "frost", "ice", "freeze", "cold", "snow")) return TowerArtKind.Frost;
            if (Has(k, "poison", "venom", "toxic", "plague", "acid")) return TowerArtKind.Poison;
            if (Has(k, "artillery", "cannon", "bomb", "mortar", "catapult", "dwarf")) return TowerArtKind.Artillery;
            if (Has(k, "mage", "magic", "wizard", "arcane", "sorcer")) return TowerArtKind.Mage;
            if (Has(k, "archer", "arrow", "bow", "ranger", "crossbow", "marksman")) return TowerArtKind.Archer;

            if (splash) return TowerArtKind.Artillery;
            if (poisons || poisonDamage) return TowerArtKind.Poison;
            if (slows) return TowerArtKind.Frost;
            if (magicDamage) return TowerArtKind.Mage;
            return id == null || k.Length == 0 ? TowerArtKind.Generic : TowerArtKind.Archer;
        }

        public static ProjectileArtKind Projectile(TowerArtKind tower)
        {
            switch (tower)
            {
                case TowerArtKind.Mage: return ProjectileArtKind.Orb;
                case TowerArtKind.Artillery: return ProjectileArtKind.Shell;
                case TowerArtKind.Frost: return ProjectileArtKind.Shard;
                case TowerArtKind.Poison: return ProjectileArtKind.Flask;
                default: return ProjectileArtKind.Arrow;
            }
        }

        /// <summary>armor: 0 None, 1 Light, 2 Heavy (ArmorType order).</summary>
        public static EnemyArtKind Enemy(int armor, bool flying, bool boss)
        {
            if (boss) return EnemyArtKind.Boss;
            if (flying) return EnemyArtKind.Flyer;
            if (armor >= 2) return EnemyArtKind.Brute;
            if (armor == 1) return EnemyArtKind.Rogue;
            return EnemyArtKind.Grunt;
        }

        private static bool Has(string k, params string[] words)
        {
            foreach (var w in words) if (k.Contains(w)) return true;
            return false;
        }
    }
}
