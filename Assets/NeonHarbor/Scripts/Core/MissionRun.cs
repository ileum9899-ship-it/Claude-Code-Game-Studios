using System;
using System.Collections.Generic;

namespace NeonHarbor.Core
{
    /// <summary>Kind of job offered at a mission marker.</summary>
    public enum MissionKind { Courier, Race, Evade }

    /// <summary>Result of a mission tick.</summary>
    public enum MissionStatus { Running, CheckpointReached, Succeeded, Failed }

    /// <summary>
    /// A running job: a list of checkpoints and a countdown. Evade jobs have no
    /// checkpoints — they arm once the wanted level reaches <see cref="RequiredStars"/>
    /// and succeed when the police are shaken (see <see cref="NotifyEvaded"/>).
    /// </summary>
    /// <example><code>
    /// var run = MissionRun.Courier(cfg, start, dropOff);
    /// var status = run.Tick(dt, playerPos, stars);
    /// </code></example>
    public sealed class MissionRun
    {
        /// <summary>Job kind.</summary>
        public MissionKind Kind { get; }
        /// <summary>Checkpoints in order.</summary>
        public IReadOnlyList<Vec2> Points => points;
        /// <summary>Index of the next checkpoint.</summary>
        public int Index { get; private set; }
        /// <summary>Seconds left.</summary>
        public float TimeLeft { get; private set; }
        /// <summary>Payout on success ($).</summary>
        public int Reward { get; }
        /// <summary>Stars needed before an evade job counts.</summary>
        public int RequiredStars { get; }
        /// <summary>True once an evade job has reached its required stars.</summary>
        public bool Armed { get; private set; }
        /// <summary>Checkpoint trigger radius (m).</summary>
        public float Radius { get; }

        private readonly List<Vec2> points;
        private bool evaded;

        private MissionRun(MissionKind kind, List<Vec2> pts, float time, int reward, float radius, int stars)
        {
            Kind = kind; points = pts; TimeLeft = time; Reward = reward; Radius = radius; RequiredStars = stars;
        }

        /// <summary>Delivery to one point; time and pay scale with distance.</summary>
        public static MissionRun Courier(HarborConfig cfg, Vec2 start, Vec2 dropOff)
        {
            float d = Vec2.Distance(start, dropOff);
            float pay = Mathx.Lerp(cfg.CourierRewardMin, cfg.CourierRewardMax, Mathx.Clamp((d - 160f) / 170f, 0, 1));
            return new MissionRun(MissionKind.Courier, new List<Vec2> { dropOff }, cfg.TimeBase + d * cfg.TimePerMeter, (int)Math.Round(pay / 10f) * 10, 3.2f, 0);
        }

        /// <summary>Checkpoint race; time from the total route length.</summary>
        public static MissionRun Race(HarborConfig cfg, Vec2 start, IList<Vec2> checkpoints)
        {
            float total = 0; Vec2 last = start;
            foreach (var p in checkpoints) { total += Vec2.Distance(last, p); last = p; }
            return new MissionRun(MissionKind.Race, new List<Vec2>(checkpoints), 12f + total * 0.062f, cfg.RaceReward, 7f, 0);
        }

        /// <summary>Get two stars, then lose the police.</summary>
        public static MissionRun Evade(HarborConfig cfg)
        {
            return new MissionRun(MissionKind.Evade, new List<Vec2>(), 120f, cfg.EvadeReward, 0f, 2);
        }

        /// <summary>Call when the wanted level is cleared by evading.</summary>
        public void NotifyEvaded() { if (Armed) evaded = true; }

        /// <summary>Advances the timer and checks the next checkpoint.</summary>
        public MissionStatus Tick(float dt, Vec2 player, int stars)
        {
            if (Kind == MissionKind.Evade)
            {
                if (stars >= RequiredStars) Armed = true;
                if (evaded) return MissionStatus.Succeeded;
            }
            TimeLeft -= dt;
            if (TimeLeft <= 0) return MissionStatus.Failed;
            if (Kind == MissionKind.Evade || Index >= points.Count) return MissionStatus.Running;
            if (Vec2.Distance(player, points[Index]) < Radius)
            {
                Index++;
                return Index >= points.Count ? MissionStatus.Succeeded : MissionStatus.CheckpointReached;
            }
            return MissionStatus.Running;
        }
    }
}
