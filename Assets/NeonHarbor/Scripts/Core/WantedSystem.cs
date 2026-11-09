using System;

namespace NeonHarbor.Core
{
    /// <summary>
    /// Wanted level ("heat"): crimes add heat, stars = floor(heat). Staying beyond
    /// <see cref="HarborConfig.EvadeRadius"/> of every cop for <see cref="HarborConfig.EvadeSeconds"/>
    /// clears it; staying nearly stopped within <see cref="HarborConfig.BustRadius"/> of a cop
    /// for <see cref="HarborConfig.BustSeconds"/> is an arrest.
    /// </summary>
    /// <example><code>
    /// wanted.AddHeat(1f, "보행자 사고");
    /// wanted.Tick(dt, nearestCopDistance, playerSpeed);
    /// </code></example>
    public sealed class WantedSystem
    {
        private readonly HarborConfig cfg;

        /// <summary>Accumulated heat.</summary>
        public float Heat { get; private set; }
        /// <summary>Current stars (0..MaxStars).</summary>
        public int Stars { get; private set; }
        /// <summary>Seconds spent out of reach of all cops.</summary>
        public float EvadeTimer { get; private set; }
        /// <summary>Seconds spent being arrested.</summary>
        public float BustTimer { get; private set; }

        /// <summary>Raised with (stars, reason) when stars go up.</summary>
        public event Action<int, string> StarsRaised;
        /// <summary>Raised when the player shakes the police.</summary>
        public event Action Evaded;
        /// <summary>Raised when the arrest completes.</summary>
        public event Action Busted;

        /// <summary>Creates the system.</summary>
        public WantedSystem(HarborConfig config) { cfg = config ?? throw new ArgumentNullException(nameof(config)); }

        /// <summary>Police cars that should be chasing right now.</summary>
        public int DesiredCops => Math.Min(cfg.MaxCops, Stars * cfg.CopsPerStar);

        /// <summary>Adds heat for a crime.</summary>
        public void AddHeat(float amount, string reason)
        {
            int before = Stars;
            Heat = Math.Min(cfg.MaxStars + 0.99f, Heat + amount);
            Stars = Math.Min(cfg.MaxStars, (int)Math.Floor(Heat));
            EvadeTimer = 0;
            if (Stars > before) StarsRaised?.Invoke(Stars, reason);
        }

        /// <summary>Clears everything (respawn).</summary>
        public void Clear() { Heat = 0; Stars = 0; EvadeTimer = 0; BustTimer = 0; }

        /// <summary>Advances evade / arrest timers.</summary>
        public void Tick(float dt, float nearestCopDistance, float playerSpeed)
        {
            if (Stars == 0) { BustTimer = 0; return; }
            if (nearestCopDistance > cfg.EvadeRadius)
            {
                EvadeTimer += dt;
                if (EvadeTimer > cfg.EvadeSeconds) { Clear(); Evaded?.Invoke(); return; }
            }
            else EvadeTimer = 0;

            if (nearestCopDistance < cfg.BustRadius && playerSpeed < 2f)
            {
                BustTimer += dt;
                if (BustTimer > cfg.BustSeconds) { BustTimer = 0; Busted?.Invoke(); }
            }
            else BustTimer = Math.Max(0, BustTimer - dt);
        }
    }
}
