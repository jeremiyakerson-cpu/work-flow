using TowerDefense.Visuals.Pure;
using UnityEngine;

namespace TowerDefense.Visuals
{
    /// <summary>
    /// UnityEngine.Color view of the game's theme (<see cref="ArtPalette"/>), plus
    /// the gameplay colour rules used by in-world feedback (damage types, health).
    /// </summary>
    public static class Palette
    {
        public static Color ToColor(Rgba c) => new Color(c.R, c.G, c.B, c.A);
        public static Rgba ToRgba(Color c) => new Rgba(c.r, c.g, c.b, c.a);

        public static readonly Color Outline = ToColor(ArtPalette.Outline);
        public static readonly Color Background = ToColor(ArtPalette.Background);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.28f);
        public static readonly Color Gold = ToColor(ArtPalette.Gold);
        public static readonly Color SlowTint = ToColor(ArtPalette.SlowTint);
        public static readonly Color PoisonTint = ToColor(ArtPalette.PoisonTint);
        public static readonly Color BranchA = ToColor(ArtPalette.BranchA);
        public static readonly Color BranchB = ToColor(ArtPalette.BranchB);
        public static readonly Color Spark = ToColor(ArtPalette.Spark);
        public static readonly Color Magic = ToColor(ArtPalette.Magic);
        public static readonly Color Toxic = ToColor(ArtPalette.Toxic);
        public static readonly Color Ice = ToColor(ArtPalette.Ice);
        public static readonly Color Fire = ToColor(ArtPalette.Fire);
        public static readonly Color Leak = ToColor(ArtPalette.Blood);
        public static readonly Color Hero = ToColor(ArtPalette.Hero);
        public static readonly Color Portal = ToColor(ArtPalette.Portal);
        public static readonly Color Dust = new Color(0.86f, 0.78f, 0.62f, 0.85f);
        public static readonly Color White = new Color(1f, 1f, 1f, 1f);

        /// <summary>Hit-spark colour per damage type.</summary>
        public static Color DamageColor(DamageType type)
        {
            switch (type)
            {
                case DamageType.Magic: return Magic;
                case DamageType.Poison: return Toxic;
                default: return Spark;
            }
        }

        /// <summary>Green → yellow → red for a 0..1 health fraction.</summary>
        public static Color HealthColor(float fraction) => ToColor(ArtPalette.HealthColor(fraction));

        /// <summary>Accent colour shown on a tower after its branch upgrade.</summary>
        public static Color BranchColor(Tower.UpgradePath path, Color neutral) =>
            path == Tower.UpgradePath.PathA ? BranchA : path == Tower.UpgradePath.PathB ? BranchB : neutral;
    }
}
