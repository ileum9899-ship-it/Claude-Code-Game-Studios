namespace NeonHarbor.Core
{
    /// <summary>Signal aspect.</summary>
    public enum Signal { Green = 0, Yellow = 1, Red = 2 }

    /// <summary>
    /// Fixed-time signals: every intersection alternates the x-axis and z-axis
    /// directions with a per-intersection offset so the city doesn't pulse in sync.
    /// </summary>
    /// <example><code>var s = lights.State(3, 4, axis: 0, time: 12.5f);</code></example>
    public sealed class TrafficLights
    {
        private readonly float cycle, yellow;

        /// <summary>Creates a schedule.</summary>
        public TrafficLights(float cycleSeconds, float yellowSeconds) { cycle = cycleSeconds; yellow = yellowSeconds; }

        /// <summary>Offset (s) of intersection (i, j) — same formula as the browser build.</summary>
        public static float Offset(int i, int j) { return (i * 7 + j * 13) % 9 * 2; }

        /// <summary>Aspect for traffic moving along <paramref name="axis"/> (0 = x, 1 = z).</summary>
        public Signal State(int i, int j, int axis, float time)
        {
            float half = cycle / 2f;
            float t = (time + Offset(i, j)) % cycle;
            float local = axis == 0 ? t : (t + half) % cycle;
            if (local < half - yellow) return Signal.Green;
            if (local < half) return Signal.Yellow;
            return Signal.Red;
        }
    }
}
