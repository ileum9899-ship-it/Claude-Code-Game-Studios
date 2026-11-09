using UnityEngine;
using UnityEngine.Rendering;

namespace NeonHarbor.Runtime
{
    /// <summary>
    /// A low-poly person assembled from procedural primitives with a joint hierarchy
    /// (pelvis → chest → head/arms, pelvis → legs) and procedural walk / run / air /
    /// seated poses. Origin at the feet, facing +z.
    /// </summary>
    /// <example><code>
    /// var rig = HumanRig.Create(mats, parent, HumanLook.Random(rng));
    /// rig.Animate(speed: 2f, dt, airborne: false);
    /// </code></example>
    public sealed class HumanRig
    {
        public Transform Root, Pelvis, Chest, Head, ArmL, ArmR, ForeL, ForeR, LegL, LegR, ShinL, ShinR;
        private float phase;

        /// <summary>Colours for one person.</summary>
        public struct HumanLook { public Color Top, Bottom, Skin, Hair; }

        private static readonly int[] SkinTones = { 0xf1c9a5, 0xe0ac85, 0xc68863, 0x8d5a3b, 0xf5d7bf };
        private static readonly int[] Tops = { 0x2f4f7f, 0xb33a3a, 0x3d3d3d, 0xe8e2d4, 0x6b8e3a, 0xd9a52b, 0x7a4a9c, 0x1f7a7a, 0xc75b2a };
        private static readonly int[] Bottoms = { 0x2b2f3a, 0x3c4a63, 0x5a4a3a, 0x1d1d22, 0x6f6a5f };
        private static readonly int[] Hairs = { 0x1a1410, 0x3b2a1e, 0x6b4a2b, 0xb08a52, 0x2b2b2b };

        /// <summary>A random civilian look.</summary>
        public static HumanLook RandomLook(System.Random r)
        {
            return new HumanLook { Top = HarborMaterials.Hex(Tops[r.Next(Tops.Length)]), Bottom = HarborMaterials.Hex(Bottoms[r.Next(Bottoms.Length)]), Skin = HarborMaterials.Hex(SkinTones[r.Next(SkinTones.Length)]), Hair = HarborMaterials.Hex(Hairs[r.Next(Hairs.Length)]) };
        }

        /// <summary>The player's look (dark jacket, jeans).</summary>
        public static HumanLook PlayerLook => new HumanLook { Top = HarborMaterials.Hex(0x2a2f36), Bottom = HarborMaterials.Hex(0x3a4d6b), Skin = HarborMaterials.Hex(0xe0ac85), Hair = HarborMaterials.Hex(0x1a1410) };

        private static Mesh torso, hips, arm, leg, head, hair, shoe, hand;

        private static void EnsureMeshes()
        {
            if (torso != null) return;
            Mesh Seg(float rTop, float rBottom, float len, int segs)
            {
                var mb = new MeshBuilder();
                mb.Add(MeshBuilder.Cylinder(segs, rTop / rBottom), Matrix4x4.TRS(new Vector3(0, -len / 2, 0), Quaternion.identity, new Vector3(rBottom, len, rBottom)));
                return mb.ToMesh("seg");
            }
            torso = Seg(0.2f, 0.17f, 0.62f, 10);
            hips = Seg(0.18f, 0.19f, 0.2f, 10);
            arm = Seg(0.075f, 0.06f, 0.42f, 8);
            leg = Seg(0.095f, 0.075f, 0.46f, 8);
            head = MeshBuilder.Sphere(8, 12);
            var hb = new MeshBuilder(); hb.Add(MeshBuilder.Sphere(8, 12), Matrix4x4.TRS(new Vector3(0, 0.055f, -0.012f), Quaternion.identity, new Vector3(0.138f, 0.1f, 0.142f))); hair = hb.ToMesh("hair");
            var sb = new MeshBuilder(); sb.Add(MeshBuilder.Cube(), Matrix4x4.TRS(new Vector3(0, -0.04f, 0.05f), Quaternion.identity, new Vector3(0.12f, 0.08f, 0.26f))); shoe = sb.ToMesh("shoe");
            hand = MeshBuilder.Sphere(5, 8);
        }

