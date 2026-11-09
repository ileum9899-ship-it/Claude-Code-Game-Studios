using NeonHarbor.Core;
using UnityEngine;

namespace NeonHarbor.Runtime
{
    /// <summary>
    /// Generates every texture at startup (facades with lit-window emission maps,
    /// asphalt, concrete, grass, roof, water normals, glow sprites). Port of the
    /// browser build's canvas textures. Call once during load; not for per-frame use.
    /// </summary>
    public static class ProceduralTextures
    {
        /// <summary>Material response of a facade style.</summary>
        public struct Spec { public float rough, metal; }

        private struct Style
        {
            public Color32 wall, frame; public Color32[] win, lit; public float rough, metal, litChance; public int mullion; public bool brick, ribbed, glass;
        }

        private static Style Get(FacadeStyle s)
        {
            switch (s)
            {
                case FacadeStyle.Glass: return new Style { wall = H(0x3d4f63), frame = H(0x8fa3b5), win = new[] { H(0x1d2b3a), H(0x24384d), H(0x2b4560) }, rough = 0.12f, metal = 0.65f, mullion = 2, litChance = 0.42f, lit = new[] { H(0xffe2a8), H(0xfff3d6), H(0xcfe7ff) }, glass = true };
                case FacadeStyle.Office: return new Style { wall = H(0x9aa1a6), frame = H(0x5b6268), win = new[] { H(0x22303d), H(0x2b3947), H(0x1c2733) }, rough = 0.45f, metal = 0.25f, mullion = 4, litChance = 0.36f, lit = new[] { H(0xffe2a8), H(0xfff6e0) } };
                case FacadeStyle.Brick: return new Style { wall = H(0x8a4a36), frame = H(0xd8cfc0), win = new[] { H(0x1f2a33), H(0x26323c) }, rough = 0.9f, metal = 0f, mullion = 6, litChance = 0.3f, lit = new[] { H(0xffcf7a), H(0xffd99a) }, brick = true };
                case FacadeStyle.Concrete: return new Style { wall = H(0xb3ada3), frame = H(0x6f6a62), win = new[] { H(0x232c33), H(0x2c3640) }, rough = 0.85f, metal = 0f, mullion = 5, litChance = 0.3f, lit = new[] { H(0xffd88f), H(0xfff0cc) } };
                case FacadeStyle.Plaster: return new Style { wall = H(0xd9c9a8), frame = H(0xf2ece0), win = new[] { H(0x26323b), H(0x2f3d48) }, rough = 0.9f, metal = 0f, mullion = 7, litChance = 0.34f, lit = new[] { H(0xffc874), H(0xffdca0) } };
                default: return new Style { wall = H(0x6f7b80), frame = H(0x4c5559), win = new[] { H(0x2a3135) }, rough = 0.7f, metal = 0.4f, mullion = 16, litChance = 0.15f, lit = new[] { H(0xffe7b0) }, ribbed = true };
            }
        }

        /// <summary>Roughness / metalness for a style.</summary>
        public static Spec FacadeSpec(FacadeStyle s) { var st = Get(s); return new Spec { rough = st.rough, metal = st.metal }; }

