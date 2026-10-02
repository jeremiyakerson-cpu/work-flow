namespace TowerDefense.Visuals.Pure
{
    /// <summary>
    /// The game's colour theme (engine-free). Saturated, warm, flat colours with
    /// one shared dark outline so silhouettes read on small phone screens.
    /// Engine code mirrors these as UnityEngine.Color in Visuals.Palette.
    /// </summary>
    public static class ArtPalette
    {
        public static readonly Rgba Outline = Rgba.Hex(0x2A2233);
        public static readonly Rgba Shadow = new Rgba(0f, 0f, 0f, 0.3f);
        public static readonly Rgba Background = Rgba.Hex(0x1E2A1C);

        // Materials
        public static readonly Rgba Stone = Rgba.Hex(0xA9A6A0);
        public static readonly Rgba StoneDark = Rgba.Hex(0x77746F);
        public static readonly Rgba Wood = Rgba.Hex(0xA8703E);
        public static readonly Rgba WoodDark = Rgba.Hex(0x6E4526);
        public static readonly Rgba Iron = Rgba.Hex(0x4A4E58);
        public static readonly Rgba Gold = Rgba.Hex(0xFFC93C);
        public static readonly Rgba Cream = Rgba.Hex(0xFFF1D0);
        public static readonly Rgba Dirt = Rgba.Hex(0x9C7A52);

        // Tower families
        public static readonly Rgba Archer = Rgba.Hex(0xB57A45);
        public static readonly Rgba ArcherRoof = Rgba.Hex(0x5E9E3E);
        public static readonly Rgba Mage = Rgba.Hex(0x7A5AB8);
        public static readonly Rgba MageRoof = Rgba.Hex(0xA98BE6);
        public static readonly Rgba Artillery = Rgba.Hex(0x8F8C88);
        public static readonly Rgba ArtilleryRoof = Rgba.Hex(0x5C5A60);
        public static readonly Rgba Frost = Rgba.Hex(0x8FC9EE);
        public static readonly Rgba FrostRoof = Rgba.Hex(0xE2F4FF);
        public static readonly Rgba Poison = Rgba.Hex(0x4F8A45);
        public static readonly Rgba PoisonRoof = Rgba.Hex(0x9BD04A);
        public static readonly Rgba Generic = Rgba.Hex(0x7F95A8);
        public static readonly Rgba GenericRoof = Rgba.Hex(0xB9CAD8);

        // Effects / status
        public static readonly Rgba Magic = Rgba.Hex(0xD27CFF);
        public static readonly Rgba Ice = Rgba.Hex(0xBDEBFF);
        public static readonly Rgba Toxic = Rgba.Hex(0x9BE04A);
        public static readonly Rgba Fire = Rgba.Hex(0xFF9A3C);
        public static readonly Rgba Spark = Rgba.Hex(0xFFF2B0);
        public static readonly Rgba SlowTint = Rgba.Hex(0x8CC2FF);
        public static readonly Rgba PoisonTint = Rgba.Hex(0xA6F07A);
        public static readonly Rgba Portal = Rgba.Hex(0x9B4DFF);
        public static readonly Rgba Blood = Rgba.Hex(0xE04848);

        // UI-in-world
        public static readonly Rgba HealthHigh = Rgba.Hex(0x5BD65B);
        public static readonly Rgba HealthMid = Rgba.Hex(0xF2C94C);
        public static readonly Rgba HealthLow = Rgba.Hex(0xEB5757);
        public static readonly Rgba HealthBack = Rgba.Hex(0x2A2233, 0.85f);
        public static readonly Rgba BranchA = Rgba.Hex(0xE8553E);
        public static readonly Rgba BranchB = Rgba.Hex(0x2EC4B6);
        public static readonly Rgba Hero = Rgba.Hex(0x3D7BE0);

        /// <summary>Health bar colour for a 0..1 fraction (green → yellow → red).</summary>
        public static Rgba HealthColor(float fraction)
        {
            fraction = Rgba.Clamp01(fraction);
            return fraction > 0.5f
                ? Rgba.Lerp(HealthMid, HealthHigh, (fraction - 0.5f) * 2f)
                : Rgba.Lerp(HealthLow, HealthMid, fraction * 2f);
        }
    }
}
