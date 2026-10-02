using System;

namespace TowerDefense.Visuals.Pure
{
    /// <summary>
    /// Minimal engine-free 2D vector so geometry helpers can be unit-tested on
    /// plain .NET. Engine code converts to/from UnityEngine.Vector2 at the edge.
    /// </summary>
    [Serializable]
    public struct Vec2f : IEquatable<Vec2f>
    {
        public float X;
        public float Y;

        public Vec2f(float x, float y) { X = x; Y = y; }

        public static Vec2f Zero => new Vec2f(0f, 0f);

        public float Length => MathF.Sqrt(X * X + Y * Y);
        public float LengthSq => X * X + Y * Y;

        public Vec2f Normalized
        {
            get
            {
                float len = Length;
                return len > 1e-6f ? new Vec2f(X / len, Y / len) : Zero;
            }
        }

        public static Vec2f operator +(Vec2f a, Vec2f b) => new Vec2f(a.X + b.X, a.Y + b.Y);
        public static Vec2f operator -(Vec2f a, Vec2f b) => new Vec2f(a.X - b.X, a.Y - b.Y);
        public static Vec2f operator -(Vec2f a) => new Vec2f(-a.X, -a.Y);
        public static Vec2f operator *(Vec2f a, float s) => new Vec2f(a.X * s, a.Y * s);
        public static Vec2f operator *(float s, Vec2f a) => new Vec2f(a.X * s, a.Y * s);

        public static float Dot(Vec2f a, Vec2f b) => a.X * b.X + a.Y * b.Y;
        public static float Cross(Vec2f a, Vec2f b) => a.X * b.Y - a.Y * b.X;
        public static float Distance(Vec2f a, Vec2f b) => (a - b).Length;
        public static Vec2f Lerp(Vec2f a, Vec2f b, float t) => new Vec2f(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        public bool Equals(Vec2f other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Vec2f v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X:0.###}, {Y:0.###})";
    }
}
