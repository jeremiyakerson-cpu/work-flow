using UnityEngine;

namespace TowerDefense.UI
{
    /// <summary>
    /// Colours, sizes and the font for every code-built UI element. Sizes are
    /// canvas units at the 1920x1080 reference resolution (match 0.5): 130 units
    /// is >= 44pt on every iPhone/iPad, so interactive elements never go below
    /// <see cref="MinTouch"/>.
    /// </summary>
    public static class UITheme
    {
        // ---- palette (warm KR-like fantasy)
        public static readonly Color Panel = new Color(0.14f, 0.12f, 0.15f, 0.94f);
        public static readonly Color PanelLight = new Color(0.24f, 0.21f, 0.25f, 0.96f);
        public static readonly Color PanelBorder = new Color(0.55f, 0.43f, 0.27f, 1f);
        public static readonly Color Parchment = new Color(0.93f, 0.86f, 0.70f, 1f);
        public static readonly Color ScreenBackground = new Color(0.10f, 0.12f, 0.14f, 1f);
        public static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.62f);

        public static readonly Color Primary = new Color(0.32f, 0.66f, 0.29f, 1f);
        public static readonly Color Secondary = new Color(0.27f, 0.45f, 0.72f, 1f);
        public static readonly Color Danger = new Color(0.82f, 0.28f, 0.24f, 1f);
        public static readonly Color Gold = new Color(1f, 0.78f, 0.22f, 1f);
        public static readonly Color GoldDark = new Color(0.72f, 0.50f, 0.10f, 1f);
        public static readonly Color Neutral = new Color(0.36f, 0.33f, 0.38f, 1f);
        public static readonly Color Disabled = new Color(0.32f, 0.32f, 0.34f, 1f);

        public static readonly Color Text = new Color(0.98f, 0.96f, 0.90f, 1f);
        public static readonly Color TextDim = new Color(0.76f, 0.72f, 0.66f, 1f);
        public static readonly Color TextDark = new Color(0.20f, 0.15f, 0.10f, 1f);
        public static readonly Color TextBad = new Color(1f, 0.42f, 0.38f, 1f);
        public static readonly Color TextGood = new Color(0.55f, 0.95f, 0.45f, 1f);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.55f);

        public static readonly Color Health = new Color(0.86f, 0.22f, 0.22f, 1f);
        public static readonly Color HealthGood = new Color(0.36f, 0.82f, 0.33f, 1f);
        public static readonly Color CooldownShade = new Color(0f, 0f, 0f, 0.62f);
        public static readonly Color Highlight = new Color(1f, 0.92f, 0.45f, 1f);
        public static readonly Color StarOff = new Color(0.25f, 0.22f, 0.26f, 1f);
        public static readonly Color RangeFill = new Color(0.55f, 0.85f, 1f, 0.12f);
        public static readonly Color RangeEdge = new Color(0.75f, 0.92f, 1f, 0.85f);

        // ---- sizes (canvas units)
        public const float MinTouch = 130f;
        public const float ButtonHeight = 130f;
        public const float ButtonWidth = 520f;
        public const float IconButton = 130f;
        public const float Margin = 28f;
        public const float Spacing = 20f;
        public const float Radius = 26f;

        // ---- fonts
        public const int FontSmall = 32;
        public const int FontBody = 40;
        public const int FontButton = 46;
        public const int FontLarge = 58;
        public const int FontTitle = 96;
        public const int FontHuge = 140;

        // ---- motion (seconds, unscaled)
        public const float FastAnim = 0.12f;
        public const float NormalAnim = 0.22f;
        public const float SlowAnim = 0.4f;

        private static Font font;

        /// <summary>Unity's built-in legacy font (no asset needed).</summary>
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        /// <summary>Slightly darker/lighter variant of a colour (keeps alpha).</summary>
        public static Color Shade(Color c, float factor)
        {
            return new Color(Mathf.Clamp01(c.r * factor), Mathf.Clamp01(c.g * factor), Mathf.Clamp01(c.b * factor), c.a);
        }

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>Tower badge colour by damage type (when TowerData has no icon).</summary>
        public static Color ForDamage(DamageType type)
        {
            switch (type)
            {
                case DamageType.Magic: return new Color(0.56f, 0.38f, 0.86f, 1f);
                case DamageType.Poison: return new Color(0.36f, 0.70f, 0.30f, 1f);
                default: return new Color(0.78f, 0.52f, 0.26f, 1f);
            }
        }
    }
}
