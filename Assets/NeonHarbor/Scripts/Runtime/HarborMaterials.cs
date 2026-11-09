using System.Collections.Generic;
using NeonHarbor.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonHarbor.Runtime
{
    /// <summary>
    /// Every material the game uses, built at runtime from procedural textures
    /// (no imported art). Works on the Built-in pipeline (Standard) and URP (Lit).
    /// Night-time glow is driven through <see cref="SetNight"/>.
    /// </summary>
    public sealed class HarborMaterials
    {
        private readonly Shader lit;
        private readonly bool urp;
        private readonly Dictionary<FacadeStyle, Material> facades = new Dictionary<FacadeStyle, Material>();
        private readonly Dictionary<Color32, Material> paints = new Dictionary<Color32, Material>();
        private readonly Dictionary<Color32, Material> plain = new Dictionary<Color32, Material>();
        private readonly Dictionary<Color32, Material> glows = new Dictionary<Color32, Material>();

        public Material Asphalt, Slab, Curb, Grass, Roof, MarkWhite, MarkYellow, Metal, MetalLight, Lamp, RedBlink,
            Trunk, Water, Crane, Quay, Glass, Tire, Rim, Chrome, HeadLight, TailLight, BrakeLight, Under, Pool, Beam, Skin;
        public readonly Material[] Leaves = new Material[3];

        /// <summary>Finds the lit shader for the active render pipeline and builds all materials.</summary>
        public HarborMaterials()
        {
            urp = GraphicsSettings.currentRenderPipeline != null;
            lit = urp ? Shader.Find("Universal Render Pipeline/Lit") : null;
            if (lit == null)
            {
                var res = Resources.Load<Material>("NeonHarborBase");
                lit = res != null ? res.shader : Shader.Find("Standard");
            }

            foreach (FacadeStyle s in System.Enum.GetValues(typeof(FacadeStyle))) facades[s] = MakeFacade(s);
            Asphalt = Make("Asphalt", Color.white, 0.18f, 0f, ProceduralTextures.Asphalt());
            Slab = Make("Slab", Color.white, 0.12f, 0f, ProceduralTextures.Concrete());
            Curb = Make("Curb", Hex(0x8e8b84), 0.1f, 0f);
            Grass = Make("Grass", Color.white, 0.02f, 0f, ProceduralTextures.Grass());
            Roof = Make("Roof", Hex(0xbbbbbb), 0.08f, 0f, ProceduralTextures.Roof());
            MarkWhite = Make("MarkWhite", Hex(0xe8e8e2), 0.4f, 0f);
            MarkYellow = Make("MarkYellow", Hex(0xf2b31a), 0.4f, 0f);
            Metal = Make("Metal", Hex(0x3b4045), 0.55f, 0.8f);
            MetalLight = Make("MetalLight", Hex(0x9aa0a6), 0.6f, 0.7f);
            Trunk = Make("Trunk", Hex(0x5a4330), 0f, 0f);
            Crane = Make("Crane", Hex(0xd9542b), 0.4f, 0.4f);
            Quay = Make("Quay", Hex(0xb0aaa0), 0.1f, 0f, ProceduralTextures.Concrete());
            Glass = Make("Glass", Hex(0x05080c), 0.96f, 0.3f);
            Tire = Make("Tire", Hex(0x161616), 0.08f, 0f);
            Rim = Make("Rim", Hex(0xb9bec4), 0.75f, 1f);
            Chrome = Make("Trim", Hex(0x202326), 0.6f, 0.6f);
            Under = Make("Under", Hex(0x0c0c0d), 0.1f, 0f);
            Skin = Make("Skin", Hex(0xe0ac85), 0.4f, 0f);
            Water = Make("Water", Hex(0x0b2b3c), 0.95f, 0.1f);
            Water.SetTexture("_BumpMap", ProceduralTextures.WaterNormals());
            Water.EnableKeyword("_NORMALMAP");
            Water.SetFloat("_BumpScale", 0.6f);
            Leaves[0] = Make("Leaf0", Hex(0x3f7a2e), 0.05f, 0f); Leaves[1] = Make("Leaf1", Hex(0x4f8a34), 0.05f, 0f); Leaves[2] = Make("Leaf2", Hex(0x5d8f3a), 0.05f, 0f);
            Lamp = MakeGlow("Lamp", new Color(1f, 0.85f, 0.6f), 1f);
            RedBlink = MakeGlow("Blink", new Color(1f, 0.05f, 0.03f), 4f);
            HeadLight = MakeGlow("Head", new Color(1f, 0.96f, 0.85f), 2.2f);
            TailLight = MakeGlow("Tail", new Color(1f, 0.04f, 0.02f), 1.2f);
            BrakeLight = MakeGlow("Brake", new Color(1f, 0.04f, 0.02f), 6f);
            Pool = MakeAdditive("LightPool", ProceduralTextures.RadialGlow(), new Color(1f, 0.75f, 0.45f, 0f));
            Beam = MakeAdditive("HeadBeam", ProceduralTextures.Beam(), new Color(1f, 0.95f, 0.8f, 0f));
        }

        /// <summary>Facade material for a building style.</summary>
        public Material Facade(FacadeStyle s) { return facades[s]; }

        /// <summary>Glossy car paint, shared per colour.</summary>
        public Material Paint(Color c)
        {
            Color32 k = c;
            if (!paints.TryGetValue(k, out var m)) paints[k] = m = Make("Paint", c, 0.82f, 0.45f);
            return m;
        }

        /// <summary>Plain matte material, shared per colour (clothes, containers, props).</summary>
        public Material Plain(Color c, float smooth = 0.2f)
        {
            Color32 k = c;
            if (!plain.TryGetValue(k, out var m)) plain[k] = m = Make("Plain", c, smooth, 0f);
            return m;
        }

        /// <summary>Emissive material, shared per colour (signal lamps, sirens, neon).</summary>
        public Material Glow(Color c, float intensity)
        {
            Color32 k = c * Mathf.Min(1f, intensity / 8f);
            if (!glows.TryGetValue(k, out var m)) glows[k] = m = MakeGlow("Glow", c, intensity);
            return m;
        }

        /// <summary>Updates window glow, lamps, beams. 0 = day, 1 = night.</summary>
        public void SetNight(float night, float dusk)
        {
            float win = Mathf.Lerp(0f, 1.4f, Mathf.Clamp01(night * 1.2f + dusk * 0.2f));
            foreach (var m in facades.Values) SetEmission(m, Color.white * win);
            SetEmission(Lamp, new Color(1f, 0.82f, 0.55f) * Mathf.Lerp(0.6f, 7f, night));
            SetEmission(HeadLight, new Color(1f, 0.96f, 0.85f) * Mathf.Lerp(1.2f, 4f, night));
            SetTint(Pool, new Color(1f, 0.75f, 0.45f, night * 0.5f));
            SetTint(Beam, new Color(1f, 0.95f, 0.8f, night * 0.3f));
        }

        /// <summary>Sets an emissive colour on any lit material.</summary>
        public static void SetEmission(Material m, Color c)
        {
            m.SetColor("_EmissionColor", c);
        }

        private static void SetTint(Material m, Color c)
        {
            m.SetColor("_TintColor", c); m.SetColor("_Color", c);
        }

        private Material MakeFacade(FacadeStyle s)
        {
            var spec = ProceduralTextures.FacadeSpec(s);
            var (albedo, emission) = ProceduralTextures.Facade(s);
            var m = Make("Facade_" + s, Color.white, 1f - spec.rough, spec.metal, albedo);
            m.SetTexture("_EmissionMap", emission);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            SetEmission(m, Color.black);
            return m;
        }

        private Material Make(string name, Color color, float smoothness, float metallic, Texture2D tex = null)
        {
            var m = new Material(lit) { name = name, enableInstancing = true };
            m.SetColor(urp ? "_BaseColor" : "_Color", color);
            m.SetFloat(urp ? "_Smoothness" : "_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (tex != null) m.SetTexture(urp ? "_BaseMap" : "_MainTex", tex);
            return m;
        }

        private Material MakeGlow(string name, Color c, float intensity)
        {
            var m = Make(name, c * 0.2f, 0.5f, 0f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            SetEmission(m, c * intensity);
            return m;
        }

        private static Material MakeAdditive(string name, Texture2D tex, Color tint)
        {
            var shader = Resources.Load<Material>("NeonHarborAdditive")?.shader ?? Shader.Find("NeonHarbor/Additive");
            var m = new Material(shader) { name = name, mainTexture = tex, renderQueue = 3010 };
            SetTint(m, tint);
            return m;
        }

        /// <summary>sRGB hex to Color.</summary>
        public static Color Hex(int hex) { return new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f); }
    }
}
