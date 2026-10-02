using System;

namespace TowerDefense.Visuals.Pure
{
    /// <summary>
    /// Engine-free straight-alpha colour (0..1 floats) used by the rasterizer and
    /// the art palette. Engine code converts to UnityEngine.Color / Color32.
    /// </summary>
    [Serializable]
    public struct Rgba : IEquatable<Rgba>
    {
        public float R;
        public float G;
        public float B;
        public float A;

        public Rgba(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }

        public static Rgba White => new Rgba(1f, 1f, 1f, 1f);
        public static Rgba Black => new Rgba(0f, 0f, 0f, 1f);
        public static Rgba Clear => new Rgba(0f, 0f, 0f, 0f);

        /// <summary>From 0xRRGGBB.</summary>
        public static Rgba Hex(uint rgb, float a = 1f) =>
            new Rgba(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        public Rgba WithAlpha(float a) => new Rgba(R, G, B, a);

        /// <summary>Move toward white by t (0..1).</summary>
        public Rgba Lighten(float t) => Lerp(this, new Rgba(1f, 1f, 1f, A), t);

        /// <summary>Move toward black by t (0..1).</summary>
        public Rgba Darken(float t) => Lerp(this, new Rgba(0f, 0f, 0f, A), t);

        /// <summary>Multiply RGB (not alpha) by k, clamped.</summary>
        public Rgba Scale(float k) => new Rgba(Clamp01(R * k), Clamp01(G * k), Clamp01(B * k), A);

        public Rgba Multiply(Rgba o) => new Rgba(R * o.R, G * o.G, B * o.B, A * o.A);

        public float Luminance => 0.2126f * R + 0.7152f * G + 0.0722f * B;

        public static Rgba Lerp(Rgba a, Rgba b, float t)
        {
            t = Clamp01(t);
            return new Rgba(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
        }

        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        public static byte ToByte(float v) => (byte)(Clamp01(v) * 255f + 0.5f);

        public bool Equals(Rgba o) => R == o.R && G == o.G && B == o.B && A == o.A;
        public override bool Equals(object obj) => obj is Rgba c && Equals(c);
        public override int GetHashCode() => HashCode.Combine(R, G, B, A);
        public override string ToString() => $"RGBA({R:0.###}, {G:0.###}, {B:0.###}, {A:0.###})";

        /// <summary>Stable key for caches (8 bits per channel).</summary>
        public uint Key => ((uint)ToByte(R) << 24) | ((uint)ToByte(G) << 16) | ((uint)ToByte(B) << 8) | ToByte(A);
    }
}
