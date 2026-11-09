using System;
using System.Collections.Generic;

namespace NeonHarbor.Core
{
    /// <summary>District of a city block.</summary>
    public enum BlockType { Downtown, Midtown, Residential, Park, Harbor }

    /// <summary>Facade style of a building.</summary>
    public enum FacadeStyle { Glass, Office, Brick, Concrete, Plaster, Warehouse }

    /// <summary>Axis-aligned box on the ground plane, from y = 0 to <see cref="H"/>.</summary>
    public struct Box2
    {
        public float X0, Z0, X1, Z1, H;
        public Box2(float x0, float z0, float x1, float z1, float h) { X0 = x0; Z0 = z0; X1 = x1; Z1 = z1; H = h; }
    }

    /// <summary>One stacked volume of a building (towers have setbacks).</summary>
    public struct Tier
    {
        public float X0, Z0, X1, Z1, Y0, Y1;
    }

    /// <summary>A building on a block.</summary>
    public sealed class Building
    {
        public FacadeStyle Style;
        public readonly List<Tier> Tiers = new List<Tier>();
        /// <summary>Footprint.</summary>
        public float X0, Z0, X1, Z1;
        /// <summary>Roof height of the top tier.</summary>
        public float Top => Tiers[Tiers.Count - 1].Y1;
    }

    /// <summary>A shipping-container stack on the harbor quay.</summary>
    public struct ContainerStack { public float X, Z; public int Count; }

    /// <summary>A city block between four roads.</summary>
    public sealed class Block
    {
        public int I, J;
        public float X0, Z0, X1, Z1;
        public BlockType Type;
        public readonly List<Building> Buildings = new List<Building>();
        public readonly List<ContainerStack> Containers = new List<ContainerStack>();
        public float CX => (X0 + X1) / 2f;
        public float CZ => (Z0 + Z1) / 2f;
    }

    /// <summary>
    /// Deterministic city: an N×N grid of blocks with roads on every grid line,
    /// building volumes, solid colliders and the road graph. Port of the browser
    /// build's CITY so both produce the same map from the same seed.
    /// Coordinates: metres, x/z on the ground, y up; intersections at (i·Pitch, j·Pitch).
    /// </summary>
    /// <example><code>
    /// var city = new CityLayout(new HarborConfig());
    /// var route = city.Path(city.NearestNode(10, 10), city.NearestNode(300, 200));
    /// </code></example>
    public sealed class CityLayout
    {
        public readonly int N;
        public readonly float BlockSize, Road, SidewalkWidth, Pitch, Size, HalfRoad;
        /// <summary>Quay width beyond the last road on the +x and +z sides.</summary>
        public const float Quay = 28f;
        public readonly List<Block> Blocks = new List<Block>();
        public readonly List<Box2> Colliders = new List<Box2>();
        private readonly Dictionary<long, List<Box2>> grid = new Dictionary<long, List<Box2>>();
        private static readonly List<Box2> Empty = new List<Box2>();