        /// <summary>Builds a person under <paramref name="parent"/>.</summary>
        public static HumanRig Create(HarborMaterials mats, Transform parent, HumanLook look, bool castShadows = true)
        {
            EnsureMeshes();
            var h = new HumanRig();
            Material top = mats.Plain(look.Top, 0.15f), bottom = mats.Plain(look.Bottom, 0.15f), skin = mats.Plain(look.Skin, 0.35f), hairM = mats.Plain(look.Hair, 0.1f), shoeM = mats.Plain(new Color(0.1f, 0.1f, 0.1f), 0.4f);
            Transform Node(string name, Transform p, Vector3 pos) { var t = new GameObject(name).transform; t.SetParent(p, false); t.localPosition = pos; return t; }
            void Mesh(Mesh m, Material mat, Transform p, Vector3 pos, Vector3 scale)
            {
                var go = new GameObject(m.name); go.transform.SetParent(p, false); go.transform.localPosition = pos; go.transform.localScale = scale;
                go.AddComponent<MeshFilter>().sharedMesh = m; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
                r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            h.Root = Node("Person", parent, Vector3.zero);
            h.Pelvis = Node("Pelvis", h.Root, new Vector3(0, 0.98f, 0));
            Mesh(hips, bottom, h.Pelvis, new Vector3(0, 0.1f, 0), Vector3.one);
            h.Chest = Node("Chest", h.Pelvis, new Vector3(0, 0.6f, 0));
            Mesh(torso, top, h.Chest, Vector3.zero, new Vector3(1.1f, 1f, 0.75f));
            h.Head = Node("Head", h.Chest, new Vector3(0, 0.17f, 0));
            Mesh(head, skin, h.Head, Vector3.zero, new Vector3(0.13f, 0.145f, 0.133f));
            Mesh(hair, hairM, h.Head, Vector3.zero, Vector3.one);
            h.ArmL = Node("ArmL", h.Chest, new Vector3(-0.27f, -0.04f, 0)); Mesh(arm, top, h.ArmL, Vector3.zero, Vector3.one);
            h.ArmR = Node("ArmR", h.Chest, new Vector3(0.27f, -0.04f, 0)); Mesh(arm, top, h.ArmR, Vector3.zero, Vector3.one);
            h.ForeL = Node("ForeL", h.ArmL, new Vector3(0, -0.42f, 0)); Mesh(arm, skin, h.ForeL, Vector3.zero, Vector3.one); Mesh(hand, skin, h.ForeL, new Vector3(0, -0.44f, 0), Vector3.one * 0.055f);
            h.ForeR = Node("ForeR", h.ArmR, new Vector3(0, -0.42f, 0)); Mesh(arm, skin, h.ForeR, Vector3.zero, Vector3.one); Mesh(hand, skin, h.ForeR, new Vector3(0, -0.44f, 0), Vector3.one * 0.055f);
            h.LegL = Node("LegL", h.Pelvis, new Vector3(-0.1f, -0.02f, 0)); Mesh(leg, bottom, h.LegL, Vector3.zero, Vector3.one);
            h.LegR = Node("LegR", h.Pelvis, new Vector3(0.1f, -0.02f, 0)); Mesh(leg, bottom, h.LegR, Vector3.zero, Vector3.one);
            h.ShinL = Node("ShinL", h.LegL, new Vector3(0, -0.46f, 0)); Mesh(leg, bottom, h.ShinL, Vector3.zero, Vector3.one); Mesh(shoe, shoeM, h.ShinL, new Vector3(0, -0.46f, 0), Vector3.one);
            h.ShinR = Node("ShinR", h.LegR, new Vector3(0, -0.46f, 0)); Mesh(leg, bottom, h.ShinR, Vector3.zero, Vector3.one); Mesh(shoe, shoeM, h.ShinR, new Vector3(0, -0.46f, 0), Vector3.one);
            h.phase = Random.value * Mathf.PI * 2;
            return h;
        }

        private static void X(Transform t, float rad) { t.localRotation = Quaternion.Euler(rad * Mathf.Rad2Deg, 0, 0); }
        private static void XZ(Transform t, float x, float z) { t.localRotation = Quaternion.Euler(x * Mathf.Rad2Deg, 0, z * Mathf.Rad2Deg); }

        /// <summary>Walk/run cycle from ground speed, or an airborne pose.</summary>
        public void Animate(float speed, float dt, bool airborne)
        {
            if (airborne)
            {
                X(LegL, -0.6f); X(LegR, 0.3f); X(ShinL, 0.9f); X(ShinR, 0.4f);
                XZ(ArmL, -0.4f, 0.5f); XZ(ArmR, -0.4f, -0.5f); return;
            }
            float run = Mathf.Clamp01((speed - 2.5f) / 3.5f);
            phase += dt * (speed < 0.1f ? 0 : 2.2f + speed * 1.15f);
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase), amp = Mathf.Clamp01(speed / 2) * Mathf.Lerp(0.45f, 0.85f, run);
            X(LegL, s * amp); X(LegR, -s * amp);
            X(ShinL, Mathf.Max(0, -c) * amp * 1.3f + 0.05f); X(ShinR, Mathf.Max(0, c) * amp * 1.3f + 0.05f);
            XZ(ArmL, -s * amp * 0.9f, 0.08f); XZ(ArmR, s * amp * 0.9f, -0.08f);
            X(ForeL, -0.25f - run); X(ForeR, -0.25f - run);
            Chest.localRotation = Quaternion.Euler(run * 0.22f * Mathf.Rad2Deg, s * amp * 0.15f * Mathf.Rad2Deg, 0);
            Pelvis.localPosition = new Vector3(0, 0.98f + Mathf.Abs(c) * amp * 0.06f - run * 0.04f, 0);
        }

        /// <summary>Seated driving pose.</summary>
        public void Sit()
        {
            X(LegL, -1.4f); X(LegR, -1.4f); X(ShinL, 1.4f); X(ShinR, 1.4f);
            X(ArmL, -0.9f); X(ArmR, -0.9f); X(ForeL, -0.4f); X(ForeR, -0.4f);
        }
    }
}
