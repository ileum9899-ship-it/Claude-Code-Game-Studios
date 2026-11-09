using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonHarbor.Runtime
{
    /// <summary>
    /// Accumulates triangles (with world-space UVs for facades) into one mesh.
    /// Used to merge a whole city block per material into a single renderer.
    /// Single-thread only; allocate during level build, not per frame.
    /// </summary>
    /// <example><code>
    /// var mb = new MeshBuilder();
    /// mb.Box(new Vector3(0,0,0), new Vector3(10,30,10), 4f, 3.5f, 1f);
    /// var go = mb.ToGameObject("Tower", material, parent);
    /// </code></example>
    public sealed class MeshBuilder
    {
        private readonly List<Vector3> v = new List<Vector3>();
        private readonly List<Vector3> n = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<int> tri = new List<int>();

        /// <summary>True when nothing has been added.</summary>
        public bool IsEmpty => tri.Count == 0;

        /// <summary>
        /// Adds a quad a,b,c,d. Order the corners clockwise as seen from the side the normal
        /// points to (Unity's front-face winding); every caller in this file does.
        /// </summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal);
            uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
            tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
        }

        /// <summary>Adds a triangle, flipping its winding if needed so it faces <paramref name="normal"/>.</summary>
        public void TriAuto(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0) { var t = b; b = c; c = t; }
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            n.Add(normal); n.Add(normal); n.Add(normal);
            uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.up);
            tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
        }

        /// <summary>Adds a quad, auto-oriented toward <paramref name="normal"/>.</summary>
        public void QuadAuto(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            TriAuto(a, b, c, normal); TriAuto(a, c, d, normal);
        }

        /// <summary>Axis-aligned box from min to max. Side UVs = metres / (us, vs); top UV = metres / ts.</summary>
        public void Box(Vector3 min, Vector3 max, float us = 1, float vs = 1, float ts = 1, bool sides = true, bool top = true, bool bottom = false)
        {
            float x0 = min.x, y0 = min.y, z0 = min.z, x1 = max.x, y1 = max.y, z1 = max.z;
            float vy0 = y0 / vs, vy1 = y1 / vs;
            if (sides)
            {
                Quad(new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), Vector3.forward,
                    new Vector2(-x0 / us, vy0), new Vector2(-x1 / us, vy0), new Vector2(-x1 / us, vy1), new Vector2(-x0 / us, vy1));
                Quad(new Vector3(x1, y0, z0), new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), Vector3.back,
                    new Vector2(x1 / us, vy0), new Vector2(x0 / us, vy0), new Vector2(x0 / us, vy1), new Vector2(x1 / us, vy1));
                Quad(new Vector3(x1, y0, z1), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), Vector3.right,
                    new Vector2(z1 / us, vy0), new Vector2(z0 / us, vy0), new Vector2(z0 / us, vy1), new Vector2(z1 / us, vy1));
                Quad(new Vector3(x0, y0, z0), new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), Vector3.left,
                    new Vector2(-z0 / us, vy0), new Vector2(-z1 / us, vy0), new Vector2(-z1 / us, vy1), new Vector2(-z0 / us, vy1));
            }
            if (top)
                Quad(new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), Vector3.up,
                    new Vector2(x0 / ts, z1 / ts), new Vector2(x1 / ts, z1 / ts), new Vector2(x1 / ts, z0 / ts), new Vector2(x0 / ts, z0 / ts));
            if (bottom)
                Quad(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), Vector3.down,
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
        }

        /// <summary>Horizontal rectangle at height y.</summary>
        public void Flat(float x0, float z0, float x1, float z1, float y, float ts = 1)
        {
            Quad(new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), new Vector3(x0, y, z0), Vector3.up,
                new Vector2(x0 / ts, z1 / ts), new Vector2(x1 / ts, z1 / ts), new Vector2(x1 / ts, z0 / ts), new Vector2(x0 / ts, z0 / ts));
        }

        /// <summary>Appends another mesh transformed by a matrix.</summary>
        public void Add(Mesh mesh, Matrix4x4 m)
        {
            int baseIndex = v.Count;
            var mv = mesh.vertices; var mn = mesh.normals; var mu = mesh.uv; var mt = mesh.triangles;
            Matrix4x4 nm = m.inverse.transpose;
            for (int k = 0; k < mv.Length; k++)
            {
                v.Add(m.MultiplyPoint3x4(mv[k]));
                n.Add(nm.MultiplyVector(mn[k]).normalized);
                uv.Add(mu.Length > k ? mu[k] : Vector2.zero);
            }
            for (int k = 0; k < mt.Length; k++) tri.Add(baseIndex + mt[k]);
        }

        /// <summary>Builds the mesh (32-bit indices for big blocks).</summary>
        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Creates a static renderer for the accumulated geometry (null when empty).</summary>
        public GameObject ToGameObject(string name, Material material, Transform parent, bool castShadows = true)
        {
            if (IsEmpty) return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ToMesh(name);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            go.isStatic = true;
            return go;
        }

        // ------------------------------------------------------------------ procedural primitives
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>Unit cylinder (radius 1, height 1, centred) with optional top taper.</summary>
        public static Mesh Cylinder(int segments = 12, float topScale = 1f)
        {
            string key = "cyl" + segments + "_" + topScale;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var mb = new MeshBuilder();
            for (int s = 0; s < segments; s++)
            {
                float a0 = s * Mathf.PI * 2 / segments, a1 = (s + 1) * Mathf.PI * 2 / segments;
                Vector3 p0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)), p1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1));
                Vector3 nn = ((p0 + p1) * 0.5f).normalized;
                mb.Quad(p0 + Vector3.down * 0.5f, p1 + Vector3.down * 0.5f, p1 * topScale + Vector3.up * 0.5f, p0 * topScale + Vector3.up * 0.5f, nn,
                    new Vector2(s / (float)segments, 0), new Vector2((s + 1f) / segments, 0), new Vector2((s + 1f) / segments, 1), new Vector2(s / (float)segments, 1));
                // caps as degenerate quads (triangle fans)
                mb.Quad(Vector3.up * 0.5f, p0 * topScale + Vector3.up * 0.5f, p1 * topScale + Vector3.up * 0.5f, Vector3.up * 0.5f, Vector3.up, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                mb.Quad(Vector3.down * 0.5f, p1 + Vector3.down * 0.5f, p0 + Vector3.down * 0.5f, Vector3.down * 0.5f, Vector3.down, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            }
            return Cache[key] = mb.ToMesh(key);
        }

        /// <summary>Unit sphere (radius 1), faceted when <paramref name="flat"/> (low-poly foliage look).</summary>
        public static Mesh Sphere(int rings = 8, int segments = 12, bool flat = false)
        {
            string key = "sph" + rings + "_" + segments + flat;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var mb = new MeshBuilder();
            for (int r = 0; r < rings; r++)
            {
                float t0 = r * Mathf.PI / rings, t1 = (r + 1) * Mathf.PI / rings;
                for (int s = 0; s < segments; s++)
                {
                    float a0 = s * Mathf.PI * 2 / segments, a1 = (s + 1) * Mathf.PI * 2 / segments;
                    Vector3 P(float t, float a) => new Vector3(Mathf.Sin(t) * Mathf.Sin(a), Mathf.Cos(t), Mathf.Sin(t) * Mathf.Cos(a));
                    Vector3 a = P(t0, a0), b = P(t0, a1), c = P(t1, a1), d = P(t1, a0);
                    Vector3 nn = flat ? ((a + b + c + d) * 0.25f).normalized : ((a + c) * 0.5f).normalized;
                    // seen from outside, b is upper-left, a upper-right, d lower-right, c lower-left (clockwise)
                    mb.Quad(b, a, d, c, nn, new Vector2((s + 1f) / segments, 1 - r / (float)rings), new Vector2(s / (float)segments, 1 - r / (float)rings),
                        new Vector2(s / (float)segments, 1 - (r + 1f) / rings), new Vector2((s + 1f) / segments, 1 - (r + 1f) / rings));
                }
            }
            var mesh = mb.ToMesh(key);
            if (!flat) mesh.RecalculateNormals();
            return Cache[key] = mesh;
        }

        /// <summary>Unit cube (1×1×1, centred).</summary>
        public static Mesh Cube()
        {
            if (Cache.TryGetValue("cube", out var cached)) return cached;
            var mb = new MeshBuilder();
            mb.Box(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f), 1, 1, 1, true, true, true);
            return Cache["cube"] = mb.ToMesh("cube");
        }
    }
}