        /// <summary>Generates the city.</summary>
        public CityLayout(HarborConfig cfg)
        {
            N = cfg.Blocks; BlockSize = cfg.BlockSize; Road = cfg.RoadWidth; SidewalkWidth = cfg.Sidewalk;
            Pitch = BlockSize + Road; Size = N * Pitch; HalfRoad = Road / 2f;
            var rnd = new Rng(cfg.Seed);
            float center = Size / 2f;
            FacadeStyle[] mid = { FacadeStyle.Office, FacadeStyle.Brick, FacadeStyle.Concrete, FacadeStyle.Glass };
            FacadeStyle[] res = { FacadeStyle.Brick, FacadeStyle.Concrete, FacadeStyle.Plaster, FacadeStyle.Plaster };
            for (int j = 0; j < N; j++)
            {
                for (int i = 0; i < N; i++)
                {
                    float x0 = i * Pitch + HalfRoad, z0 = j * Pitch + HalfRoad;
                    var blk = new Block { I = i, J = j, X0 = x0, Z0 = z0, X1 = x0 + BlockSize, Z1 = z0 + BlockSize };
                    float d = (float)Math.Sqrt((blk.CX - center) * (blk.CX - center) + (blk.CZ - center) * (blk.CZ - center)) / (Size * 0.5f);
                    if ((i == 3 && j == 4) || (i == 6 && j == 1) || (i == 1 && j == 6)) blk.Type = BlockType.Park;
                    else if (i == N - 1 && j <= 2) blk.Type = BlockType.Harbor;
                    else if (d < 0.38f) blk.Type = BlockType.Downtown;
                    else if (d < 0.75f) blk.Type = BlockType.Midtown;
                    else blk.Type = BlockType.Residential;

                    if (blk.Type == BlockType.Downtown || blk.Type == BlockType.Midtown || blk.Type == BlockType.Residential)
                        Subdivide(blk, rnd, d, mid, res);
                    else if (blk.Type == BlockType.Harbor)
                        BuildHarbor(blk, rnd);
                    Blocks.Add(blk);
                }
            }
        }

        private void Subdivide(Block blk, Rng rnd, float d, FacadeStyle[] mid, FacadeStyle[] res)
        {
            float sw = SidewalkWidth;
            float lx0 = blk.X0 + sw + 1, lz0 = blk.Z0 + sw + 1, lx1 = blk.X1 - sw - 1, lz1 = blk.Z1 - sw - 1;
            bool down = blk.Type == BlockType.Downtown;
            int nx = down ? (rnd.Next() < 0.5f ? 1 : 2) : (rnd.Next() < 0.5f ? 2 : 3);
            int nz = down ? (rnd.Next() < 0.5f ? 1 : 2) : (rnd.Next() < 0.5f ? 2 : 3);
            float gap = blk.Type == BlockType.Residential ? 3f : 2f;
            float pw = (lx1 - lx0 - gap * (nx - 1)) / nx, pd = (lz1 - lz0 - gap * (nz - 1)) / nz;
            for (int a = 0; a < nx; a++)
            {
                for (int b = 0; b < nz; b++)
                {
                    float bx0 = lx0 + a * (pw + gap), bz0 = lz0 + b * (pd + gap), bw = pw, bd = pd;
                    float shrink = blk.Type == BlockType.Residential ? rnd.Next() * 3f : rnd.Next() * 1.5f;
                    bx0 += shrink / 2; bz0 += shrink / 2; bw -= shrink; bd -= shrink;
                    float h; FacadeStyle style;
                    if (down) { h = 55f + rnd.Next() * 110f * (1.2f - d); style = rnd.Next() < 0.7f ? FacadeStyle.Glass : FacadeStyle.Office; }
                    else if (blk.Type == BlockType.Midtown) { h = 18f + rnd.Next() * 42f; style = mid[(int)Math.Floor(rnd.Next() * mid.Length)]; }
                    else { h = 7f + rnd.Next() * 12f; style = res[(int)Math.Floor(rnd.Next() * res.Length)]; }
                    h = (float)Math.Round(h / 3.5f) * 3.5f;
                    var bld = new Building { Style = style, X0 = bx0, Z0 = bz0, X1 = bx0 + bw, Z1 = bz0 + bd };
                    bld.Tiers.Add(new Tier { X0 = bld.X0, Z0 = bld.Z0, X1 = bld.X1, Z1 = bld.Z1, Y0 = 0, Y1 = h });
                    if (h > 60f && rnd.Next() < 0.8f)
                    {
                        float s = Math.Min(bw, bd) * 0.18f, h2 = (float)Math.Round(h * (0.25f + rnd.Next() * 0.3f) / 3.5f) * 3.5f;
                        bld.Tiers.Add(new Tier { X0 = bld.X0 + s, Z0 = bld.Z0 + s, X1 = bld.X1 - s, Z1 = bld.Z1 - s, Y0 = h, Y1 = h + h2 });
                        if (rnd.Next() < 0.5f)
                        {
                            float s2 = s * 2.2f;
                            bld.Tiers.Add(new Tier { X0 = bld.X0 + s2, Z0 = bld.Z0 + s2, X1 = bld.X1 - s2, Z1 = bld.Z1 - s2, Y0 = h + h2, Y1 = h + h2 + 10.5f });
                        }
                    }
                    blk.Buildings.Add(bld);
                    AddCollider(new Box2(bld.X0, bld.Z0, bld.X1, bld.Z1, bld.Top));
                }
            }
        }

