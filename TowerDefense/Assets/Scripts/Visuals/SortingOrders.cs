using UnityEngine;

namespace TowerDefense.Visuals
{
    /// <summary>
    /// Global draw order (default sorting layer, sortingOrder only, so no
    /// TagManager sorting layers are required):
    /// ground &lt; path &lt; decor &lt; slots &lt; towers &lt; units &lt; projectiles &lt; fx.
    /// Bands that hold y-sorted objects are 1000 wide: <see cref="ForY"/> adds
    /// an offset so objects lower on screen draw in front. Multi-sprite objects
    /// use a SortingGroup at the band order and small local orders for parts.
    /// </summary>
    public static class SortingOrders
    {
        public const int Ground = -3000;
        public const int GroundDetail = -2990;
        public const int PathShadow = -2900;
        public const int PathBorder = -2890;
        public const int PathFill = -2880;
        public const int PathHighlight = -2870;
        public const int PathMarkers = -2500;

        /// <summary>Trees, rocks, flowers (y-sorted).</summary>
        public const int Decor = -1000;
        /// <summary>Darkens the area outside the play rect; above decor, below gameplay.</summary>
        public const int Vignette = -400;
        /// <summary>Build plots (y-sorted).</summary>
        public const int Slots = 1000;
        /// <summary>Towers (y-sorted SortingGroup).</summary>
        public const int Towers = 3000;
        /// <summary>Enemies, hero and barricades share a band so they interleave by y.</summary>
        public const int Units = 5000;
        public const int Projectiles = 7000;
        public const int Fx = 8000;
        public const int FxText = 8600;

        /// <summary>Half-width of a y-sorted band.</summary>
        public const int BandHalfWidth = 499;
        /// <summary>Sort steps per world unit of y.</summary>
        public const float StepsPerUnit = 10f;

        /// <summary>Order offset for a world y: lower y (nearer the viewer) sorts higher.</summary>
        public static int YOffset(float y) => Mathf.Clamp(Mathf.RoundToInt(-y * StepsPerUnit), -BandHalfWidth, BandHalfWidth);

        /// <summary>Band order plus the y offset.</summary>
        public static int ForY(int band, float y) => band + YOffset(y);
    }
}
