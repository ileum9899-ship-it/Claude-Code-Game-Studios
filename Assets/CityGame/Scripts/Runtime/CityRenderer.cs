using CityGame.Core;
using UnityEngine;

namespace CityGame.Runtime
{
    /// <summary>
    /// Builds a GameObject per occupied tile from primitive meshes (no imported art).
    /// One world unit = one tile; tile (x, y) is centred at (x + 0.5, 0, y + 0.5).
    /// Rebuilds only the tile the simulation reports as changed.
    /// </summary>
    /// <example><code>var r = new CityRenderer(sim, materials, parent); r.RebuildAll();</code></example>
    public sealed class CityRenderer
    {
        private static readonly Color Asphalt = new Color(0.22f, 0.23f, 0.25f);
        private static readonly Color Marking = new Color(0.95f, 0.85f, 0.35f);
        private static readonly Color Sidewalk = new Color(0.62f, 0.62f, 0.6f);
        private static readonly Color ResZone = new Color(0.55f, 0.85f, 0.5f);
        private static readonly Color ComZone = new Color(0.5f, 0.7f, 0.95f);
        private static readonly Color IndZone = new Color(0.95f, 0.8f, 0.45f);
        private static readonly Color Grass = new Color(0.35f, 0.62f, 0.3f);
        private static readonly Color ParkGrass = new Color(0.3f, 0.7f, 0.32f);
        private static readonly Color Trunk = new Color(0.42f, 0.28f, 0.16f);
        private static readonly Color Leaves = new Color(0.18f, 0.5f, 0.2f);
        private static readonly Color[] Roofs = { new Color(0.7f, 0.25f, 0.2f), new Color(0.35f, 0.3f, 0.45f), new Color(0.3f, 0.45f, 0.5f) };
        private static readonly Color[] HouseWalls = { new Color(0.95f, 0.9f, 0.8f), new Color(0.9f, 0.8f, 0.65f), new Color(0.85f, 0.88f, 0.92f) };
        private static readonly Color[] Towers = { new Color(0.55f, 0.68f, 0.82f), new Color(0.7f, 0.75f, 0.8f), new Color(0.4f, 0.55f, 0.7f) };
        private static readonly Color[] Factories = { new Color(0.6f, 0.55f, 0.5f), new Color(0.55f, 0.5f, 0.42f), new Color(0.5f, 0.52f, 0.55f) };

        private readonly CitySimulation sim;
        private readonly CityMaterials materials;
        private readonly Transform root;
        private readonly GameObject[] objects;
        private readonly Mesh cube, cylinder, sphere;

        /// <summary>Creates the renderer and the ground plane.</summary>
        public CityRenderer(CitySimulation sim, CityMaterials materials, Transform root)
        {
            this.sim = sim;
            this.materials = materials;
            this.root = root;
            objects = new GameObject[sim.Grid.Size * sim.Grid.Size];
            cube = PrimitiveMesh(PrimitiveType.Cube);
            cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
            sphere = PrimitiveMesh(PrimitiveType.Sphere);

            int s = sim.Grid.Size;
            Part(root, "Ground", cube, Grass, new Vector3(s / 2f, -0.05f, s / 2f), new Vector3(s, 0.1f, s));
            Part(root, "Border", cube, new Color(0.25f, 0.45f, 0.25f), new Vector3(s / 2f, -0.12f, s / 2f), new Vector3(s + 6f, 0.1f, s + 6f));
        }

        /// <summary>Rebuilds every tile (after load).</summary>
        public void RebuildAll()
        {
            int s = sim.Grid.Size;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    Rebuild(x, y);
        }

        /// <summary>Rebuilds the visuals of one tile.</summary>
        public void Rebuild(int x, int y)
        {
            int index = y * sim.Grid.Size + x;
            if (objects[index] != null) Object.Destroy(objects[index]);
            objects[index] = null;

            Tile t = sim.Grid.Get(x, y);
            if (t.Type == TileType.Empty) return;

            var go = new GameObject($"{t.Type}_{x}_{y}");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(x + 0.5f, 0f, y + 0.5f);
            objects[index] = go;
            Transform tr = go.transform;
            uint h = Hash(x, y);

            switch (t.Type)
            {
                case TileType.Road: BuildRoad(tr, x, y); break;
                case TileType.Park: BuildPark(tr, h); break;
                case TileType.Residential: BuildResidential(tr, t, h); break;
                case TileType.Commercial: BuildCommercial(tr, t, h); break;
                case TileType.Industrial: BuildIndustrial(tr, t, h); break;
            }

            if (TileTypes.IsZone(t.Type)) tr.localRotation = Quaternion.Euler(0f, FacingRoad(x, y), 0f);
        }

