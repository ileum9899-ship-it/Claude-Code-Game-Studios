using System.Collections.Generic;
using NeonHarbor.Core;
using UnityEngine;

namespace NeonHarbor.Runtime
{
    /// <summary>
    /// Signal heads on every corner and their lamps, drawn with GPU instancing:
    /// each frame the lamps are bucketed by aspect (lit red / yellow / green, unlit)
    /// and submitted with one instanced draw per bucket. No per-lamp GameObjects.
    /// </summary>
    public sealed class TrafficSignalRenderer
    {
        private struct Lamp { public Matrix4x4 M; public int I, J, Axis, Slot; }
        private readonly List<Lamp> lamps = new List<Lamp>();
        private readonly TrafficLights lights;
        private readonly Mesh ball = MeshBuilder.Sphere(6, 10);
        private readonly Material[] on = new Material[3];
        private readonly Material off;
        private readonly List<Matrix4x4>[] buckets = { new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>() };

        /// <summary>Builds poles/heads (static mesh) and registers the per-frame lamp draw.</summary>
        public TrafficSignalRenderer(TrafficLights lights, CityLayout city, HarborMaterials mats, Transform root)
        {
            this.lights = lights;
            on[0] = mats.Glow(new Color(0.1f, 1f, 0.3f), 4f); on[1] = mats.Glow(new Color(1f, 0.6f, 0.05f), 4f); on[2] = mats.Glow(new Color(1f, 0.05f, 0.03f), 4f);
            off = mats.Plain(new Color(0.05f, 0.05f, 0.05f), 0.6f);
            foreach (var m in on) m.enableInstancing = true; off.enableInstancing = true;
            var poles = new MeshBuilder(); var heads = new MeshBuilder();
            Mesh cyl = MeshBuilder.Cylinder(8), cube = MeshBuilder.Cube();
            float h = city.HalfRoad + 1.2f;
            for (int j = 0; j <= city.N; j++)
                for (int i = 0; i <= city.N; i++)
                {
                    float x = i * city.Pitch, z = j * city.Pitch;
                    foreach (var (dx, dz, axis) in new[] { (1, 0, 0), (-1, 0, 0), (0, 1, 1), (0, -1, 1) })
                    {
                        float rx = dz, rz = -dx; // right of travel (Unity)
                        float cx = x + dx * h + rx * h, cz = z + dz * h + rz * h, ex = cx - rx * 2.6f, ez = cz - rz * 2.6f;
                        var face = Quaternion.LookRotation(new Vector3(-dx, 0, -dz));
                        poles.Add(cyl, Matrix4x4.TRS(new Vector3(cx, 2.68f, cz), Quaternion.identity, new Vector3(0.22f, 5f, 0.22f)));
                        poles.Add(cube, Matrix4x4.TRS(new Vector3((cx + ex) / 2, 5f, (cz + ez) / 2), Quaternion.LookRotation(new Vector3(rx, 0, rz)), new Vector3(0.12f, 0.12f, 2.8f)));
                        heads.Add(cube, Matrix4x4.TRS(new Vector3(ex, 4.4f, ez), face, new Vector3(0.42f, 1.2f, 0.36f)));
                        for (int k = 0; k < 3; k++)
                            lamps.Add(new Lamp { M = Matrix4x4.TRS(new Vector3(ex - dx * 0.2f, 4.78f - k * 0.37f, ez - dz * 0.2f), Quaternion.identity, Vector3.one * 0.13f), I = i, J = j, Axis = axis, Slot = 2 - k });
                    }
                }
            poles.ToGameObject("SignalPoles", mats.Metal, root);
            heads.ToGameObject("SignalHeads", mats.Plain(new Color(0.11f, 0.12f, 0.14f), 0.4f), root);
            var driver = new GameObject("SignalLamps").AddComponent<SignalLampDraw>(); driver.transform.SetParent(root, false); driver.Owner = this;
        }

        /// <summary>Submits the instanced lamp draws for this frame.</summary>
        public void Draw(float time)
        {
            foreach (var b in buckets) b.Clear();
            foreach (var l in lamps)
            {
                int s = (int)lights.State(l.I, l.J, l.Axis, time);
                buckets[s == l.Slot ? s : 3].Add(l.M);
            }
            for (int b = 0; b < 4; b++)
            {
                var list = buckets[b]; var mat = b < 3 ? on[b] : off;
                if (list.Count == 0) continue;
                if (list.Count <= 1023) Graphics.DrawMeshInstanced(ball, 0, mat, list); // 972 lamps for the default 8×8 city
                else for (int start = 0; start < list.Count; start += 1023) Graphics.DrawMeshInstanced(ball, 0, mat, list.GetRange(start, Mathf.Min(1023, list.Count - start)));
            }
        }

        private sealed class SignalLampDraw : MonoBehaviour
        {
            public TrafficSignalRenderer Owner;
            private float t;
            private void Update() { t += Time.deltaTime; Owner?.Draw(t); }
        }
    }
}
