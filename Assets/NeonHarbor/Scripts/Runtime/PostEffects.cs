using UnityEngine;
#if NEON_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace NeonHarbor.Runtime
{
    /// <summary>
    /// Bloom + ACES tone mapping + grading + vignette. Built-in pipeline: a small
    /// OnRenderImage chain using Shaders/NeonHarborPost.shader. URP (define NEON_URP,
    /// set automatically when the URP package is installed): an equivalent global Volume.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PostEffects : MonoBehaviour
    {
        /// <summary>Linear exposure before tone mapping.</summary>
        public float Exposure = 1.05f;
        /// <summary>Bloom brightness threshold (linear).</summary>
        public float Threshold = 1.0f;
        /// <summary>Bloom strength.</summary>
        public float Intensity = 0.6f;
        /// <summary>Saturation multiplier.</summary>
        public float Saturation = 1.12f;
        /// <summary>Vignette strength.</summary>
        public float Vignette = 0.3f;
        /// <summary>Full-screen colour flash (busted / wasted), alpha = strength.</summary>
        public Color Flash = Color.clear;

        private Material mat;
        private readonly RenderTexture[] chain = new RenderTexture[6];

        private void Awake()
        {
            var cam = GetComponent<Camera>();
            cam.allowHDR = true;
#if NEON_URP
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var vol = gameObject.AddComponent<Volume>(); vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); vol.profile = profile;
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(1.1f); bloom.threshold.Override(0.95f); bloom.scatter.Override(0.72f);
            var tm = profile.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            var ca = profile.Add<ColorAdjustments>(true); ca.saturation.Override(12f); ca.contrast.Override(8f);
            var vg = profile.Add<UnityEngine.Rendering.Universal.Vignette>(true); vg.intensity.Override(0.28f);
            enabled = false; // URP ignores OnRenderImage
#else
            var res = Resources.Load<Material>("NeonHarborPost");
            var shader = res != null ? res.shader : Shader.Find("Hidden/NeonHarbor/Post");
            if (shader == null || !shader.isSupported) { enabled = false; return; }
            mat = new Material(shader);
#endif
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (mat == null) { Graphics.Blit(src, dst); return; }
            mat.SetFloat("_Threshold", Threshold); mat.SetFloat("_Intensity", Intensity); mat.SetFloat("_Exposure", Exposure);
            mat.SetFloat("_Saturation", Saturation); mat.SetFloat("_Vignette", Vignette); mat.SetColor("_Flash", Flash);
            int w = src.width / 2, h = src.height / 2, n = 0;
            var fmt = RenderTextureFormat.DefaultHDR;
            chain[0] = RenderTexture.GetTemporary(w, h, 0, fmt);
            Graphics.Blit(src, chain[0], mat, 0); // prefilter
            for (n = 1; n < chain.Length; n++)
            {
                w /= 2; h /= 2; if (w < 4 || h < 4) break;
                chain[n] = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(chain[n - 1], chain[n], mat, 1); // downsample
            }
            for (int k = n - 1; k > 0; k--) Graphics.Blit(chain[k], chain[k - 1], mat, 2); // additive upsample
            mat.SetTexture("_BloomTex", chain[0]);
            Graphics.Blit(src, dst, mat, 3); // composite + tone map
            for (int k = 0; k < n; k++) { RenderTexture.ReleaseTemporary(chain[k]); chain[k] = null; }
        }
    }
}
