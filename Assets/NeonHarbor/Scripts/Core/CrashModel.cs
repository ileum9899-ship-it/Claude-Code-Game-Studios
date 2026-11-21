using System;

namespace NeonHarbor.Core
{
    /// <summary>
    /// Rules for vehicle accidents: how much damage an impact does, how deep the body
    /// dents, when lights break, when the driver goes through the windscreen, and how much
    /// the engine smokes. Engine-free so both builds share it (browser twin: src/12-crash.js).
    /// </summary>
    /// <example><code>
    /// car.Health -= CrashModel.Damage(impactSpeed);
    /// if (CrashModel.EjectsDriver(frontalSpeed)) ThrowDriver();
    /// </code></example>
    public static class CrashModel
    {
        /// <summary>Impacts below this (m/s) leave no damage.</summary>
        public const float MinImpact = 3f;
        /// <summary>Frontal impact (m/s, ≈ 86 km/h) that throws the driver through the windscreen.</summary>
        public const float EjectSpeed = 24f;
        /// <summary>Impact (m/s) that breaks the lights on the struck end.</summary>
        public const float LightBreakSpeed = 9f;

        /// <summary>Health lost for an impact of <paramref name="impactSpeed"/> m/s (grows faster than linear).</summary>
        public static float Damage(float impactSpeed)
        {
            if (impactSpeed < MinImpact) return 0;
            return Math.Min(120f, 1.1f * (float)Math.Pow(impactSpeed - MinImpact, 1.35));
        }

        /// <summary>How far (m) the body panel at the contact point is pushed in.</summary>
        public static float DentDepth(float impactSpeed) { return Mathx.Clamp((impactSpeed - 4f) * 0.012f, 0f, 0.28f); }

        /// <summary>True when the impact is hard enough to smash the lights on that end.</summary>
        public static bool LightsBreak(float impactSpeed) { return impactSpeed >= LightBreakSpeed; }

        /// <summary>
        /// Share of the impact that hit the front of the car: the contact normal (pointing out of the
        /// obstacle, into the car) against the car's forward direction. 1 = head-on, 0 = side or rear.
        /// </summary>
        public static float Frontal(Vec2 carForward, Vec2 normalIntoCar)
        {
            return Math.Max(0f, -carForward.Normalized.Dot(normalIntoCar.Normalized));
        }

        /// <summary>True when a head-on impact throws the driver out.</summary>
        public static bool EjectsDriver(float impactSpeed, float frontal)
        {
            return impactSpeed * frontal >= EjectSpeed;
        }

        /// <summary>Damage to a driver thrown through the windscreen.</summary>
        public static float EjectionInjury(float impactSpeed) { return Mathx.Clamp(15f + (impactSpeed - EjectSpeed) * 2.5f, 15f, 60f); }

        /// <summary>Engine smoke: 0 none, 1 grey (below 50 health), 2 thick black (below 25).</summary>
        public static int SmokeLevel(float health) { return health < 25 ? 2 : health < 50 ? 1 : 0; }

        /// <summary>Top-speed multiplier for a damaged car (down to 55 % when wrecked).</summary>
        public static float Performance(float health) { return Mathx.Lerp(0.55f, 1f, Mathx.Clamp01(health / 60f)); }
    }
}
