using UnityEngine;

namespace NeonHarbor.Runtime
{
    /// <summary>
    /// Procedural sound with no audio assets: a synthesised engine (saw + square
    /// through a low-pass, pitch from speed and gear), tyre squeal, siren, and
    /// generated one-shots (horn, crash, cash, mission jingles).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class HarborAudio : MonoBehaviour
    {
        private volatile float engineHz, engineGain, cutoff = 600, skidGain, sirenGain, sirenHz = 950;
        private double phaseA, phaseB, sirenPhase; private float lp, noiseLp; private int rate;
        private System.Random rng = new System.Random(1);
        private AudioSource oneShots;
        private AudioClip horn, crash, cash, win, fail, star;

        private void Awake()
        {
            rate = AudioSettings.outputSampleRate;
            var src = GetComponent<AudioSource>(); src.playOnAwake = true; src.loop = true; src.spatialBlend = 0; src.volume = 0.6f; src.Play();
            oneShots = gameObject.AddComponent<AudioSource>(); oneShots.spatialBlend = 0; oneShots.volume = 0.8f;
            horn = Tone(new[] { 392f, 494f }, 0.35f, true, 0.25f);
            crash = Noise(0.5f, 0.9f);
            cash = Seq(new[] { 880f, 1320f }, 0.08f, 0.2f);
            win = Seq(new[] { 523f, 659f, 784f, 1046f }, 0.13f, 0.25f);
            fail = Seq(new[] { 392f, 311f, 262f }, 0.2f, 0.2f);
            star = Seq(new[] { 660f, 440f }, 0.12f, 0.15f);
        }

        /// <summary>Per-frame parameters from the game.</summary>
        public void Tick(Vehicle car, float sirenLevel)
        {
            if (car != null)
            {
                float sp = Mathf.Abs(car.Model.ForwardSpeed); int gear = Mathf.Min(5, (int)(sp / 11)); float within = (sp - gear * 11) / 11f;
                float rpm = 900 + within * 4800 + (car.Input.Throttle != 0 ? 400 : 0);
                engineHz = rpm / 60f * 2f; engineGain = 0.08f + Mathf.Abs(car.Input.Throttle) * 0.1f; cutoff = 500 + Mathf.Abs(car.Input.Throttle) * 1600;
                skidGain = Mathf.Clamp01((car.Model.Slip - 3.5f) / 10f) * 0.35f * (sp > 4 ? 1 : 0);
            }
            else { engineGain = 0; skidGain = 0; }
            sirenGain = sirenLevel * 0.09f; sirenHz = Mathf.Sin(Time.time * 3.2f) > 0 ? 1350 : 950;
        }

        public void Horn() { oneShots.PlayOneShot(horn); }
        public void Crash(float v) { oneShots.PlayOneShot(crash, Mathf.Clamp01(v)); }
        public void Cash() { oneShots.PlayOneShot(cash); }
        public void Win() { oneShots.PlayOneShot(win); }
        public void Fail() { oneShots.PlayOneShot(fail); }
        public void Star() { oneShots.PlayOneShot(star); }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (rate == 0) return;
            float a = Mathf.Clamp01(2 * Mathf.PI * cutoff / rate);
            for (int i = 0; i < data.Length; i += channels)
            {
                phaseA += engineHz / rate; phaseB += engineHz * 0.5 / rate; sirenPhase += sirenHz / rate;
                float saw = (float)(phaseA % 1.0) * 2 - 1, sq = (phaseB % 1.0) < 0.5 ? 0.35f : -0.35f;
                lp += a * ((saw + sq) - lp);
                float noise = (float)rng.NextDouble() * 2 - 1; noiseLp += 0.6f * (noise - noiseLp);
                float s = lp * engineGain + (noise - noiseLp) * skidGain + (float)System.Math.Sin(sirenPhase * 2 * System.Math.PI) * sirenGain;
                for (int c = 0; c < channels; c++) data[i + c] = s;
            }
        }

        private AudioClip Tone(float[] freqs, float dur, bool square, float vol)
        {
            int n = (int)(rate * dur); var d = new float[n];
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)rate, env = Mathf.Min(1, t * 80) * Mathf.Exp(-t * 2);
                foreach (var f in freqs) { float ph = (t * f) % 1f; d[k] += (square ? (ph < 0.5f ? 1 : -1) : Mathf.Sin(ph * Mathf.PI * 2)) * vol * env / freqs.Length; }
            }
            var clip = AudioClip.Create("tone", n, 1, rate, false); clip.SetData(d, 0); return clip;
        }

        private AudioClip Seq(float[] freqs, float step, float vol)
        {
            int n = (int)(rate * step * (freqs.Length + 1)); var d = new float[n];
            for (int i = 0; i < freqs.Length; i++)
            {
                int start = (int)(rate * step * 0.8f * i);
                for (int k = 0; k < rate * step * 1.6f && start + k < n; k++) { float t = k / (float)rate; d[start + k] += Mathf.Sin(t * freqs[i] * Mathf.PI * 2) * vol * Mathf.Min(1, t * 100) * Mathf.Exp(-t * 6); }
            }
            var clip = AudioClip.Create("seq", n, 1, rate, false); clip.SetData(d, 0); return clip;
        }

        private AudioClip Noise(float dur, float vol)
        {
            int n = (int)(rate * dur); var d = new float[n]; var r = new System.Random(3); float low = 0;
            for (int k = 0; k < n; k++) { float t = k / (float)rate; low += 0.08f * ((float)r.NextDouble() * 2 - 1 - low); d[k] = (low * 3 + ((float)r.NextDouble() * 2 - 1) * 0.3f) * vol * Mathf.Exp(-t * 7); }
            var clip = AudioClip.Create("crash", n, 1, rate, false); clip.SetData(d, 0); return clip;
        }
    }
}
