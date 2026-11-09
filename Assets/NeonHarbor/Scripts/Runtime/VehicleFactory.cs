using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonHarbor.Runtime
{
    /// <summary>Vehicle archetypes.</summary>
    public enum CarType { Sedan, Sports, Suv, Taxi, Van, Police }

    /// <summary>Shape and handling of a vehicle archetype.</summary>
    public sealed class CarSpec
    {
        public CarType Type; public string Name; public float Length, Width, Wheel, Mass; public Vector2[] Profile; public int[] Paints;
    }

    /// <summary>
    /// Builds car meshes from an extruded side profile with wheel arches cut out,
    /// a glass band offset from the body, lights, bumpers and wheels. Port of the
    /// browser build's makeCar. Meshes are cached per type.
    /// </summary>
    public static class VehicleFactory
    {
        public static readonly Dictionary<CarType, CarSpec> Specs = new Dictionary<CarType, CarSpec>
        {
            [CarType.Sedan] = new CarSpec { Type = CarType.Sedan, Name = "세단", Length = 4.6f, Width = 1.85f, Wheel = 0.36f, Mass = 1f, Profile = P(-2.3f, 0.35f, 2.3f, 0.35f, 2.32f, 0.78f, 1.6f, 0.92f, 0.85f, 1.42f, -0.9f, 1.45f, -1.85f, 0.98f, -2.3f, 0.92f), Paints = new[] { 0x1d3b6e, 0xb8b8bc, 0x111114, 0x8a1d1d, 0xe6e6e6, 0x2e4d3a, 0x6b5b3e } },
            [CarType.Sports] = new CarSpec { Type = CarType.Sports, Name = "스포츠카", Length = 4.4f, Width = 1.92f, Wheel = 0.36f, Mass = 0.9f, Profile = P(-2.2f, 0.3f, 2.2f, 0.3f, 2.25f, 0.62f, 1.3f, 0.78f, 0.4f, 1.18f, -0.7f, 1.2f, -1.75f, 0.9f, -2.22f, 0.82f), Paints = new[] { 0xd61f1f, 0xf2c31b, 0x1f8fd6, 0x111114, 0xf06a14 } },
            [CarType.Suv] = new CarSpec { Type = CarType.Suv, Name = "SUV", Length = 4.9f, Width = 2.0f, Wheel = 0.44f, Mass = 1.35f, Profile = P(-2.45f, 0.45f, 2.45f, 0.45f, 2.48f, 1.0f, 1.85f, 1.12f, 1.25f, 1.8f, -2.25f, 1.85f, -2.45f, 1.2f), Paints = new[] { 0x2b2b2e, 0xdcdcdc, 0x3b4a2e, 0x5c1e1e, 0x23395c } },
            [CarType.Taxi] = new CarSpec { Type = CarType.Taxi, Name = "택시", Length = 4.6f, Width = 1.85f, Wheel = 0.36f, Mass = 1f, Profile = P(-2.3f, 0.35f, 2.3f, 0.35f, 2.32f, 0.78f, 1.6f, 0.92f, 0.85f, 1.42f, -0.9f, 1.45f, -1.85f, 0.98f, -2.3f, 0.92f), Paints = new[] { 0xf2b01e } },
            [CarType.Van] = new CarSpec { Type = CarType.Van, Name = "밴", Length = 5.2f, Width = 2.05f, Wheel = 0.4f, Mass = 1.6f, Profile = P(-2.6f, 0.45f, 2.6f, 0.45f, 2.62f, 1.05f, 2.1f, 1.3f, 1.6f, 2.15f, -2.6f, 2.2f), Paints = new[] { 0xe8e8e8, 0x2f5f8f, 0x8f2f2f } },
            [CarType.Police] = new CarSpec { Type = CarType.Police, Name = "경찰차", Length = 4.7f, Width = 1.9f, Wheel = 0.37f, Mass = 1.1f, Profile = P(-2.35f, 0.35f, 2.35f, 0.35f, 2.37f, 0.8f, 1.65f, 0.94f, 0.85f, 1.44f, -0.95f, 1.47f, -1.9f, 1.0f, -2.35f, 0.94f), Paints = new[] { 0x101318 } },
        };

        private static Vector2[] P(params float[] xy) { var o = new Vector2[xy.Length / 2]; for (int k = 0; k < o.Length; k++) o[k] = new Vector2(xy[k * 2], xy[k * 2 + 1]); return o; }

        private sealed class Meshes { public Mesh Body, Glass, Tire, Rim; public float Belt; }
        private static readonly Dictionary<CarType, Meshes> cache = new Dictionary<CarType, Meshes>();

        /// <summary>Visual parts of a spawned car (owned by <see cref="Vehicle"/>).</summary>
        public sealed class Parts
        {
            public Transform Root, Body; public Transform[] WheelPivots = new Transform[4], WheelSpins = new Transform[4];
            public MeshRenderer[] Tails = new MeshRenderer[2]; public MeshRenderer SirenRed, SirenBlue; public Transform Seat;
        }

        /// <summary>Builds a car GameObject.</summary>
        public static Parts Build(CarType type, Color paint, HarborMaterials mats, Transform parent)
        {
            var spec = Specs[type];
            var m = GetMeshes(spec);
            var parts = new Parts();
            var root = new GameObject(spec.Name).transform; root.SetParent(parent, false); parts.Root = root;
            var body = new GameObject("Body").transform; body.SetParent(root, false); parts.Body = body;
            Part(body, m.Body, mats.Paint(paint), Vector3.zero, Vector3.one);
            Part(body, m.Glass, mats.Glass, Vector3.zero, Vector3.one, receive: false);
            float L = spec.Length / 2, W = spec.Width / 2, yb = spec.Profile[0].y, hy = spec.Profile[2].y - 0.12f;
            var cube = MeshBuilder.Cube();
            Part(body, cube, mats.Under, new Vector3(0, yb + 0.05f, 0), new Vector3(spec.Width * 0.86f, 0.22f, spec.Length * 0.9f));
            Part(body, cube, mats.Chrome, new Vector3(0, yb + 0.1f, L + 0.06f), new Vector3(spec.Width * 0.94f, 0.2f, 0.14f));
            Part(body, cube, mats.Chrome, new Vector3(0, yb + 0.1f, -L - 0.06f), new Vector3(spec.Width * 0.94f, 0.2f, 0.14f));
            Part(body, cube, mats.Plain(new Color(0.9f, 0.89f, 0.85f)), new Vector3(0, yb + 0.34f, -L - 0.09f), new Vector3(spec.Width * 0.36f, 0.14f, 0.02f));
            Part(body, cube, mats.Chrome, new Vector3(0, hy - 0.08f, L + 0.09f), new Vector3(spec.Width * 0.5f, 0.2f, 0.04f));
            Part(body, cube, mats.HeadLight, new Vector3(-W + 0.36f, hy, L + 0.1f), new Vector3(0.42f, 0.13f, 0.05f), shadows: false);
            Part(body, cube, mats.HeadLight, new Vector3(W - 0.36f, hy, L + 0.1f), new Vector3(0.42f, 0.13f, 0.05f), shadows: false);
            parts.Tails[0] = Part(body, cube, mats.TailLight, new Vector3(-W + 0.36f, hy + 0.05f, -L - 0.1f), new Vector3(0.46f, 0.14f, 0.05f), shadows: false);
            parts.Tails[1] = Part(body, cube, mats.TailLight, new Vector3(W - 0.36f, hy + 0.05f, -L - 0.1f), new Vector3(0.46f, 0.14f, 0.05f), shadows: false);
            float mirY = (m.Belt + spec.Profile[2].y) / 2 + 0.05f, mirZ = spec.Profile.Length > 4 ? spec.Profile[4].x * 0.95f : 0.8f;
            Part(body, cube, mats.Chrome, new Vector3(-W - 0.08f, mirY, mirZ), new Vector3(0.18f, 0.1f, 0.12f));
            Part(body, cube, mats.Chrome, new Vector3(W + 0.08f, mirY, mirZ), new Vector3(0.18f, 0.1f, 0.12f));
            if (type == CarType.Taxi) Part(body, cube, mats.Glow(new Color(1f, 0.8f, 0.2f), 2f), new Vector3(0, 1.53f, -0.1f), new Vector3(0.6f, 0.16f, 0.24f), shadows: false);
            if (type == CarType.Police)
            {
                Part(body, cube, mats.Paint(new Color(0.95f, 0.95f, 0.95f)), new Vector3(-W - 0.05f, 0.78f, 0.1f), new Vector3(0.02f, 0.42f, 2f));
                Part(body, cube, mats.Paint(new Color(0.95f, 0.95f, 0.95f)), new Vector3(W + 0.05f, 0.78f, 0.1f), new Vector3(0.02f, 0.42f, 2f));
                Part(body, cube, mats.Chrome, new Vector3(0, 1.52f, -0.1f), new Vector3(1.2f, 0.08f, 0.3f));
                parts.SirenRed = Part(body, cube, mats.Glow(new Color(1, 0.05f, 0.05f), 0.4f), new Vector3(-0.3f, 1.62f, -0.1f), new Vector3(0.5f, 0.14f, 0.26f), shadows: false);
                parts.SirenBlue = Part(body, cube, mats.Glow(new Color(0.1f, 0.2f, 1f), 0.4f), new Vector3(0.3f, 1.62f, -0.1f), new Vector3(0.5f, 0.14f, 0.26f), shadows: false);
            }
            // headlight cone on the road (visible at night)
            var beam = new MeshBuilder();
            beam.Quad(new Vector3(-2.1f, 0, L + 11), new Vector3(2.1f, 0, L + 11), new Vector3(2.1f, 0, L + 0.2f), new Vector3(-2.1f, 0, L + 0.2f), Vector3.up,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0));
            var bgo = new GameObject("Beam"); bgo.transform.SetParent(root, false); bgo.transform.localPosition = new Vector3(0, 0.05f, 0);
            bgo.AddComponent<MeshFilter>().sharedMesh = beam.ToMesh("beam");
            var br = bgo.AddComponent<MeshRenderer>(); br.sharedMaterial = mats.Beam; br.shadowCastingMode = ShadowCastingMode.Off; br.receiveShadows = false;
            // wheels
            float wz = L - 0.82f;
            var wheels = new[] { new Vector3(-W + 0.2f, spec.Wheel, wz), new Vector3(W - 0.2f, spec.Wheel, wz), new Vector3(-W + 0.2f, spec.Wheel, -wz + 0.05f), new Vector3(W - 0.2f, spec.Wheel, -wz + 0.05f) };
            for (int k = 0; k < 4; k++)
            {
                var pivot = new GameObject("Wheel" + k).transform; pivot.SetParent(root, false); pivot.localPosition = wheels[k];
                var spin = new GameObject("Spin").transform; spin.SetParent(pivot, false);
                Part(spin, m.Tire, mats.Tire, Vector3.zero, Vector3.one);
                Part(spin, m.Rim, mats.Rim, Vector3.zero, Vector3.one);
                parts.WheelPivots[k] = pivot; parts.WheelSpins[k] = spin;
            }
            var seat = new GameObject("Seat").transform; seat.SetParent(body, false); seat.localPosition = new Vector3(-0.38f, -0.48f, -0.15f); parts.Seat = seat;
            return parts;
        }

        private static MeshRenderer Part(Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, bool shadows = true, bool receive = true)
        {
            var go = new GameObject(mesh.name); go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = receive;
            return r;
        }

        private static Meshes GetMeshes(CarSpec spec)
        {
            if (cache.TryGetValue(spec.Type, out var m)) return m;
            m = new Meshes();
            float L = spec.Length / 2, yb = spec.Profile[0].y, ra = spec.Wheel + 0.1f, xf = L - 0.82f, xr = -(L - 0.82f) + 0.05f;
            // silhouette (x = length, y = up) with wheel arches cut into the sill
            var outline = new List<Vector2> { new Vector2(spec.Profile[0].x, yb) };
            void Arch(float cx)
            {
                outline.Add(new Vector2(cx - ra, yb));
                for (int k = 1; k < 10; k++) { float a = Mathf.PI - k * Mathf.PI / 10; outline.Add(new Vector2(cx + Mathf.Cos(a) * ra, spec.Wheel + Mathf.Sin(a) * ra)); }
                outline.Add(new Vector2(cx + ra, yb));
            }
            Arch(xr); Arch(xf);
            for (int k = 1; k < spec.Profile.Length; k++) outline.Add(spec.Profile[k]);
            m.Body = Extrude(outline, spec.Width - 0.02f, "CarBody_" + spec.Type);

            // glass: silhouette grown outward, clipped between the belt line and just under the roof
            float minHigh = float.MaxValue, maxY = float.MinValue;
            foreach (var p in spec.Profile) { if (p.y > 0.6f) minHigh = Mathf.Min(minHigh, p.y); maxY = Mathf.Max(maxY, p.y); }
            m.Belt = Mathf.Lerp(minHigh, maxY, 0.42f);
            var grown = Offset(spec.Profile, 0.085f);
            var glass = ClipY(ClipY(grown, m.Belt, true), maxY - 0.07f, false);
            m.Glass = Extrude(glass, spec.Width - 0.06f, "CarGlass_" + spec.Type);

            var tire = new MeshBuilder(); tire.Add(MeshBuilder.Cylinder(20), Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, 90), new Vector3(spec.Wheel, 0.26f, spec.Wheel)));
            m.Tire = tire.ToMesh("Tire");
            var rim = new MeshBuilder(); rim.Add(MeshBuilder.Cylinder(10), Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, 90), new Vector3(spec.Wheel * 0.62f, 0.28f, spec.Wheel * 0.62f)));
            m.Rim = rim.ToMesh("Rim");
            cache[spec.Type] = m;
            return m;
        }

        /// <summary>Extrudes a 2D outline (x = forward, y = up) across the car width; flat-shaded.</summary>
        private static Mesh Extrude(List<Vector2> outline, float width, string name)
        {
            if (SignedArea(outline) < 0) outline.Reverse();
            var mb = new MeshBuilder();
            float hw = width / 2;
            Vector3 V(Vector2 p, float side) => new Vector3(side, p.y, p.x);
            var tris = Triangulate(outline);
            for (int k = 0; k < tris.Count; k += 3)
            {
                mb.TriAuto(V(outline[tris[k]], hw), V(outline[tris[k + 1]], hw), V(outline[tris[k + 2]], hw), Vector3.right);
                mb.TriAuto(V(outline[tris[k]], -hw), V(outline[tris[k + 1]], -hw), V(outline[tris[k + 2]], -hw), Vector3.left);
            }
            for (int k = 0; k < outline.Count; k++)
            {
                Vector2 a = outline[k], b = outline[(k + 1) % outline.Count], e = b - a;
                if (e.sqrMagnitude < 1e-8f) continue;
                Vector2 n2 = new Vector2(e.y, -e.x).normalized; // outward for a CCW outline
                mb.QuadAuto(V(a, -hw), V(b, -hw), V(b, hw), V(a, hw), new Vector3(0, n2.y, n2.x));
            }
            return mb.ToMesh(name);
        }

        private static float SignedArea(List<Vector2> p)
        {
            float a = 0; for (int k = 0; k < p.Count; k++) { var q = p[k]; var r = p[(k + 1) % p.Count]; a += q.x * r.y - r.x * q.y; } return a / 2;
        }

        /// <summary>Ear-clipping triangulation of a simple CCW polygon.</summary>
        private static List<int> Triangulate(List<Vector2> pts)
        {
            var idx = new List<int>(); for (int k = 0; k < pts.Count; k++) idx.Add(k);
            var result = new List<int>();
            int guard = 0;
            while (idx.Count > 3 && guard++ < 5000)
            {
                bool clipped = false;
                for (int k = 0; k < idx.Count; k++)
                {
                    int i0 = idx[(k - 1 + idx.Count) % idx.Count], i1 = idx[k], i2 = idx[(k + 1) % idx.Count];
                    Vector2 a = pts[i0], b = pts[i1], c = pts[i2];
                    if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) <= 1e-9f) continue; // reflex
                    bool inside = false;
                    foreach (int j in idx)
                    {
                        if (j == i0 || j == i1 || j == i2) continue;
                        if (PointInTri(pts[j], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    result.Add(i0); result.Add(i1); result.Add(i2); idx.RemoveAt(k); clipped = true; break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]); }
            return result;
        }

        private static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        private static List<Vector2> Offset(Vector2[] p, float d)
        {
            var o = new List<Vector2>(); int n = p.Length;
            for (int k = 0; k < n; k++)
            {
                Vector2 a = p[(k - 1 + n) % n], b = p[k], c = p[(k + 1) % n];
                Vector2 n1 = new Vector2(b.y - a.y, -(b.x - a.x)).normalized, n2 = new Vector2(c.y - b.y, -(c.x - b.x)).normalized;
                o.Add(b + (n1 + n2).normalized * d);
            }
            return o;
        }

        private static List<Vector2> ClipY(List<Vector2> poly, float y0, bool keepAbove)
        {
            var o = new List<Vector2>();
            for (int k = 0; k < poly.Count; k++)
            {
                Vector2 a = poly[k], b = poly[(k + 1) % poly.Count];
                bool ain = keepAbove ? a.y >= y0 : a.y <= y0, bin = keepAbove ? b.y >= y0 : b.y <= y0;
                if (ain) o.Add(a);
                if (ain != bin) { float u = (y0 - a.y) / (b.y - a.y); o.Add(new Vector2(a.x + (b.x - a.x) * u, y0)); }
            }
            return o;
        }
    }
}
