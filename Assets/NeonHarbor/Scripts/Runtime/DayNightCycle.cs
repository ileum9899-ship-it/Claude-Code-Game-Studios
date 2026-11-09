using NeonHarbor.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonHarbor.Runtime
{
    /// <summary>
    /// Sun / moon, procedural skybox, fog, ambient light and everything that glows at
    /// night (windows, street lamps, neon, light pools, headlights). 0.25 = sunrise,
    /// 0.5 = noon, 0.75 = sunset.
    /// </summary>
    public sealed class DayNightCycle
    {
        private readonly Light sun;
        private readonly Material sky;
        private readonly HarborMaterials mats;
        private readonly System.Collections.Generic.List<(Material material, Color color)> neon;
        private readonly float dayLength;
        private float lastGi = -1;

        /// <summary>Time of day 0..1.</summary>
        public float T = 0.728f;
        /// <summary>0 = day, 1 = night (last update).</summary>
        public float Night { get; private set; }

        /// <summary>Creates the cycle and the sun light.</summary>
        public DayNightCycle(HarborMaterials mats, System.Collections.Generic.List<(Material material, Color color)> neon, float dayLength)
        {
            this.mats = mats; this.neon = neon; this.dayLength = dayLength;
            Light found = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { found = l; break; }
            sun = found != null ? found : new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.8f; sun.shadowBias = 0.04f; sun.shadowNormalBias = 0.3f;
            QualitySettings.shadowDistance = 140; QualitySettings.shadowCascades = 4;
            var baseSky = RenderSettings.skybox;
            var procedural = Shader.Find("Skybox/Procedural");
            sky = baseSky != null && baseSky.shader == procedural ? new Material(baseSky) : (procedural != null ? new Material(procedural) : baseSky);
            RenderSettings.skybox = sky; RenderSettings.sun = sun;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = AmbientMode.Trilight;
        }

        /// <summary>Advances time (speed multiplier from T key) and applies lighting.</summary>
        public void Tick(float dt, Vector3 focus)
        {
            T = Mathf.Repeat(T + dt / dayLength, 1f);
            float th = (T - 0.25f) * Mathx.Tau;
            Vector3 dir = new Vector3(Mathf.Cos(th), Mathf.Sin(th), 0.42f).normalized; // towards the sun
            float elev = dir.y, day = Mathf.Clamp01(elev * 5 + 0.15f), night = Mathf.Clamp01((0.06f - elev) / 0.18f), dusk = Mathf.Clamp01(1 - Mathf.Abs(elev - 0.06f) / 0.16f);
            Night = night;
            Vector3 lightDir = elev > -0.05f ? dir : new Vector3(-dir.x, -dir.y, -dir.z * 0.5f).normalized;
            sun.transform.rotation = Quaternion.LookRotation(-lightDir);
            sun.intensity = elev > -0.05f ? Mathf.Lerp(0.15f, 1.35f, day) : 0.18f;
            sun.color = elev > -0.05f ? Color.Lerp(Color.white, new Color(1f, 0.6f, 0.36f), dusk * 0.85f) : new Color(0.56f, 0.66f, 1f);
            if (sky != null)
            {
                sky.SetFloat("_Exposure", Mathf.Lerp(0.12f, 1.25f, day));
                sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.9f, 1.25f, dusk));
                sky.SetColor("_SkyTint", Color.Lerp(new Color(0.5f, 0.5f, 0.5f), new Color(0.35f, 0.38f, 0.55f), night));
                sky.SetFloat("_SunSize", 0.045f);
            }
            Color fog = Color.Lerp(Color.Lerp(HarborMaterials.Hex(0xa9bdd0), HarborMaterials.Hex(0xd59a72), dusk * 0.7f), HarborMaterials.Hex(0x0b1222), night);
            RenderSettings.fogColor = fog; RenderSettings.fogDensity = Mathf.Lerp(0.0017f, 0.0032f, night);
            RenderSettings.ambientSkyColor = Color.Lerp(HarborMaterials.Hex(0x9fb6d6), HarborMaterials.Hex(0x1a2440), night);
            RenderSettings.ambientEquatorColor = Color.Lerp(fog, HarborMaterials.Hex(0x141a2a), night * 0.6f);
            RenderSettings.ambientGroundColor = Color.Lerp(HarborMaterials.Hex(0x4a4036), HarborMaterials.Hex(0x0a0a10), night);
            mats.SetNight(night, dusk);
            foreach (var (m, c) in neon) HarborMaterials.SetEmission(m, c * Mathf.Lerp(0.9f, 3.4f, night));
            HarborMaterials.SetEmission(mats.RedBlink, new Color(1, 0.05f, 0.03f) * (Mathf.Sin(Time.time * 3) > 0.6f ? 6f : 0.2f));
            if (Mathf.Abs(T - lastGi) > 0.006f) { lastGi = T; DynamicGI.UpdateEnvironment(); }
        }

        /// <summary>Jumps to a time of day.</summary>
        public void SetTime(float t) { T = Mathf.Repeat(t, 1f); lastGi = -1; }
    }
}
