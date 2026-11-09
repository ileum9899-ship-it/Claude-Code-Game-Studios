using System;

namespace NeonHarbor.Core
{
    /// <summary>Small engine-free math helpers shared by the simulation.</summary>
    public static class Mathx
    {
        /// <summary>Full turn in radians.</summary>
        public const float Tau = (float)(Math.PI * 2);

        /// <summary>Clamps v to [a, b].</summary>
        public static float Clamp(float v, float a, float b) { return v < a ? a : v > b ? b : v; }

        /// <summary>Clamps v to [0, 1].</summary>
        public static float Clamp01(float v) { return Clamp(v, 0f, 1f); }

        /// <summary>Linear interpolation.</summary>
        public static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        /// <summary>Frame-rate independent exponential approach of a toward b with rate k.</summary>
        public static float Damp(float a, float b, float k, float dt) { return Lerp(a, b, 1f - (float)Math.Exp(-k * dt)); }

        /// <summary>Shortest signed angle from a to b, in (-π, π].</summary>
        public static float AngleDiff(float a, float b)
        {
            float d = (b - a) % Tau;
            if (d > Math.PI) d -= Tau;
            if (d < -Math.PI) d += Tau;
            return d;
        }

        /// <summary>Sign of v, 0 for 0.</summary>
        public static float Sign(float v) { return v > 0f ? 1f : v < 0f ? -1f : 0f; }
    }

    /// <summary>2D vector on the ground plane (X = world x, Y = world z).</summary>
    public struct Vec2
    {
        /// <summary>World x.</summary>
        public float X;
        /// <summary>World z.</summary>
        public float Y;

        /// <summary>Creates a vector.</summary>
        public Vec2(float x, float y) { X = x; Y = y; }

        /// <summary>Length.</summary>
        public float Length => (float)Math.Sqrt(X * X + Y * Y);

        /// <summary>Dot product.</summary>
        public float Dot(Vec2 o) { return X * o.X + Y * o.Y; }

        /// <summary>Unit vector in the same direction (zero stays zero).</summary>
        public Vec2 Normalized { get { float l = Length; return l > 1e-6f ? new Vec2(X / l, Y / l) : new Vec2(0, 0); } }

        /// <summary>Unit forward for a yaw (yaw 0 = +z, increasing yaw turns toward +x).</summary>
        public static Vec2 FromYaw(float yaw) { return new Vec2((float)Math.Sin(yaw), (float)Math.Cos(yaw)); }

        /// <summary>Yaw that points along this vector.</summary>
        public float ToYaw() { return (float)Math.Atan2(X, Y); }

        /// <summary>Distance between points.</summary>
        public static float Distance(Vec2 a, Vec2 b) { return (a - b).Length; }

        public static Vec2 operator +(Vec2 a, Vec2 b) { return new Vec2(a.X + b.X, a.Y + b.Y); }
        public static Vec2 operator -(Vec2 a, Vec2 b) { return new Vec2(a.X - b.X, a.Y - b.Y); }
        public static Vec2 operator *(Vec2 a, float s) { return new Vec2(a.X * s, a.Y * s); }

        /// <inheritdoc />
        public override string ToString() { return $"({X:0.##}, {Y:0.##})"; }
    }

    /// <summary>Deterministic PRNG (mulberry32) — same sequence as the browser build, so both build the same city.</summary>
    public sealed class Rng
    {
        private uint state;

        /// <summary>Creates a generator.</summary>
        public Rng(int seed) { state = (uint)seed; }

        /// <summary>Next value in [0, 1).</summary>
        public float Next()
        {
            unchecked
            {
                state += 0x6D2B79F5;
                uint t = state;
                t = (t ^ (t >> 15)) * (1 | t);
                t = (t + ((t ^ (t >> 7)) * (61 | t))) ^ t;
                float v = (float)((t ^ (t >> 14)) / 4294967296.0);
                return v < 1f ? v : 0.99999994f;
            }
        }

        /// <summary>Value in [a, b).</summary>
        public float Range(float a, float b) { return a + (b - a) * Next(); }

        /// <summary>Integer in [a, b] inclusive.</summary>
        public int RangeInt(int a, int b) { return a + (int)Math.Floor(Next() * (b - a + 1)); }

        /// <summary>Random element.</summary>
        public T Pick<T>(T[] items) { return items[(int)Math.Floor(Next() * items.Length) % items.Length]; }
    }
}