        private void BuildRoad(Transform tr, int x, int y)
        {
            Part(tr, "Asphalt", cube, Asphalt, new Vector3(0f, 0.02f, 0f), new Vector3(1f, 0.04f, 1f));
            CityGrid g = sim.Grid;
            bool e = g.TypeAt(x + 1, y) == TileType.Road, w = g.TypeAt(x - 1, y) == TileType.Road;
            bool n = g.TypeAt(x, y + 1) == TileType.Road, s = g.TypeAt(x, y - 1) == TileType.Road;
            int links = (e ? 1 : 0) + (w ? 1 : 0) + (n ? 1 : 0) + (s ? 1 : 0);
            // Centre-line dashes toward each connected neighbour (intersections stay clear).
            if (links <= 2)
            {
                if (e || w || links == 0) Part(tr, "DashX", cube, Marking, new Vector3(0f, 0.045f, 0f), new Vector3(0.35f, 0.01f, 0.05f));
                if (n || s) Part(tr, "DashZ", cube, Marking, new Vector3(0f, 0.045f, 0f), new Vector3(0.05f, 0.01f, 0.35f));
            }
            // Sidewalk edges on sides without a road.
            if (!e) Part(tr, "WalkE", cube, Sidewalk, new Vector3(0.45f, 0.05f, 0f), new Vector3(0.1f, 0.06f, 1f));
            if (!w) Part(tr, "WalkW", cube, Sidewalk, new Vector3(-0.45f, 0.05f, 0f), new Vector3(0.1f, 0.06f, 1f));
            if (!n) Part(tr, "WalkN", cube, Sidewalk, new Vector3(0f, 0.05f, 0.45f), new Vector3(1f, 0.06f, 0.1f));
            if (!s) Part(tr, "WalkS", cube, Sidewalk, new Vector3(0f, 0.05f, -0.45f), new Vector3(1f, 0.06f, 0.1f));
            // Street lamp on every few straight pieces.
            if (links == 2 && ((x + y) % 3 == 0))
            {
                Vector3 side = (e || w) ? new Vector3(0f, 0f, 0.44f) : new Vector3(0.44f, 0f, 0f);
                Part(tr, "Pole", cylinder, new Color(0.3f, 0.3f, 0.32f), side + new Vector3(0f, 0.25f, 0f), new Vector3(0.03f, 0.25f, 0.03f));
                var lamp = Part(tr, "Lamp", sphere, Color.white, side + new Vector3(0f, 0.52f, 0f), Vector3.one * 0.07f);
                lamp.GetComponent<MeshRenderer>().sharedMaterial = materials.Lamps;
            }
        }

        private void BuildPark(Transform tr, uint h)
        {
            Part(tr, "Lawn", cube, ParkGrass, new Vector3(0f, 0.02f, 0f), new Vector3(0.96f, 0.04f, 0.96f));
            int trees = 2 + (int)(h % 3);
            for (int i = 0; i < trees; i++)
            {
                uint hh = Hash((int)h, i);
                float px = ((hh & 0xFF) / 255f - 0.5f) * 0.7f;
                float pz = (((hh >> 8) & 0xFF) / 255f - 0.5f) * 0.7f;
                float size = 0.22f + ((hh >> 16) & 0xFF) / 255f * 0.15f;
                Part(tr, "Trunk", cylinder, Trunk, new Vector3(px, 0.15f, pz), new Vector3(0.05f, 0.13f, 0.05f));
                Part(tr, "Leaves", sphere, Leaves, new Vector3(px, 0.3f + size * 0.5f, pz), new Vector3(size, size * 1.2f, size));
            }
        }

        private void BuildResidential(Transform tr, Tile t, uint h)
        {
            Lot(tr, ResZone, t);
            if (t.Level == 0) return;
            Color wall = HouseWalls[h % (uint)HouseWalls.Length];
            if (t.Level == 1)
            {
                // Small house with pitched roof (a cube rotated 45° and squashed).
                Part(tr, "House", cube, wall, new Vector3(0f, 0.2f, -0.05f), new Vector3(0.55f, 0.36f, 0.5f));
                var roof = Part(tr, "Roof", cube, Roofs[h % (uint)Roofs.Length], new Vector3(0f, 0.38f, -0.05f), new Vector3(0.6f, 0.28f, 0.28f));
                roof.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
                roof.transform.localScale = new Vector3(0.6f, 0.3f, 0.3f);
                Part(tr, "Door", cube, Roofs[(h >> 3) % (uint)Roofs.Length], new Vector3(0f, 0.1f, -0.31f), new Vector3(0.12f, 0.2f, 0.02f));
                return;
            }
            float height = t.Level == 2 ? 0.9f + (h % 3) * 0.1f : 1.6f + (h % 4) * 0.25f;
            Building(tr, wall, new Vector3(0.75f, height, 0.7f), height);
            Part(tr, "RoofTop", cube, Roofs[h % (uint)Roofs.Length], new Vector3(0f, height + 0.03f, 0f), new Vector3(0.78f, 0.06f, 0.73f));
        }

