using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityGame.Runtime
{
    /// <summary>
    /// Creates and caches one shared material per colour, so every building of the
    /// same colour batches. Works on the Built-in pipeline (Standard) and URP (Lit).
    /// Single-thread only (main thread).
    /// </summary>
    /// <example><code>var mat = materials.Get(new Color(0.8f, 0.2f, 0.2f));</code></example>
    public sealed class CityMaterials
    {
        private readonly Dictionary<Color32, Material> cache = new Dictionary<Color32, Material>();
        private readonly Material template;

        /// <summary>Window material whose emission <see cref="DayNightCycle"/> animates.</summary>
        public Material Windows { get; }

        /// <summary>Headlight / street light material (emissive at night).</summary>
        public Material Lamps { get; }

        /// <summary>Finds a lit shader for the active pipeline.</summary>
        public CityMaterials()
        {
            // A material in Resources keeps the shader in player builds; fall back to Shader.Find in the editor.
            Material fromResources = Resources.Load<Material>("CityBaseMaterial");
            Shader shader = null;
            if (GraphicsSettings.currentRenderPipeline != null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null && fromResources != null) shader = fromResources.shader;
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            template = new Material(shader);
            if (template.HasProperty("_Glossiness")) template.SetFloat("_Glossiness", 0.15f);
            if (template.HasProperty("_Smoothness")) template.SetFloat("_Smoothness", 0.15f);

            Windows = new Material(template) { name = "Windows", color = new Color(0.25f, 0.32f, 0.42f) };
            Windows.EnableKeyword("_EMISSION");
            Lamps = new Material(template) { name = "Lamps", color = new Color(0.9f, 0.9f, 0.8f) };
            Lamps.EnableKeyword("_EMISSION");
        }

        /// <summary>Shared material for a colour.</summary>
        public Material Get(Color color)
        {
            Color32 key = color;
            if (!cache.TryGetValue(key, out Material m))
            {
                m = new Material(template) { name = "City_" + ColorUtility.ToHtmlStringRGB(color), color = color };
                cache.Add(key, m);
            }
            return m;
        }

        /// <summary>Sets night-time glow, 0 = day, 1 = full night.</summary>
        public void SetNight(float night)
        {
            Windows.SetColor("_EmissionColor", new Color(1f, 0.82f, 0.45f) * (night * 1.6f));
            Lamps.SetColor("_EmissionColor", new Color(1f, 0.95f, 0.75f) * (night * 2.2f));
        }
    }
}
