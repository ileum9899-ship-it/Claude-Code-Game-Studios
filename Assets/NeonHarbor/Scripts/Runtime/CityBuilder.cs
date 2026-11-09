using System.Collections.Generic;
using NeonHarbor.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonHarbor.Runtime
{
    /// <summary>
    /// Turns a <see cref="CityLayout"/> into renderers: roads and markings, raised
    /// sidewalks, merged building volumes per block and material, street furniture,
    /// parks, the harbour (cranes, containers, water) and neon signs.
    /// Also registers tree trunks and lamp posts as colliders. Build once at load.
    /// </summary>
    public sealed class CityBuilder
    {
        private readonly CityLayout city;
        private readonly HarborMaterials mats;
        private readonly Transform root;
        private readonly System.Random rnd = new System.Random(4242);
        private readonly Mesh cube = MeshBuilder.Cube(), cyl = MeshBuilder.Cylinder(10), sph = MeshBuilder.Sphere(5, 8, true), ball = MeshBuilder.Sphere(6, 10);

        /// <summary>Ground positions of street lamps (for light pools).</summary>
        public readonly List<Vector3> LampPoints = new List<Vector3>();
        /// <summary>Neon sign renderers (their emission is animated at night).</summary>
        public readonly List<(Material material, Color color)> NeonSigns = new List<(Material, Color)>();

        /// <summary>Creates the builder.</summary>
        public CityBuilder(CityLayout city, HarborMaterials mats, Transform root) { this.city = city; this.mats = mats; this.root = root; }

        private float R(float a, float b) { return a + (b - a) * (float)rnd.NextDouble(); }

        /// <summary>Builds everything.</summary>
        public void Build()
        {
            float E0 = -city.HalfRoad, E1 = city.Size + city.HalfRoad, Q = CityLayout.Quay;
            var road = new MeshBuilder();
            road.Flat(E0, E0, E1, E1, 0f, 9f);
            road.ToGameObject("Roads", mats.Asphalt, root, false);
            var quay = new MeshBuilder();
            quay.Box(new Vector3(E1, -4, E0), new Vector3(E1 + Q, 0.18f, E1 + Q), 4, 4, 6);
            quay.Box(new Vector3(E0, -4, E1), new Vector3(E1, 0.18f, E1 + Q), 4, 4, 6);
            quay.Box(new Vector3(E0 - 1.5f, -4, E0 - 1.5f), new Vector3(E0, 0.6f, E1 + Q), 4, 4, 6);
            quay.Box(new Vector3(E0, -4, E0 - 1.5f), new Vector3(E1 + Q, 0.6f, E0), 4, 4, 6);
            quay.Box(new Vector3(E1 + Q, -4, E0 - 1.5f), new Vector3(E1 + Q + 1.5f, 0.6f, E1 + Q + 1.5f), 4, 4, 6);
            quay.Box(new Vector3(E0 - 1.5f, -4, E1 + Q), new Vector3(E1 + Q, 0.6f, E1 + Q + 1.5f), 4, 4, 6);
            quay.ToGameObject("Quay", mats.Quay, root);
            BuildMarkings();
            foreach (var blk in city.Blocks) BuildBlock(blk);
            BuildHarbor(E1);
            BuildWater();
            BuildLightPools();
            StaticBatchingUtility.Combine(root.gameObject);
        }

        private void BuildMarkings()
        {
            var w = new MeshBuilder(); var y = new MeshBuilder();
            const float Y = 0.012f;
            float H = city.HalfRoad;
            for (int k = 0; k <= city.N; k++)
                for (int s = 0; s < city.N; s++)
                {
                    float a = s * city.Pitch + H, b = (s + 1) * city.Pitch - H, c = k * city.Pitch;
                    y.Flat(a + 4, c - 0.22f, b - 4, c - 0.08f, Y); y.Flat(a + 4, c + 0.08f, b - 4, c + 0.22f, Y);
                    y.Flat(c - 0.22f, a + 4, c - 0.08f, b - 4, Y); y.Flat(c + 0.08f, a + 4, c + 0.22f, b - 4, Y);
                    foreach (int side in new[] { -1, 1 })
                    {
                        float e = c + side * (H - 0.6f);
                        w.Flat(a + 4, e - 0.08f, b - 4, e + 0.08f, Y); w.Flat(e - 0.08f, a + 4, e + 0.08f, b - 4, Y);
                    }
                    foreach (var (end, dir) in new[] { (a, 1f), (b, -1f) })
                    {
                        float p0 = Mathf.Min(end + dir * 0.5f, end + dir * 3.3f), p1 = Mathf.Max(end + dir * 0.5f, end + dir * 3.3f);
                        for (float t = -H + 0.8f; t < H - 0.8f; t += 1.2f) { w.Flat(p0, c + t, p1, c + t + 0.6f, Y); w.Flat(c + t, p0, c + t + 0.6f, p1, Y); }
                        float s0 = Mathf.Min(end + dir * 3.8f, end + dir * 4.3f), s1 = Mathf.Max(end + dir * 3.8f, end + dir * 4.3f);
                        // stop lines across the incoming lane (drive on the right: lane is on the travel-right side)
                        w.Flat(s0, dir > 0 ? c : c - H + 0.6f, s1, dir > 0 ? c + H - 0.6f : c, Y);
                        w.Flat(dir > 0 ? c - H + 0.6f : c, s0, dir > 0 ? c : c + H - 0.6f, s1, Y);
                    }
                }
            w.ToGameObject("MarkingsWhite", mats.MarkWhite, root, false);
            y.ToGameObject("MarkingsYellow", mats.MarkYellow, root, false);
        }

        private void BuildBlock(Block blk)
        {
            var parent = new GameObject($"Block_{blk.I}_{blk.J}_{blk.Type}").transform;
            parent.SetParent(root, false);
            var slab = new MeshBuilder(); var curb = new MeshBuilder(); var lot = new MeshBuilder(); var roof = new MeshBuilder();
            var metal = new MeshBuilder(); var metalLight = new MeshBuilder(); var lamp = new MeshBuilder(); var trunk = new MeshBuilder(); var blink = new MeshBuilder();
            var leaves = new[] { new MeshBuilder(), new MeshBuilder(), new MeshBuilder() };
            var fac = new Dictionary<FacadeStyle, MeshBuilder>();
            float SW = city.SidewalkWidth;
            slab.Box(new Vector3(blk.X0, 0, blk.Z0), new Vector3(blk.X1, 0.18f, blk.Z1), 4, 4, 4, sides: false);
            curb.Box(new Vector3(blk.X0, 0, blk.Z0), new Vector3(blk.X1, 0.18f, blk.Z1), 4, 4, 4, top: false);
            float ix0 = blk.X0 + SW, iz0 = blk.Z0 + SW, ix1 = blk.X1 - SW, iz1 = blk.Z1 - SW;
            if (blk.Type == BlockType.Park || blk.Type == BlockType.Residential) lot.Flat(ix0, iz0, ix1, iz1, 0.185f, 6);

            void Tree(float x, float z, float s)
            {
                city.AddCollider(new Box2(x - 0.3f, z - 0.3f, x + 0.3f, z + 0.3f, 2.2f));
                trunk.Add(cyl, Matrix4x4.TRS(new Vector3(x, 1.5f * s + 0.18f, z), Quaternion.identity, new Vector3(0.2f * s, 3 * s, 0.2f * s)));
                leaves[rnd.Next(3)].Add(sph, Matrix4x4.TRS(new Vector3(x, 3.6f * s + 0.18f, z), Quaternion.Euler(0, R(0, 360), 0), new Vector3(1.9f, 1.6f, 1.9f) * s));
                leaves[rnd.Next(3)].Add(sph, Matrix4x4.TRS(new Vector3(x + R(-0.6f, 0.6f) * s, 4.7f * s + 0.18f, z + R(-0.6f, 0.6f) * s), Quaternion.Euler(0, R(0, 360), 0), new Vector3(1.3f, 1.2f, 1.3f) * s));
            }

            foreach (var b in blk.Buildings)
            {
                if (!fac.TryGetValue(b.Style, out var fb)) fac[b.Style] = fb = new MeshBuilder();
                float us = b.Style == FacadeStyle.Glass || b.Style == FacadeStyle.Warehouse ? 16 : 11, vs = b.Style == FacadeStyle.Glass ? 14 : 12;
                foreach (var t in b.Tiers)
                {
                    fb.Box(new Vector3(t.X0, t.Y0 == 0 ? 0.18f : t.Y0, t.Z0), new Vector3(t.X1, t.Y1, t.Z1), us, vs, 1, top: false);
                    roof.Box(new Vector3(t.X0 - 0.3f, t.Y1, t.Z0 - 0.3f), new Vector3(t.X1 + 0.3f, t.Y1 + 0.9f, t.Z1 + 0.3f), 4, 4, 6);
                }
                var top = b.Tiers[b.Tiers.Count - 1]; float ty = top.Y1 + 0.9f;
                float tw = top.X1 - top.X0, td = top.Z1 - top.Z0;
                if (b.Style != FacadeStyle.Warehouse)
                    for (int k = 0; k < Mathf.Min(6, (int)(tw * td / 60)); k++)
                    {
                        var size = new Vector3(R(1.2f, 2.6f), R(0.8f, 1.6f), R(1.2f, 2.6f));
                        var p = new Vector3(R(top.X0 + 1.5f, top.X1 - 1.5f), ty + size.y / 2, R(top.Z0 + 1.5f, top.Z1 - 1.5f));
                        metalLight.Add(cube, Matrix4x4.TRS(p, Quaternion.identity, size));
                    }
                if ((b.Style == FacadeStyle.Brick || b.Style == FacadeStyle.Concrete) && rnd.NextDouble() < 0.55)
                {
                    float tx = R(top.X0 + 3, top.X1 - 3), tz = R(top.Z0 + 3, top.Z1 - 3);
                    trunk.Add(cyl, Matrix4x4.TRS(new Vector3(tx, ty + 3.2f, tz), Quaternion.identity, new Vector3(3.2f, 2.6f, 3.2f)));
                    foreach (var (dx, dz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                        metalLight.Add(cyl, Matrix4x4.TRS(new Vector3(tx + dx, ty + 1, tz + dz), Quaternion.identity, new Vector3(0.24f, 2, 0.24f)));
                }
                if (top.Y1 > 70)
                {
                    float ax = (top.X0 + top.X1) / 2, az = (top.Z0 + top.Z1) / 2, ah = R(8, 22);
                    metalLight.Add(cyl, Matrix4x4.TRS(new Vector3(ax, ty + ah / 2, az), Quaternion.identity, new Vector3(0.3f, ah, 0.3f)));
                    blink.Add(ball, Matrix4x4.TRS(new Vector3(ax, ty + ah + 0.3f, az), Quaternion.identity, Vector3.one * 0.35f));
                }
            }

            // sidewalk furniture along the four edges
            var edges = new[] { (blk.X0, blk.Z0, blk.X1, blk.Z0, 0f, -1f), (blk.X0, blk.Z1, blk.X1, blk.Z1, 0f, 1f), (blk.X0, blk.Z0, blk.X0, blk.Z1, -1f, 0f), (blk.X1, blk.Z0, blk.X1, blk.Z1, 1f, 0f) };
            foreach (var (x0, z0, x1, z1, nx, nz) in edges)
            {
                float len = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0)), tx = (x1 - x0) / len, tz = (z1 - z0) / len;
                for (float s = 6; s < len - 4; s += 11)
                {
                    float px = x0 + tx * s - nx * 0.7f, pz = z0 + tz * s - nz * 0.7f;
                    int k = Mathf.RoundToInt((s - 6) / 11);
                    if (k % 2 == 0)
                    {
                        var rot = Quaternion.LookRotation(new Vector3(nx, 0, nz));
                        metal.Add(cyl, Matrix4x4.TRS(new Vector3(px, 3.68f, pz), Quaternion.identity, new Vector3(0.22f, 7, 0.22f)));
                        metal.Add(cube, Matrix4x4.TRS(new Vector3(px + nx, 7.1f, pz + nz), rot, new Vector3(0.12f, 0.12f, 2.2f)));
                        lamp.Add(cube, Matrix4x4.TRS(new Vector3(px + nx * 2, 7f, pz + nz * 2), rot, new Vector3(0.5f, 0.18f, 0.9f)));
                        LampPoints.Add(new Vector3(px + nx * 2, 0, pz + nz * 2));
                        city.AddCollider(new Box2(px - 0.15f, pz - 0.15f, px + 0.15f, pz + 0.15f, 7));
                    }
                    else if (blk.Type != BlockType.Harbor && blk.Type != BlockType.Downtown) Tree(px - nx * 0.6f, pz - nz * 0.6f, R(0.85f, 1.15f));
                    else if (k % 4 == 1) metal.Add(cyl, Matrix4x4.TRS(new Vector3(px, 0.63f, pz), Quaternion.identity, new Vector3(0.6f, 0.9f, 0.6f)));
                    else trunk.Add(cube, Matrix4x4.TRS(new Vector3(px - nx * 0.8f, 0.41f, pz - nz * 0.8f), Quaternion.LookRotation(new Vector3(-nz, 0, nx)), new Vector3(0.6f, 0.45f, 1.8f)));
                }
            }

            if (blk.Type == BlockType.Park)
            {
                slab.Flat(blk.CX - 1.6f, iz0, blk.CX + 1.6f, iz1, 0.19f, 4); slab.Flat(ix0, blk.CZ - 1.6f, ix1, blk.CZ + 1.6f, 0.19f, 4);
                slab.Add(cyl, Matrix4x4.TRS(new Vector3(blk.CX, 0.5f, blk.CZ), Quaternion.identity, new Vector3(8.6f, 0.8f, 8.6f)));
                var pool = new MeshBuilder(); pool.Add(cyl, Matrix4x4.TRS(new Vector3(blk.CX, 0.86f, blk.CZ), Quaternion.identity, new Vector3(7.2f, 0.02f, 7.2f)));
                pool.ToGameObject("Fountain", mats.Water, parent, false);
                city.AddCollider(new Box2(blk.CX - 4.2f, blk.CZ - 4.2f, blk.CX + 4.2f, blk.CZ + 4.2f, 0.9f));
                for (int k = 0; k < 26; k++)
                {
                    float x = R(ix0 + 2, ix1 - 2), z = R(iz0 + 2, iz1 - 2);
                    if (Mathf.Abs(x - blk.CX) < 3.5f || Mathf.Abs(z - blk.CZ) < 3.5f) continue;
                    Tree(x, z, R(0.9f, 1.5f));
                }
            }
            if (blk.Type == BlockType.Residential) for (int k = 0; k < 6; k++) Tree(R(ix0 + 1, ix1 - 1), R(iz0 + 1, iz1 - 1), R(0.8f, 1.2f));
            if (blk.Containers.Count > 0)
            {
                var palette = new[] { 0xb23a2a, 0x2a5fa8, 0xd99a1e, 0x2e7d4f, 0x7b3fa0, 0xc4c4c0, 0x1f8a9a };
                foreach (var c in blk.Containers)
                    for (int s = 0; s < c.Count; s++) Container(parent, new Vector3(c.X, 0.18f + 1.3f + s * 2.62f, c.Z + 3), 0, palette[rnd.Next(palette.Length)]);
            }

            foreach (var kv in fac) kv.Value.ToGameObject("Facade_" + kv.Key, mats.Facade(kv.Key), parent);
            roof.ToGameObject("Roofs", mats.Roof, parent); slab.ToGameObject("Sidewalk", mats.Slab, parent, false); curb.ToGameObject("Curb", mats.Curb, parent, false);
            lot.ToGameObject("Lawn", mats.Grass, parent, false); metal.ToGameObject("Poles", mats.Metal, parent); metalLight.ToGameObject("RoofKit", mats.MetalLight, parent);
            lamp.ToGameObject("Lamps", mats.Lamp, parent, false); trunk.ToGameObject("Wood", mats.Trunk, parent); blink.ToGameObject("Beacons", mats.RedBlink, parent, false);
            for (int k = 0; k < 3; k++) leaves[k].ToGameObject("Leaves" + k, mats.Leaves[k], parent);
            if (blk.Type == BlockType.Midtown || blk.Type == BlockType.Downtown) BuildNeon(blk, parent);
        }

        private readonly Dictionary<int, MeshBuilder> containerBatches = new Dictionary<int, MeshBuilder>();

        private void Container(Transform parent, Vector3 p, float yaw, int hex)
        {
            if (!containerBatches.TryGetValue(hex, out var mb)) containerBatches[hex] = mb = new MeshBuilder();
            mb.Add(cube, Matrix4x4.TRS(p, Quaternion.Euler(0, yaw, 0), new Vector3(2.44f, 2.6f, 6.1f)));
        }

        private void BuildHarbor(float E1)
        {
            var crane = new MeshBuilder(); var blink = new MeshBuilder();
            for (int k = 0; k < 4; k++)
            {
                float x = E1 + 18, z = 40 + k * 70;
                foreach (var (dx, dz) in new[] { (-5, -6), (5, -6), (-5, 6), (5, 6) }) crane.Box(new Vector3(x + dx - 0.5f, 0.18f, z + dz - 0.5f), new Vector3(x + dx + 0.5f, 30, z + dz + 0.5f), 4, 4, 4);
                crane.Box(new Vector3(x - 6, 30, z - 7), new Vector3(x + 6, 33, z + 7), 4, 4, 4);
                crane.Box(new Vector3(x - 2, 30, z - 1.5f), new Vector3(x + 36, 32.5f, z + 1.5f), 4, 4, 4);
                crane.Box(new Vector3(x - 16, 30.5f, z - 1.5f), new Vector3(x - 6, 32.5f, z + 1.5f), 4, 4, 4);
                crane.Box(new Vector3(x - 4, 33, z - 3), new Vector3(x + 2, 37, z + 3), 4, 4, 4);
                blink.Add(ball, Matrix4x4.TRS(new Vector3(x + 36, 33, z), Quaternion.identity, Vector3.one * 0.4f));
                foreach (var (dx, dz) in new[] { (-5, -6), (5, -6), (-5, 6), (5, 6) }) city.AddCollider(new Box2(x + dx - 0.5f, z + dz - 0.5f, x + dx + 0.5f, z + dz + 0.5f, 30));
            }
            crane.ToGameObject("Cranes", mats.Crane, root); blink.ToGameObject("CraneBeacons", mats.RedBlink, root, false);
            var palette = new[] { 0xb23a2a, 0x2a5fa8, 0xd99a1e, 0x2e7d4f, 0x1f8a9a };
            for (int k = 0; k < 40; k++) Container(root, new Vector3(E1 + R(6, 14), 0.18f + 1.3f + (k % 3) * 2.62f, 30 + (k / 3) * 7.5f + R(-0.3f, 0.3f)), 90, palette[rnd.Next(palette.Length)]);
            foreach (var kv in containerBatches) kv.Value.ToGameObject("Containers", mats.Paint(HarborMaterials.Hex(kv.Key)), root);
        }

        private void BuildWater()
        {
            var water = new MeshBuilder();
            float c = city.Size / 2;
            water.Flat(c - 2000, c - 2000, c + 2000, c + 2000, -1.2f, 10);
            water.ToGameObject("Harbour Water", mats.Water, root, false);
            var far = new MeshBuilder(); var rr = new Rng(5);
            for (int k = 0; k < 60; k++)
            {
                float a = rr.Next() * Mathx.Tau, dist = 900 + rr.Next() * 300, x = c + Mathf.Cos(a) * dist, z = c + Mathf.Sin(a) * dist, w = 20 + rr.Next() * 40, h = 10 + rr.Next() * 70;
                far.Box(new Vector3(x - w / 2, -2, z - w / 2), new Vector3(x + w / 2, h, z + w / 2), 16, 14, 1);
            }
            far.ToGameObject("Far Skyline", mats.Facade(FacadeStyle.Office), root, false);
        }

        private void BuildLightPools()
        {
            var pools = new MeshBuilder();
            foreach (var p in LampPoints) pools.Flat(p.x - 5.5f, p.z - 5.5f, p.x + 5.5f, p.z + 5.5f, 0.03f, 11);
            // UVs: Flat() uses world/11, so re-map each quad to 0..1 for the radial texture
            var go = pools.ToGameObject("LightPools", mats.Pool, root, false);
            if (go == null) return;
            var mesh = go.GetComponent<MeshFilter>().sharedMesh; var uv = mesh.uv;
            for (int k = 0; k < uv.Length; k += 4) { uv[k] = new Vector2(0, 1); uv[k + 1] = new Vector2(1, 1); uv[k + 2] = new Vector2(1, 0); uv[k + 3] = new Vector2(0, 0); }
            mesh.uv = uv;
        }

        private static readonly (string text, int color)[] Words =
        {
            ("24시 편의점", 0x3cf2ff), ("HOTEL", 0xff3b7f), ("치킨", 0xffd23c), ("NOODLE BAR", 0xff6a2b), ("카페", 0x9dff6a), ("PHARMACY", 0x3cff9e), ("노래방", 0xd14bff),
            ("BAR", 0xff3b3b), ("CINEMA", 0xffe66d), ("약국", 0x4bffb2), ("GARAGE", 0x5ab0ff), ("SUSHI", 0xff5ac8), ("피시방", 0x5affff), ("DINER", 0xff9a3c),
        };
        private int neonIndex;
        private Font font;

        private void BuildNeon(Block blk, Transform parent)
        {
            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { font = null; }
                if (font == null) try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { font = null; }
            }
            foreach (var b in blk.Buildings)
            {
                if (rnd.NextDouble() < 0.45) continue;
                var (text, hex) = Words[neonIndex++ % Words.Length];
                Color c = HarborMaterials.Hex(hex);
                float w = Mathf.Min(10, (b.X1 - b.X0) * 0.8f), h = w / 4f, y = R(6, 11);
                // face the nearest street
                var faces = new List<(float gap, float fx, float fz)> { (b.Z0 - blk.Z0, 0, -1), (blk.Z1 - b.Z1, 0, 1), (b.X0 - blk.X0, -1, 0), (blk.X1 - b.X1, 1, 0) };
                faces.Sort((p, q) => p.gap.CompareTo(q.gap));
                var (_, fx, fz) = faces[0];
                var pos = new Vector3(fx != 0 ? (fx > 0 ? b.X1 + 0.15f : b.X0 - 0.15f) : (b.X0 + b.X1) / 2, y, fz != 0 ? (fz > 0 ? b.Z1 + 0.15f : b.Z0 - 0.15f) : (b.Z0 + b.Z1) / 2);
                var sign = new GameObject("Neon_" + text).transform;
                sign.SetParent(parent, false); sign.position = pos; sign.rotation = Quaternion.LookRotation(new Vector3(-fx, 0, -fz));
                var glow = mats.Glow(c, 3f); NeonSigns.Add((glow, c));
                var frame = new MeshBuilder();
                frame.Box(new Vector3(-w / 2, -h / 2, -0.05f), new Vector3(w / 2, -h / 2 + 0.12f, 0.05f));
                frame.Box(new Vector3(-w / 2, h / 2 - 0.12f, -0.05f), new Vector3(w / 2, h / 2, 0.05f));
                frame.Box(new Vector3(-w / 2, -h / 2, -0.05f), new Vector3(-w / 2 + 0.12f, h / 2, 0.05f));
                frame.Box(new Vector3(w / 2 - 0.12f, -h / 2, -0.05f), new Vector3(w / 2, h / 2, 0.05f));
                var fgo = frame.ToGameObject("Frame", glow, sign, false); fgo.isStatic = false;
                var backing = new MeshBuilder(); backing.Box(new Vector3(-w / 2, -h / 2, 0.05f), new Vector3(w / 2, h / 2, 0.15f));
                var bgo = backing.ToGameObject("Board", mats.Plain(new Color(0.04f, 0.04f, 0.06f)), sign, false); bgo.isStatic = false;
                if (font != null)
                {
                    var tgo = new GameObject("Text"); tgo.transform.SetParent(sign, false); tgo.transform.localPosition = new Vector3(0, 0, -0.06f);
                    tgo.transform.localRotation = Quaternion.identity;
                    var tm = tgo.AddComponent<TextMesh>();
                    tm.font = font; tm.text = text; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
                    tm.fontSize = 64; tm.characterSize = h * 0.62f / 64f * 10f / Mathf.Max(1, text.Length > 6 ? 1.6f : 1f); tm.color = Color.Lerp(c, Color.white, 0.5f);
                    tgo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                    tgo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }
    }
}
