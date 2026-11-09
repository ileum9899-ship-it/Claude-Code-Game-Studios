using System;
using System.Collections.Generic;

namespace NeonHarbor.Core
{
    /// <summary>
    /// GTA-style wanted level.
    /// <list type="bullet">
    /// <item>Crimes seen by police add heat at once; crimes seen only by bystanders are phoned in
    /// after <see cref="HarborConfig.WitnessDelay"/> seconds; unseen crimes don't count.</item>
    /// <item>While any cop can see the player, the last known position follows them.</item>
    /// <item>Out of sight, the stars flash and police search a circle around the last known
    /// position (radius grows with stars). Leave the circle and stay unseen for
    /// <see cref="HarborConfig.EvadeSeconds"/> to lose them; being spotted again re-centres it.</item>
    /// <item>A nearly stopped player next to a cop for <see cref="HarborConfig.BustSeconds"/> is arrested.</item>
    /// </list>
    /// </summary>
    /// <example><code>
    /// wanted.ReportCrime(1f, "보행자 공격", copSees: false, witnessed: true);
    /// wanted.Tick(dt, seen, playerPos, playerSpeed, nearestCop);
    /// </code></example>
    public sealed class WantedSystem
    {
        private readonly HarborConfig cfg;
        private readonly List<(float delay, float amount, string reason)> reports = new List<(float, float, string)>();
        private bool needsAnchor;

        /// <summary>Accumulated heat.</summary>
        public float Heat { get; private set; }
        /// <summary>Current stars (0..MaxStars).</summary>
        public int Stars { get; private set; }
        /// <summary>Seconds spent outside the search area, unseen.</summary>
        public float EvadeTimer { get; private set; }
        /// <summary>Seconds spent being arrested.</summary>
        public float BustTimer { get; private set; }
        /// <summary>True while wanted and no cop can see the player.</summary>
        public bool Searching { get; private set; }
        /// <summary>Where police last saw the player (centre of the search area).</summary>
        public Vec2 LastKnown { get; private set; }
        /// <summary>Radius of the search area (m).</summary>
        public float SearchRadius => cfg.SearchRadiusBase + cfg.SearchRadiusPerStar * Stars;
        /// <summary>Witness reports still being phoned in.</summary>
        public int PendingReports => reports.Count;

        /// <summary>Raised with (stars, reason) when stars go up.</summary>
        public event Action<int, string> StarsRaised;
        /// <summary>Raised when the player shakes the police.</summary>
        public event Action Evaded;
        /// <summary>Raised when the arrest completes.</summary>
        public event Action Busted;
        /// <summary>Raised when a bystander's call comes through (reason).</summary>
        public event Action<string> WitnessReported;

        /// <summary>Creates the system.</summary>
        public WantedSystem(HarborConfig config) { cfg = config ?? throw new ArgumentNullException(nameof(config)); }

        /// <summary>Police cars that should be chasing right now.</summary>
        public int DesiredCops => Math.Min(cfg.MaxCops, Stars * cfg.CopsPerStar);

        /// <summary>Adds heat directly (crime seen by police).</summary>
        public void AddHeat(float amount, string reason)
        {
            int before = Stars;
            Heat = Mathx.Clamp(Heat + amount, 0, cfg.MaxStars + 0.99f);
            Stars = Math.Min(cfg.MaxStars, (int)Math.Floor(Heat));
            EvadeTimer = 0;
            if (before == 0 && Stars > 0) needsAnchor = true;
            if (Stars > before) StarsRaised?.Invoke(Stars, reason);
        }

        /// <summary>A crime happened. Returns true if it will count (seen by police or a witness).</summary>
        public bool ReportCrime(float amount, string reason, bool copSees, bool witnessed)
        {
            if (copSees) { AddHeat(amount, reason); return true; }
            if (!witnessed) return false;
            reports.Add((cfg.WitnessDelay, amount, reason));
            return true;
        }

        /// <summary>Clears everything (respawn, spray garage).</summary>
        public void Clear() { Heat = 0; Stars = 0; EvadeTimer = 0; BustTimer = 0; Searching = false; reports.Clear(); }

        /// <summary>Advances witness calls, the search and the arrest timer.</summary>
        public void Tick(float dt, bool seenByPolice, Vec2 player, float playerSpeed, float nearestCopDistance)
        {
            for (int k = reports.Count - 1; k >= 0; k--)
            {
                var r = reports[k]; r.delay -= dt;
                if (r.delay > 0) { reports[k] = r; continue; }
                reports.RemoveAt(k);
                AddHeat(r.amount, r.reason);
                WitnessReported?.Invoke(r.reason);
            }

            if (Stars == 0) { BustTimer = 0; Searching = false; return; }
            if (needsAnchor) { LastKnown = player; needsAnchor = false; } // the report says where you are
            if (seenByPolice)
            {
                LastKnown = player; Searching = false; EvadeTimer = 0;
            }
            else
            {
                Searching = true;
                if (Vec2.Distance(player, LastKnown) > SearchRadius)
                {
                    EvadeTimer += dt;
                    if (EvadeTimer > cfg.EvadeSeconds) { Clear(); Evaded?.Invoke(); return; }
                }
            }

            if (nearestCopDistance < cfg.BustRadius && playerSpeed < 2f)
            {
                BustTimer += dt;
                if (BustTimer > cfg.BustSeconds) { BustTimer = 0; Busted?.Invoke(); }
            }
            else BustTimer = Math.Max(0, BustTimer - dt);
        }
    }
}
