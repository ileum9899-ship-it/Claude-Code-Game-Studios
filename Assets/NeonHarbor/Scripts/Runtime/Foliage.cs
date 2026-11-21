using System.Collections.Generic;
using UnityEngine;

namespace NeonHarbor.Runtime
{
    /// <summary>
    /// Tree crowns as ellipsoids in a coarse grid, so the camera can stop in front of a
    /// crown instead of ending up inside it (which fills the screen with leaves).
    /// Trunks are already city colliders; crowns stay soft for cars, people and bullets.
    /// </summary>
    public sealed class Foliage
    {
        private struct Crown { public Vector3 C; public float Rx, Ry; }
        private const float Cell = 20f;
        private readonly Dictionary<long, List<Crown>> grid = new Dictionary<long, List<Crown>>();

        private static long Key(int x, int z) { return ((long)x << 32) ^ (uint)z; }

        /// <summary>Registers one crown: centre, horizontal and vertical radius.</summary>
        public void Add(Vector3 centre, float rx, float ry)
        {
            long k = Key(Mathf.FloorToInt(centre.x / Cell), Mathf.FloorToInt(centre.z / Cell));
            if (!grid.TryGetValue(k, out var list)) grid[k] = list = new List<Crown>();
            list.Add(new Crown { C = centre, Rx = rx, Ry = ry });
        }

        /// <summary>
        /// First entry of segment a→b into any crown grown by <paramref name="margin"/>, as t in [0,1];
        /// 1 when clear. A segment that starts inside a crown is ignored for that crown (the player stands under it).
        /// </summary>
        public float Raycast(Vector3 a, Vector3 b, float margin)
        {
            float best = 1;
            int x0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - 4) / Cell), x1 = Mathf.FloorToInt((Mathf.Max(a.x, b.x) + 4) / Cell);
            int z0 = Mathf.FloorToInt((Mathf.Min(a.z, b.z) - 4) / Cell), z1 = Mathf.FloorToInt((Mathf.Max(a.z, b.z) + 4) / Cell);
            for (int gx = x0; gx <= x1; gx++)
                for (int gz = z0; gz <= z1; gz++)
                {
                    if (!grid.TryGetValue(Key(gx, gz), out var list)) continue;
                    foreach (var c in list)
                    {
                        float rx = c.Rx + margin, ry = c.Ry + margin;
                        Vector3 o = new Vector3((a.x - c.C.x) / rx, (a.y - c.C.y) / ry, (a.z - c.C.z) / rx);
                        Vector3 d = new Vector3((b.x - a.x) / rx, (b.y - a.y) / ry, (b.z - a.z) / rx);
                        float A = Vector3.Dot(d, d), B = Vector3.Dot(o, d), C = Vector3.Dot(o, o) - 1;
                        if (C <= 0 || A < 1e-9f) continue;
                        float disc = B * B - A * C; if (disc < 0) continue;
                        float t = (-B - Mathf.Sqrt(disc)) / A;
                        if (t >= 0 && t < best) best = t;
                    }
                }
            return best;
        }
    }
}