        private void BuildHarbor(Block blk, Rng rnd)
        {
            float sw = SidewalkWidth, lx0 = blk.X0 + sw + 2, lz0 = blk.Z0 + sw + 2;
            var wh = new Building { Style = FacadeStyle.Warehouse, X0 = lx0, Z0 = lz0, X1 = lx0 + 20, Z1 = blk.Z1 - sw - 2 };
            wh.Tiers.Add(new Tier { X0 = wh.X0, Z0 = wh.Z0, X1 = wh.X1, Z1 = wh.Z1, Y0 = 0, Y1 = 10.5f });
            blk.Buildings.Add(wh);
            AddCollider(new Box2(wh.X0, wh.Z0, wh.X1, wh.Z1, 10.5f));
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 5; c++)
                    blk.Containers.Add(new ContainerStack { X = lx0 + 24 + r * 2.7f, Z = lz0 + 1 + c * 7.4f, Count = 1 + (int)Math.Floor(rnd.Next() * 3) });
            AddCollider(new Box2(lx0 + 22.6f, lz0, lx0 + 31f, lz0 + 37f, 8f));
        }

        private long Key(int a, int b) { return ((long)a << 32) ^ (uint)b; }
        private int CellOf(float v) { return (int)Math.Floor(v / Pitch); }

        /// <summary>Registers a solid box (buildings, tree trunks, poles).</summary>
        public void AddCollider(Box2 c)
        {
            Colliders.Add(c);
            for (int a = CellOf(c.X0) - 1; a <= CellOf(c.X1) + 1; a++)
                for (int b = CellOf(c.Z0) - 1; b <= CellOf(c.Z1) + 1; b++)
                {
                    long k = Key(a, b);
                    if (!grid.TryGetValue(k, out var list)) grid[k] = list = new List<Box2>();
                    list.Add(c);
                }
        }

        /// <summary>Colliders near a point (same and neighbouring cells).</summary>
        public List<Box2> Near(float x, float z)
        {
            return grid.TryGetValue(Key(CellOf(x), CellOf(z)), out var list) ? list : Empty;
        }

        /// <summary>World position of intersection (i, j).</summary>
        public Vec2 NodePos(int i, int j) { return new Vec2(i * Pitch, j * Pitch); }

        /// <summary>True when (i, j) is an intersection.</summary>
        public bool InGraph(int i, int j) { return i >= 0 && j >= 0 && i <= N && j <= N; }

        /// <summary>Connected intersections of (i, j).</summary>
        public List<(int, int)> Neighbours(int i, int j)
        {
            var o = new List<(int, int)>(4);
            if (InGraph(i + 1, j)) o.Add((i + 1, j));
            if (InGraph(i - 1, j)) o.Add((i - 1, j));
            if (InGraph(i, j + 1)) o.Add((i, j + 1));
            if (InGraph(i, j - 1)) o.Add((i, j - 1));
            return o;
        }

        /// <summary>Closest intersection to a point.</summary>
        public (int, int) NearestNode(float x, float z)
        {
            return ((int)Mathx.Clamp((float)Math.Round(x / Pitch), 0, N), (int)Mathx.Clamp((float)Math.Round(z / Pitch), 0, N));
        }