        /// <summary>One facade tile: 4 window columns × 4 floors. Returns albedo and emission (lit windows).</summary>
        public static (Texture2D albedo, Texture2D emission) Facade(FacadeStyle s)
        {
            const int W = 256, Hh = 256, cw = W / 4, fh = Hh / 4;
            var st = Get(s);
            var rnd = new System.Random(17 + (int)s);
            var a = new Color32[W * Hh];
            var e = new Color32[W * Hh];
            for (int y = 0; y < Hh; y++)
                for (int x = 0; x < W; x++)
                {
                    Color32 c = st.wall;
                    if (st.brick)
                    {
                        int row = y / 4, off = (row % 2) * 4;
                        bool mortar = y % 4 == 0 || (x + off) % 8 == 0;
                        float k = 0.85f + (float)((x * 31 + row * 17) % 7) / 20f;
                        c = mortar ? new Color32(150, 140, 128, 255) : Scale(st.wall, k);
                    }
                    if (st.ribbed && x % 4 < 2) c = Scale(c, 0.82f);
                    c = Scale(c, 0.92f + (float)rnd.NextDouble() * 0.16f);
                    a[y * W + x] = c;
                    e[y * W + x] = new Color32(0, 0, 0, 255);
                }
            for (int fy = 0; fy < 4; fy++)
                for (int cx = 0; cx < 4; cx++)
                {
                    if (st.ribbed && fy != 1) continue;
                    int m = st.mullion, x0 = cx * cw + m, y0 = fy * fh + m + 2, w = cw - m * 2, h = fh - m * 2 - (st.glass ? 0 : 6);
                    Fill(a, W, x0 - 2, y0 - 2, w + 4, h + 4, st.frame);
                    Color32 wc = st.win[rnd.Next(st.win.Length)];
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            float g = 1f - (x + y) / (float)(w + h) * 0.55f;
                            Color32 c = Scale(wc, g);
                            if (x < w * 0.45f) c = Lighten(c, 0.08f); // sky reflection streak
                            a[(y0 + y) * W + x0 + x] = c;
                        }
                    if (!st.glass) Fill(a, W, x0 - 2, y0 - 6, w + 4, 3, Scale(st.wall, 0.7f)); // sill shadow (v up)
                    if (rnd.NextDouble() < st.litChance)
                    {
                        Color32 lc = st.lit[rnd.Next(st.lit.Length)];
                        float k = 0.35f + (float)rnd.NextDouble() * 0.5f;
                        int curtainX = (int)(w * rnd.NextDouble() * 0.6f), curtainW = rnd.NextDouble() < 0.5 ? (int)(w * 0.3f) : 0;
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++)
                            {
                                bool curtain = x >= curtainX && x < curtainX + curtainW;
                                e[(y0 + y) * W + x0 + x] = Scale(lc, curtain ? k * 0.45f : k);
                            }
                    }
                }
            if (st.glass) for (int fy = 0; fy < 4; fy++) Fill(a, W, 0, fy * fh, W, 4, H(0x56687a));
            return (Make(a, W, Hh, true), Make(e, W, Hh, true));
        }

        /// <summary>Asphalt with grain and tar cracks.</summary>
        public static Texture2D Asphalt() { return Noise(256, H(0x3a3c3f), 0.22f, 3, true); }
        /// <summary>Sidewalk concrete with joints every 2 m.</summary>
        public static Texture2D Concrete()
        {
            var t = Noise(128, H(0xa7a49d), 0.12f, 5, false);
            var p = t.GetPixels32();
            for (int k = 0; k < 128; k++) { p[k * 128] = p[k] = new Color32(110, 108, 100, 255); }
            t.SetPixels32(p); t.Apply(true); return t;
        }
        /// <summary>Lawn grass.</summary>
        public static Texture2D Grass() { return Noise(128, H(0x4d7a35), 0.3f, 7, false); }
        /// <summary>Tar roof.</summary>
        public static Texture2D Roof() { return Noise(128, H(0x4a4c4f), 0.2f, 9, false); }

        /// <summary>Tileable water normal map from summed sines.</summary>
        public static Texture2D WaterNormals()
        {
            const int N = 128;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, name = "WaterNormals" };
            var p = new Color32[N * N];
            float Ht(float x, float y) => Mathf.Sin(x * Mathf.PI * 2 * 3 / N) * 0.5f + Mathf.Sin(y * Mathf.PI * 2 * 5 / N) * 0.4f + Mathf.Sin((x + y) * Mathf.PI * 2 * 4 / N) * 0.3f + Mathf.Sin((x - 2 * y) * Mathf.PI * 2 * 2 / N) * 0.2f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = Ht(x + 1, y) - Ht(x - 1, y), dy = Ht(x, y + 1) - Ht(x, y - 1);
                    var n = new Vector3(-dx * 2f, -dy * 2f, 1f).normalized;
                    p[y * N + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255), (byte)((n.y * 0.5f + 0.5f) * 255), (byte)((n.z * 0.5f + 0.5f) * 255), 255);
                }
            t.SetPixels32(p); t.Apply(true); return t;
        }

        /// <summary>Soft round glow (light pools on the ground).</summary>
        public static Texture2D RadialGlow()
        {
            const int N = 64; var p = new Color32[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(N / 2f, N / 2f)) / (N / 2f));
                    byte v = (byte)(Mathf.Pow(1 - d, 1.6f) * 230);
                    p[y * N + x] = new Color32(v, v, v, v);
                }
            return Make(p, N, N, false, TextureWrapMode.Clamp);
        }

        /// <summary>Headlight cone on the road (narrow at v = 0, the car end).</summary>
        public static Texture2D Beam()
        {
            const int W = 32, Hh = 64; var p = new Color32[W * Hh];
            for (int y = 0; y < Hh; y++) for (int x = 0; x < W; x++)
                {
                    float v = y / (float)Hh, half = Mathf.Lerp(0.18f, 0.5f, v), u = Mathf.Abs(x / (float)W - 0.5f);
                    float a = u < half ? (1 - v) * (1 - u / half * 0.6f) : 0;
                    byte b = (byte)(a * 230);
                    p[y * W + x] = new Color32(b, b, b, b);
                }
            return Make(p, W, Hh, false, TextureWrapMode.Clamp);
        }

        private static Texture2D Noise(int n, Color32 baseColor, float amount, int seed, bool cracks)
        {
            var rnd = new System.Random(seed);
            var p = new Color32[n * n];
            for (int k = 0; k < p.Length; k++) p[k] = Scale(baseColor, 1f + ((float)rnd.NextDouble() - 0.5f) * amount * 2);
            if (cracks)
                for (int c = 0; c < 10; c++)
                {
                    float x = rnd.Next(n), y = rnd.Next(n);
                    for (int s = 0; s < 40; s++)
                    {
                        x += (float)rnd.NextDouble() * 2 - 1; y += (float)rnd.NextDouble() * 2 - 1;
                        int ix = ((int)x % n + n) % n, iy = ((int)y % n + n) % n; p[iy * n + ix] = Scale(baseColor, 0.55f);
                    }
                }
            return Make(p, n, n, true);
        }

        private static Texture2D Make(Color32[] p, int w, int h, bool srgb, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true, !srgb) { wrapMode = wrap, anisoLevel = 8, filterMode = FilterMode.Trilinear };
            t.SetPixels32(p); t.Apply(true, false);
            return t;
        }

        private static void Fill(Color32[] p, int W, int x, int y, int w, int h, Color32 c)
        {
            for (int yy = Mathf.Max(0, y); yy < Mathf.Min(W, y + h); yy++)
                for (int xx = Mathf.Max(0, x); xx < Mathf.Min(W, x + w); xx++) p[yy * W + xx] = c;
        }

        private static Color32 Scale(Color32 c, float k) { return new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), 255); }
        private static Color32 Lighten(Color32 c, float k) { return new Color32((byte)Mathf.Min(255, c.r + 255 * k), (byte)Mathf.Min(255, c.g + 255 * k), (byte)Mathf.Min(255, c.b + 255 * k), 255); }
        private static Color32 H(int hex) { return new Color32((byte)((hex >> 16) & 255), (byte)((hex >> 8) & 255), (byte)(hex & 255), 255); }
    }
}
