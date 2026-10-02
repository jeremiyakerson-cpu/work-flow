using System;

namespace TowerDefense.Core
{
    /// <summary>
    /// The armor triangle as a data table: damage multiplier for every
    /// DamageType x ArmorType pair. One source of truth - Enemy.TakeDamage,
    /// tooltips and balance tests all read <see cref="Default"/>.
    /// Physical shreds unarmored targets but struggles vs Heavy; Magic and
    /// Poison ignore armor and punish Heavy.
    /// </summary>
    public sealed class DamageTable
    {
        private static readonly int DamageTypeCount = Enum.GetValues(typeof(DamageType)).Length;
        private static readonly int ArmorTypeCount = Enum.GetValues(typeof(ArmorType)).Length;

        private readonly float[] multipliers = new float[DamageTypeCount * ArmorTypeCount];

        /// <summary>The game's shipping table (the KR-style triangle).</summary>
        public static readonly DamageTable Default = CreateDefault();

        /// <summary>A table where every pair is 1 (no resistances).</summary>
        public DamageTable()
        {
            for (int i = 0; i < multipliers.Length; i++) multipliers[i] = 1f;
        }

        public static DamageTable CreateDefault()
        {
            var t = new DamageTable();
            t.Set(DamageType.Physical, ArmorType.None, 1f);
            t.Set(DamageType.Physical, ArmorType.Light, 0.85f);
            t.Set(DamageType.Physical, ArmorType.Heavy, 0.55f);
            t.Set(DamageType.Magic, ArmorType.None, 1f);
            t.Set(DamageType.Magic, ArmorType.Light, 1f);
            t.Set(DamageType.Magic, ArmorType.Heavy, 1.25f);
            t.Set(DamageType.Poison, ArmorType.None, 1f);
            t.Set(DamageType.Poison, ArmorType.Light, 1f);
            t.Set(DamageType.Poison, ArmorType.Heavy, 1.25f);
            return t;
        }

        /// <summary>Damage multiplier for this pair. Unknown enum values resolve to 1.</summary>
        public float Multiplier(DamageType type, ArmorType armor)
        {
            int i = Index(type, armor);
            return i < 0 ? 1f : multipliers[i];
        }

        /// <summary>Override one pair (balance experiments, difficulty modes). Negative/NaN values clamp to 0.</summary>
        public void Set(DamageType type, ArmorType armor, float multiplier)
        {
            int i = Index(type, armor);
            if (i < 0) throw new ArgumentOutOfRangeException(nameof(type));
            multipliers[i] = multiplier > 0f ? multiplier : 0f;
        }

        /// <summary>Final damage after resistances. Never negative; NaN/negative input deals 0.</summary>
        public float Apply(float amount, DamageType type, ArmorType armor)
        {
            if (!(amount > 0f)) return 0f;
            return amount * Multiplier(type, armor);
        }

        private static int Index(DamageType type, ArmorType armor)
        {
            int d = (int)type, a = (int)armor;
            if (d < 0 || d >= DamageTypeCount || a < 0 || a >= ArmorTypeCount) return -1;
            return d * ArmorTypeCount + a;
        }
    }
}
