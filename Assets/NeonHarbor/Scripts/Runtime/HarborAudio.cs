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
        private volatile int station = -1;
        // radio sequencer state (audio thread only)
        private double radioClock; private int radioStep = -1; private float kickEnv, snareEnv, hatEnv, bassEnv, leadEnv, padEnv, bassHz, leadHz, kickPh, bassPh, leadPh; private readonly float[] padHz = new float[3]; private readonly float[] padPh = new float[3]; private float leadLp;
        private static readonly int[] Roots = { 57, 53, 48, 55 }; // A minor: i–VI–III–VII
        private static readonly float[] Bpm = { 108, 88, 70 };
        private double phaseA, phaseB, sirenPhase; private float lp, noiseLp; private int rate;
        private System.Random rng = new System.Random(1);
        private AudioSource oneShots;
        private AudioClip horn, crash, cash, win, fail, star, pistol, smg, blast, click, reloadClick, swing;

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
            pistol = Gunshot(0.22f, 0.9f, 0.10f); smg = Gunshot(0.14f, 0.7f, 0.05f); blast = Noise(1.8f, 1f);
            click = Tone(new[] { 900f }, 0.03f, true, 0.12f); reloadClick = Seq(new[] { 1800f, 1200f }, 0.03f, 0.12f); swing = Gunshot(0.12f, 0.25f, 0.6f);
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

        /// <summary>Radio station, -1 = off (set from the main thread).</summary>
        public void SetStation(int s) { station = s; }

        private static float Mtof(int m) { return 440f * Mathf.Pow(2f, (m - 69) / 12f); }

        // one sixteenth-note step of the current station's pattern
        private void RadioStep(int s, int step)
        {
            int bar = (step / 16) % 4, s16 = step % 16, root = Roots[bar];
            if (s == 0)
            {
                bassHz = Mtof(root - 12 + (s16 % 2 == 1 ? 12 : 0)); bassEnv = 1;
                if (s16 % 2 == 0) { leadHz = Mtof(root + 12 + new[] { 0, 3, 7, 12, 7, 3, 0, 7 }[(s16 / 2) % 8]); leadEnv = 1; }
                if (s16 % 4 == 0) kickEnv = 1; if (s16 == 4 || s16 == 12) snareEnv = 1; if (s16 % 2 == 1) hatEnv = 1;
                if (s16 == 0) { padHz[0] = Mtof(root + 12); padHz[1] = Mtof(root + 15); padHz[2] = Mtof(root + 19); padEnv = 1; }
            }
            else if (s == 1)
            {
                if (s16 == 0 || s16 == 7 || s16 == 10) kickEnv = 1; if (s16 == 4 || s16 == 12) snareEnv = 1; if (s16 % 2 == 0) hatEnv = 0.8f;
                if (s16 == 0 || s16 == 10) { bassHz = Mtof(root - 24); bassEnv = 1; }
                if (s16 == 2 || s16 == 6 || s16 == 14) { leadHz = Mtof(root + new[] { 3, 7, 10 }[(s16 / 4) % 3]); leadEnv = 0.8f; }
            }
            else
            {
                if (s16 == 0) { padHz[0] = Mtof(root); padHz[1] = Mtof(root + 7); padHz[2] = Mtof(root + 15); padEnv = 1; }
                if (s16 == 8 && (step * 7919 % 10) < 6) { leadHz = Mtof(root + 24 + new[] { 0, 3, 7, 10 }[(step / 16) % 4]); leadEnv = 0.7f; }
            }
        }

        private float RadioSample(float noise)
        {
            int s = station;
            if (s < 0) { radioStep = -1; return 0; }
            double sixteenth = 60.0 / Bpm[s] / 4.0;
            radioClock += 1.0 / rate;
            if (radioStep < 0) { radioStep = 0; radioClock = 0; RadioStep(s, 0); }
            while (radioClock >= sixteenth) { radioClock -= sixteenth; radioStep++; RadioStep(s, radioStep); }
            float dtS = 1f / rate, o = 0;
            // kick: falling sine
            kickPh += (40 + 100 * kickEnv) * dtS; o += Mathf.Sin(kickPh * 2 * Mathf.PI) * kickEnv * 0.55f; kickEnv *= 1 - 12 * dtS;
            o += noise * snareEnv * 0.25f; snareEnv *= 1 - 18 * dtS;
            o += noise * hatEnv * 0.06f; hatEnv *= 1 - 60 * dtS;
            bassPh += bassHz * dtS; float saw = (bassPh % 1f) * 2 - 1; o += (s == 1 ? Mathf.Sin(bassPh * 2 * Mathf.PI) * 0.35f : saw * 0.1f) * bassEnv; bassEnv *= 1 - (s == 1 ? 3 : 9) * dtS;
            leadPh += leadHz * dtS; float sq = (leadPh % 1f) < 0.5f ? 1 : -1; leadLp += 0.15f * (sq - leadLp); o += (s == 2 ? Mathf.Sin(leadPh * 2 * Mathf.PI) : leadLp) * leadEnv * 0.05f; leadEnv *= 1 - (s == 2 ? 1.2f : 6) * dtS;
            for (int k = 0; k < 3; k++) { padPh[k] += padHz[k] * dtS; o += Mathf.Sin(padPh[k] * 2 * Mathf.PI) * padEnv * 0.03f; }
            padEnv *= 1 - (s == 2 ? 0.25f : 0.8f) * dtS;
            if (kickPh > 1000) kickPh -= 1000; if (bassPh > 1000) bassPh -= 1000; if (leadPh > 1000) leadPh -= 1000;
            for (int k = 0; k < 3; k++) if (padPh[k] > 1000) padPh[k] -= 1000;
            return o * 0.55f;
        }

        public void Horn() { oneShots.PlayOneShot(horn); }
        public void Crash(float v) { oneShots.PlayOneShot(crash, Mathf.Clamp01(v)); }
        public void Cash() { oneShots.PlayOneShot(cash); }
        public void Win() { oneShots.PlayOneShot(win); }
        public void Fail() { oneShots.PlayOneShot(fail); }
        public void Star() { oneShots.PlayOneShot(star); }
        public void Shot(bool smgSound, float volume = 1) { oneShots.PlayOneShot(smgSound ? smg : pistol, volume); }
        public void Blast() { oneShots.PlayOneShot(blast); }
        public void Click(bool reloadDone) { oneShots.PlayOneShot(reloadDone ? reloadClick : click); }
        public void Swing() { oneShots.PlayOneShot(swing, 0.5f); }

        private AudioClip Gunshot(float dur, float vol, float crack)
        {
            int n = (int)(rate * dur); var d = new float[n]; var r = new System.Random(11); float low = 0;
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)rate, noise = (float)r.NextDouble() * 2 - 1; low += 0.15f * (noise - low);
                d[k] = (noise * crack * Mathf.Exp(-t * 60) + low * 2.5f * Mathf.Exp(-t * 14) + Mathf.Sin(t * 2 * Mathf.PI * 110) * 0.5f * Mathf.Exp(-t * 25)) * vol;
            }
            var clip = AudioClip.Create("shot", n, 1, rate, false); clip.SetData(d, 0); return clip;
        }

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
                float s = lp * engineGain + (noise - noiseLp) * skidGain + (float)System.Math.Sin(sirenPhase * 2 * System.Math.PI) * sirenGain + RadioSample(noise);
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