        private void BuildCommercial(Transform tr, Tile t, uint h)
        {
            Lot(tr, ComZone, t);
            if (t.Level == 0) return;
            Color c = Towers[h % (uint)Towers.Length];
            float height = t.Level == 1 ? 0.55f : (t.Level == 2 ? 1.3f + (h % 3) * 0.2f : 2.6f + (h % 5) * 0.45f);
            float width = t.Level == 3 ? 0.7f : 0.8f;
            Building(tr, c, new Vector3(width, height, width), height);
            if (t.Level == 1)
                Part(tr, "Awning", cube, Roofs[h % (uint)Roofs.Length], new Vector3(0f, 0.42f, -0.43f), new Vector3(0.8f, 0.04f, 0.12f));
            if (t.Level == 3)
                Part(tr, "Antenna", cylinder, new Color(0.8f, 0.8f, 0.8f), new Vector3(0f, height + 0.25f, 0f), new Vector3(0.03f, 0.25f, 0.03f));
        }

        private void BuildIndustrial(Transform tr, Tile t, uint h)
        {
            Lot(tr, IndZone, t);
            if (t.Level == 0) return;
            Color c = Factories[h % (uint)Factories.Length];
            float height = 0.35f + t.Level * 0.18f;
            Part(tr, "Hall", cube, c, new Vector3(0f, height / 2f, 0.05f), new Vector3(0.85f, height, 0.65f));
            Part(tr, "Roof", cube, c * 0.8f, new Vector3(0f, height + 0.03f, 0.05f), new Vector3(0.88f, 0.06f, 0.68f));
            for (int i = 0; i < t.Level; i++)
            {
                float cx = -0.3f + i * 0.3f;
                float ch = 0.5f + t.Level * 0.25f;
                Part(tr, "Chimney", cylinder, new Color(0.45f, 0.4f, 0.38f), new Vector3(cx, ch / 2f, 0.28f), new Vector3(0.1f, ch / 2f, 0.1f));
                Part(tr, "Stripe", cylinder, new Color(0.8f, 0.2f, 0.15f), new Vector3(cx, ch * 0.9f, 0.28f), new Vector3(0.105f, 0.03f, 0.105f));
            }
        }

        private void Lot(Transform tr, Color zone, Tile t)
        {
            Color c = t.HasRoadAccess ? zone : Color.Lerp(zone, new Color(0.5f, 0.5f, 0.5f), 0.6f);
            float inset = t.Level == 0 ? 0.9f : 0.96f;
            Part(tr, "Lot", cube, t.Level == 0 ? c : Color.Lerp(c, Sidewalk, 0.5f), new Vector3(0f, 0.015f, 0f), new Vector3(inset, 0.03f, inset));
        }

        /// <summary>A box with lit window bands every floor.</summary>
        private void Building(Transform tr, Color color, Vector3 size, float height)
        {
            Part(tr, "Body", cube, color, new Vector3(0f, height / 2f, 0f), size);
            const float floor = 0.28f;
            for (float yy = 0.18f; yy < height - 0.08f; yy += floor)
            {
                var band = Part(tr, "Windows", cube, color, new Vector3(0f, yy, 0f), new Vector3(size.x + 0.02f, 0.1f, size.z + 0.02f));
                band.GetComponent<MeshRenderer>().sharedMaterial = materials.Windows;
            }
        }

        /// <summary>Yaw that turns a building's front (−Z) toward an adjacent road.</summary>
        private float FacingRoad(int x, int y)
        {
            CityGrid g = sim.Grid;
            if (g.TypeAt(x, y - 1) == TileType.Road) return 0f;
            if (g.TypeAt(x - 1, y) == TileType.Road) return 90f;
            if (g.TypeAt(x, y + 1) == TileType.Road) return 180f;
            if (g.TypeAt(x + 1, y) == TileType.Road) return 270f;
            return 0f;
        }

        private GameObject Part(Transform parent, string name, Mesh mesh, Color color, Vector3 pos, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = materials.Get(color);
            return go;
        }

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(temp);
            return mesh;
        }

        private static uint Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return h;
            }
        }
    }
}
