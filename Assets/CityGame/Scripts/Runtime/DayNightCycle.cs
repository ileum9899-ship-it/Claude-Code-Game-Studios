using UnityEngine;

namespace CityGame.Runtime
{
    /// <summary>Rotates the sun, tints ambient light and sky, and lights windows at night.</summary>
    public sealed class DayNightCycle : MonoBehaviour
    {
        private Light sun;
        private Camera cam;
        private CityMaterials materials;

        /// <summary>0..1 through the day; 0.5 is noon.</summary>
        public float TimeOfDay { get; private set; } = 0.35f;
        /// <summary>Real seconds for a full cycle at 1x.</summary>
        public float CycleSeconds = 150f;
        /// <summary>Speed multiplier from the game clock.</summary>
        public float TimeScale { get; set; } = 1f;

        /// <summary>True between dusk and dawn.</summary>
        public bool IsNight => TimeOfDay < 0.23f || TimeOfDay > 0.77f;

        /// <summary>Wires dependencies.</summary>
        public void Init(Light sunLight, Camera camera, CityMaterials mats)
        {
            sun = sunLight;
            cam = camera;
            materials = mats;
            Apply();
        }

        /// <summary>Jumps to a time of day (0..1).</summary>
        public void SetTime(float t)
        {
            TimeOfDay = Mathf.Repeat(t, 1f);
            Apply();
        }

        private void Update()
        {
            if (sun == null) return;
            TimeOfDay = Mathf.Repeat(TimeOfDay + Time.deltaTime * TimeScale / CycleSeconds, 1f);
            Apply();
        }

        private void Apply()
        {
            // Sun elevation: -90 at midnight, +90 at noon.
            float elevation = (TimeOfDay - 0.25f) * 360f;
            sun.transform.rotation = Quaternion.Euler(elevation, 35f, 0f);
            float day = Mathf.Clamp01(Mathf.Sin((TimeOfDay - 0.25f) * Mathf.PI * 2f) * 2.5f + 0.4f);
            float night = 1f - day;

            sun.intensity = Mathf.Lerp(0.05f, 1.15f, day);
            sun.color = Color.Lerp(new Color(1f, 0.55f, 0.35f), new Color(1f, 0.96f, 0.9f), Mathf.Clamp01(day * 1.5f - 0.3f));
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(new Color(0.12f, 0.14f, 0.25f), new Color(0.55f, 0.58f, 0.62f), day);
            if (cam != null) cam.backgroundColor = Color.Lerp(new Color(0.03f, 0.04f, 0.1f), new Color(0.55f, 0.75f, 0.95f), day);
            if (materials != null) materials.SetNight(night);
        }
    }
}
