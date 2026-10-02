using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.UI
{
    /// <summary>
    /// Procedurally generated, anti-aliased UI sprites (signed-distance shapes
    /// rasterised once into small textures and cached). White unless noted, so
    /// Image.color tints them. RoundedRect/RoundedOutline are 9-sliced.
    /// </summary>
    public static class UISprites
    {
        /// <summary>Corner radius in pixels baked into the 9-sliced sprites.</summary>
        public const float SliceRadius = 24f;

        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite RoundedRect => Get("rrect", () =>
            Build("UI_RoundedRect", 64, (x, y) => RoundBox(x, y, 31f, 31f, SliceRadius), Border(28)));

        public static Sprite RoundedOutline => Get("rrect_outline", () =>
            Build("UI_RoundedOutline", 64, (x, y) => Mathf.Abs(RoundBox(x, y, 28f, 28f, SliceRadius - 4f)) - 3f, Border(28)));

        public static Sprite Circle => Get("circle", () => Build("UI_Circle", 128, (x, y) => Len(x, y) - 62f, Vector4.zero));

        public static Sprite Glow => Get("glow", () => BuildAlpha("UI_Glow", 128, (x, y) =>
        {
            float t = Mathf.Clamp01(1f - Len(x, y) / 63f);
            return t * t;
        }));

        /// <summary>Ring with the given stroke as a fraction of the radius (0..1).</summary>
        public static Sprite Ring(float thicknessFraction = 0.14f)
        {
            int key = Mathf.RoundToInt(thicknessFraction * 100f);
            return Get("ring" + key, () =>
            {
                float half = 62f * thicknessFraction * 0.5f;
                float mid = 62f - half;
                return Build("UI_Ring", 128, (x, y) => Mathf.Abs(Len(x, y) - mid) - half, Vector4.zero);
            });
        }

        public static Sprite Star => Get("star", () => Build("UI_Star", 128, (x, y) => Star5(x, y + 5f, 56f, 0.5f), Vector4.zero));

        public static Sprite Heart => Get("heart", () => Build("UI_Heart", 128, (x, y) =>
        {
            const float s = 98f; // heart spans ~1.1 units tall
            return HeartSdf(x / s, (y + 54f) / s) * s;
        }, Vector4.zero));

        /// <summary>Triangle pointing right (play / next). Rotate the Image for other directions.</summary>
        public static Sprite Triangle => Get("tri", () => Build("UI_Triangle", 128, (x, y) =>
            RoundedTriangleRight(x + 8f, y, 44f, 6f), Vector4.zero));

        public static Sprite Check => Get("check", () => Build("UI_Check", 128, (x, y) =>
            Mathf.Min(Segment(x, y, -38f, 2f, -10f, -26f), Segment(x, y, -10f, -26f, 40f, 30f)) - 10f, Vector4.zero));

        public static Sprite Cross => Get("cross", () => Build("UI_Cross", 128, (x, y) =>
            Mathf.Min(Segment(x, y, -34f, -34f, 34f, 34f), Segment(x, y, -34f, 34f, 34f, -34f)) - 10f, Vector4.zero));

        public static Sprite PauseIcon => Get("pause", () => Build("UI_Pause", 128, (x, y) =>
            Mathf.Min(RoundBox(x + 20f, y, 12f, 40f, 5f), RoundBox(x - 20f, y, 12f, 40f, 5f)), Vector4.zero));

        /// <summary>Gold coin with darker rim, baked in colour (do not tint).</summary>
        public static Sprite Coin => Get("coin", () => BuildColored("UI_Coin", 128, (x, y) =>
        {
            float r = Len(x, y);
            float outer = Mathf.Clamp01(0.5f - (r - 60f));
            if (outer <= 0f) return new Color(1f, 1f, 1f, 0f);
            float rimT = Mathf.Clamp01(0.5f - (44f - r)); // 1 outside radius 44
            Color face = Color.Lerp(UITheme.Gold, new Color(1f, 0.93f, 0.6f, 1f), Mathf.Clamp01((y + x * 0.3f) / 80f + 0.2f));
            Color c = Color.Lerp(face, UITheme.GoldDark, rimT * 0.85f);
            c.a = outer;
            return c;
        }));

        /// <summary>Radius (world units, at scale 1) of the bright edge of <see cref="WorldRing"/>.</summary>
        public const float WorldRingEdgeRadius = 122f / 256f;

        /// <summary>World-space range ring: faint fill with a bright edge. 1 world unit across.</summary>
        public static Sprite WorldRing => Get("world_ring", () =>
        {
            const int size = 256;
            return BuildAlphaPpu("World_RangeRing", size, size, (x, y) =>
            {
                float r = Len(x, y);
                float inside = Mathf.Clamp01(0.5f - (r - 126f));
                float edge = Mathf.Clamp01(0.5f - (Mathf.Abs(r - 122f) - 3.5f));
                return Mathf.Max(inside * 0.16f, edge);
            });
        });

        /// <summary>World-space selection marker: soft dashed-looking double ring.</summary>
        public static Sprite WorldMarker => Get("world_marker", () =>
        {
            const int size = 128;
            return BuildAlphaPpu("World_Marker", size, size, (x, y) =>
            {
                float r = Len(x, y);
                float outer = Mathf.Clamp01(0.5f - (Mathf.Abs(r - 56f) - 4f));
                float inner = Mathf.Clamp01(0.5f - (r - 50f)) * 0.22f;
                return Mathf.Max(outer, inner);
            });
        });

        // ------------------------------------------------------------------ builders

        private static Sprite Get(string key, Func<Sprite> create)
        {
            if (cache.TryGetValue(key, out Sprite s) && s != null) return s;
            s = create();
            cache[key] = s;
            return s;
        }

        private static Vector4 Border(float px) => new Vector4(px, px, px, px);

        /// <summary>White shape from a signed distance function (pixels, origin at centre, y up).</summary>
        private static Sprite Build(string name, int size, Func<float, float, float> sdf, Vector4 border)
        {
            return BuildAlphaCore(name, size, 100f, (x, y) => Mathf.Clamp01(0.5f - sdf(x, y)), border);
        }

        private static Sprite BuildAlpha(string name, int size, Func<float, float, float> alpha)
        {
            return BuildAlphaCore(name, size, 100f, alpha, Vector4.zero);
        }

        private static Sprite BuildAlphaPpu(string name, int size, float ppu, Func<float, float, float> alpha)
        {
            return BuildAlphaCore(name, size, ppu, alpha, Vector4.zero);
        }

        private static Sprite BuildAlphaCore(string name, int size, float ppu, Func<float, float, float> alpha, Vector4 border)
        {
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float a = alpha(px + 0.5f - half, py + 0.5f - half);
                    pixels[py * size + px] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            return ToSprite(name, size, ppu, pixels, border);
        }

        private static Sprite BuildColored(string name, int size, Func<float, float, Color> color)
        {
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int py = 0; py < size; py++)
                for (int px = 0; px < size; px++)
                    pixels[py * size + px] = color(px + 0.5f - half, py + 0.5f - half);
            return ToSprite(name, size, 100f, pixels, Vector4.zero);
        }

        private static Sprite ToSprite(string name, int size, float ppu, Color32[] pixels, Vector4 border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = name;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.DontSave;
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), ppu, 0,
                                          SpriteMeshType.FullRect, border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        // ------------------------------------------------------------------ distance functions (pixels)

        private static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);

        private static float RoundBox(float x, float y, float halfW, float halfH, float r)
        {
            float qx = Mathf.Abs(x) - halfW + r;
            float qy = Mathf.Abs(y) - halfH + r;
            float outside = Len(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f));
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - r;
        }

        private static float Segment(float px, float py, float ax, float ay, float bx, float by)
        {
            float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            return Len(pax - bax * h, pay - bay * h);
        }

        // Inigo Quilez's 5-point star. r = outer radius, rf = inner/outer ratio.
        private static float Star5(float px, float py, float r, float rf)
        {
            const float k1x = 0.809016994375f, k1y = -0.587785252292f;
            const float k2x = -k1x, k2y = k1y;
            px = Mathf.Abs(px);
            float d1 = Mathf.Max(k1x * px + k1y * py, 0f);
            px -= 2f * d1 * k1x; py -= 2f * d1 * k1y;
            float d2 = Mathf.Max(k2x * px + k2y * py, 0f);
            px -= 2f * d2 * k2x; py -= 2f * d2 * k2y;
            px = Mathf.Abs(px);
            py -= r;
            float bax = rf * -k1y, bay = rf * k1x - 1f;
            float h = Mathf.Clamp((px * bax + py * bay) / (bax * bax + bay * bay), 0f, r);
            float dx = px - bax * h, dy = py - bay * h;
            float sign = Mathf.Sign(py * bax - px * bay);
            return Len(dx, dy) * sign;
        }

        // Inigo Quilez's heart (unit size, bottom tip at origin).
        private static float HeartSdf(float x, float y)
        {
            x = Mathf.Abs(x);
            if (y + x > 1f)
                return Len(x - 0.25f, y - 0.75f) - 0.35355339f;
            float a = (x - 0f) * (x - 0f) + (y - 1f) * (y - 1f);
            float m = 0.5f * Mathf.Max(x + y, 0f);
            float b = (x - m) * (x - m) + (y - m) * (y - m);
            return Mathf.Sqrt(Mathf.Min(a, b)) * Mathf.Sign(x - y);
        }

        // Equilateral triangle pointing right with rounded corners.
        private static float RoundedTriangleRight(float x, float y, float r, float round)
        {
            // Rotate so the apex (+y in the base formula) points to +x.
            float px = -y, py = x;
            const float k = 1.7320508f;
            float rr = r - round;
            px = Mathf.Abs(px) - rr;
            py = py + rr / k;
            if (px + k * py > 0f)
            {
                float nx = (px - k * py) * 0.5f;
                float ny = (-k * px - py) * 0.5f;
                px = nx; py = ny;
            }
            px -= Mathf.Clamp(px, -2f * rr, 0f);
            return -Len(px, py) * Mathf.Sign(py) - round;
        }
    }
}