        /// <summary>Breadth-first route over the road graph, both ends included.</summary>
        public List<(int, int)> Path((int, int) from, (int, int) to)
        {
            var prev = new Dictionary<(int, int), (int, int)?> { [from] = null };
            var q = new Queue<(int, int)>();
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c.Equals(to)) break;
                foreach (var n in Neighbours(c.Item1, c.Item2))
                    if (!prev.ContainsKey(n)) { prev[n] = c; q.Enqueue(n); }
            }
            var path = new List<(int, int)>();
            if (!prev.ContainsKey(to)) return path;
            (int, int)? cur = to;
            while (cur.HasValue) { path.Insert(0, cur.Value); cur = prev[cur.Value]; }
            return path;
        }

        /// <summary>
        /// GPS route between two world points: start, the intersections of the shortest road path, end.
        /// </summary>
        public List<Vec2> Route(Vec2 from, Vec2 to)
        {
            var nodes = Path(NearestNode(from.X, from.Y), NearestNode(to.X, to.Y));
            var pts = new List<Vec2>(nodes.Count + 2) { from };
            foreach (var n in nodes) pts.Add(NodePos(n.Item1, n.Item2));
            pts.Add(to);
            return pts;
        }

        /// <summary>True on asphalt (inside a road corridor or an intersection).</summary>
        public bool OnRoad(float x, float z)
        {
            float mx = ((x % Pitch) + Pitch) % Pitch, mz = ((z % Pitch) + Pitch) % Pitch;
            bool inX = mx < HalfRoad || mx > Pitch - HalfRoad, inZ = mz < HalfRoad || mz > Pitch - HalfRoad;
            return (inX || inZ) && x > -HalfRoad && z > -HalfRoad && x < Size + HalfRoad && z < Size + HalfRoad;
        }

        /// <summary>Walkable ground height: 0 on roads, 0.18 on sidewalks/lots, −100 in the water.</summary>
        public float GroundHeight(float x, float z)
        {
            if (x < -HalfRoad - 0.01f || z < -HalfRoad - 0.01f || x > Size + HalfRoad + Quay + 2 || z > Size + HalfRoad + Quay + 2) return -100f;
            return OnRoad(x, z) ? 0f : 0.18f;
        }

        /// <summary>The block containing a point, or null on a road.</summary>
        public Block BlockAt(float x, float z)
        {
            int i = (int)Math.Floor((x - HalfRoad) / Pitch), j = (int)Math.Floor((z - HalfRoad) / Pitch);
            if (i < 0 || j < 0 || i >= N || j >= N) return null;
            var b = Blocks[j * N + i];
            return x >= b.X0 && x <= b.X1 && z >= b.Z0 && z <= b.Z1 ? b : null;
        }

        /// <summary>
        /// Pushes a circle out of every collider and the quay edge. Returns true on contact and
        /// the last contact normal in <paramref name="nx"/>, <paramref name="nz"/>.
        /// </summary>
        public bool CollideCircle(ref float px, ref float pz, float r, out float nx, out float nz)
        {
            bool hit = false; nx = 0; nz = 0;
            var list = Near(px, pz);
            for (int k = 0; k < list.Count; k++)
            {
                Box2 c = list[k];
                float qx = Mathx.Clamp(px, c.X0, c.X1), qz = Mathx.Clamp(pz, c.Z0, c.Z1);
                float dx = px - qx, dz = pz - qz, d2 = dx * dx + dz * dz;
                if (d2 >= r * r) continue;
                if (d2 < 1e-8f)
                {
                    float l = px - c.X0, rr = c.X1 - px, t = pz - c.Z0, b = c.Z1 - pz, m = Math.Min(Math.Min(l, rr), Math.Min(t, b));
                    if (m == l) { nx = -1; nz = 0; px = c.X0 - r; }
                    else if (m == rr) { nx = 1; nz = 0; px = c.X1 + r; }
                    else if (m == t) { nx = 0; nz = -1; pz = c.Z0 - r; }
                    else { nx = 0; nz = 1; pz = c.Z1 + r; }
                }
                else
                {
                    float d = (float)Math.Sqrt(d2), push = r - d;
                    dx /= d; dz /= d; px += dx * push; pz += dz * push; nx = dx; nz = dz;
                }
                hit = true;
            }
            float lim0 = -HalfRoad - 1, lim1 = Size + HalfRoad + Quay;
            if (px < lim0 + r) { px = lim0 + r; nx = 1; nz = 0; hit = true; }
            if (pz < lim0 + r) { pz = lim0 + r; nx = 0; nz = 1; hit = true; }
            if (px > lim1 - r) { px = lim1 - r; nx = -1; nz = 0; hit = true; }
            if (pz > lim1 - r) { pz = lim1 - r; nx = 0; nz = -1; hit = true; }
            return hit;
        }

        private readonly HashSet<Box2> raySeen = new HashSet<Box2>();
        private readonly float[] rayO = new float[3], rayD = new float[3], rayMin = new float[3], rayMax = new float[3];

        /// <summary>First hit along segment a→b against collider boxes, as a fraction t in [0, 1] (1 = clear).</summary>
        public float Raycast(float ax, float ay, float az, float bx, float by, float bz)
        {
            float best = 1f;
            var seen = raySeen; seen.Clear();
            for (int s = 0; s <= 4; s++)
            {
                float t = s / 4f;
                foreach (var c in Near(Mathx.Lerp(ax, bx, t), Mathx.Lerp(az, bz, t))) seen.Add(c);
            }
            float[] o = rayO, dv = rayD, mn = rayMin, mx = rayMax;
            o[0] = ax; o[1] = ay; o[2] = az; dv[0] = bx - ax; dv[1] = by - ay; dv[2] = bz - az;
            foreach (var c in seen)
            {
                mn[0] = c.X0; mn[1] = 0; mn[2] = c.Z0; mx[0] = c.X1; mx[1] = c.H; mx[2] = c.Z1;
                float t0 = 0, t1 = 1; bool ok = true;
                for (int k = 0; k < 3 && ok; k++)
                {
                    if (Math.Abs(dv[k]) < 1e-9f) { if (o[k] < mn[k] || o[k] > mx[k]) ok = false; }
                    else
                    {
                        float ta = (mn[k] - o[k]) / dv[k], tb = (mx[k] - o[k]) / dv[k];
                        if (ta > tb) { float tmp = ta; ta = tb; tb = tmp; }
                        t0 = Math.Max(t0, ta); t1 = Math.Min(t1, tb);
                        if (t0 > t1) ok = false;
                    }
                }
                if (ok && t0 < best) best = t0;
            }
            return best;
        }

        /// <summary>Point on the sidewalk ring of a block, with walking direction; t wraps in [0, 1).</summary>
        public (float x, float z, float dx, float dz) SidewalkPoint(Block blk, float t)
        {
            float inset = SidewalkWidth * 0.5f, x0 = blk.X0 + inset, z0 = blk.Z0 + inset, x1 = blk.X1 - inset, z1 = blk.Z1 - inset;
            float w = x1 - x0, h = z1 - z0, per = 2 * (w + h);
            float d = (((t % 1f) + 1f) % 1f) * per;
            if (d < w) return (x0 + d, z0, 1, 0); d -= w;
            if (d < h) return (x1, z0 + d, 0, 1); d -= h;
            if (d < w) return (x1 - d, z1, -1, 0); d -= w;
            return (x0, z1 - d, 0, -1);
        }

        /// <summary>Perimeter of a block's sidewalk ring.</summary>
        public float SidewalkPerimeter(Block blk) { return 2 * ((blk.X1 - blk.X0) + (blk.Z1 - blk.Z0) - 2 * SidewalkWidth); }
    }
}
