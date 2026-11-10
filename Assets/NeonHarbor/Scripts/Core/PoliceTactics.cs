using System;
using System.Collections.Generic;

namespace NeonHarbor.Core
{
    /// <summary>What a police car is doing in a pursuit.</summary>
    public enum PoliceRole
    {
        /// <summary>Closest unit: chases and rams.</summary>
        Pursuer,
        /// <summary>Takes a parallel street to the intersection ahead of the target.</summary>
        Flanker,
        /// <summary>Races further ahead to cut the target off.</summary>
        Interceptor,
    }

    /// <summary>
    /// Engine-free maths behind the police AI: interception, cornering speed, pure-pursuit
    /// steering targets, role assignment, field-of-view checks, cut-off nodes and search
    /// sectors. Both builds use the same rules (browser twin: src/11-police.js).
    /// </summary>
    /// <example><code>
    /// var aim = PoliceTactics.Intercept(cop, copSpeed, target, targetVel, 2.5f);
    /// float vMax = PoliceTactics.SpeedForCorner(angle, distanceToCorner, top);
    /// </code></example>
    public static class PoliceTactics
    {
        /// <summary>Lateral grip budget used for cornering (m/s²).</summary>
        public const float LateralAccel = 9f;
        /// <summary>Braking used for speed planning (m/s²).</summary>
        public const float PlanBrake = 14f;

        /// <summary>
        /// Where to aim to meet a moving target: the earliest point on the target's straight-line
        /// path that the chaser can reach at <paramref name="chaserSpeed"/>, capped at <paramref name="maxLead"/> s ahead.
        /// </summary>
        public static Vec2 Intercept(Vec2 chaser, float chaserSpeed, Vec2 target, Vec2 targetVel, float maxLead)
        {
            Vec2 d = target - chaser;
            float s = Math.Max(chaserSpeed, 8f);
            float a = targetVel.Dot(targetVel) - s * s, b = 2 * d.Dot(targetVel), c = d.Dot(d);
            float t;
            if (Math.Abs(a) < 1e-4f) t = b < 0 ? -c / b : maxLead;
            else
            {
                float disc = b * b - 4 * a * c;
                if (disc < 0) t = maxLead;
                else
                {
                    float sq = (float)Math.Sqrt(disc), t1 = (-b - sq) / (2 * a), t2 = (-b + sq) / (2 * a);
                    t = Math.Min(t1, t2) > 0 ? Math.Min(t1, t2) : Math.Max(t1, t2);
                    if (t <= 0) t = maxLead;
                }
            }
            t = Mathx.Clamp(t, 0, maxLead);
            return target + targetVel * t;
        }

        /// <summary>Turn angle (rad, 0..π) at <paramref name="b"/> on the path a → b → c.</summary>
        public static float TurnAngle(Vec2 a, Vec2 b, Vec2 c)
        {
            Vec2 u = (b - a).Normalized, v = (c - b).Normalized;
            return (float)Math.Acos(Mathx.Clamp(u.Dot(v), -1, 1));
        }

        /// <summary>
        /// Highest speed that still lets the car slow down for a corner of <paramref name="turnAngle"/>
        /// that is <paramref name="distance"/> metres ahead.
        /// </summary>
        public static float SpeedForCorner(float turnAngle, float distance, float topSpeed)
        {
            float k = Mathx.Clamp(1 - turnAngle / (float)(Math.PI / 2), 0, 1);
            float corner = 8f + (topSpeed - 8f) * k * k; // 90° turn ≈ 8 m/s, straight = top speed
            float v = (float)Math.Sqrt(corner * corner + 2 * PlanBrake * Math.Max(0, distance));
            return Math.Min(topSpeed, v);
        }

        /// <summary>
        /// Pure-pursuit target: the point <paramref name="lookahead"/> metres along the polyline,
        /// measured from the closest point to <paramref name="pos"/>. Returns the end when shorter.
        /// </summary>
        public static Vec2 LookAhead(IList<Vec2> path, Vec2 pos, float lookahead)
        {
            if (path.Count == 1) return path[0];
            int best = 0; float bestD = float.MaxValue, bestT = 0;
            for (int k = 0; k + 1 < path.Count; k++)
            {
                Vec2 a = path[k], ab = path[k + 1] - a; float len2 = Math.Max(1e-6f, ab.Dot(ab));
                float t = Mathx.Clamp((pos - a).Dot(ab) / len2, 0, 1);
                float d = Vec2.Distance(a + ab * t, pos);
                if (d < bestD) { bestD = d; best = k; bestT = t; }
            }
            float remaining = lookahead;
            Vec2 cur = path[best] + (path[best + 1] - path[best]) * bestT;
            for (int k = best; k + 1 < path.Count; k++)
            {
                Vec2 next = path[k + 1]; float seg = Vec2.Distance(cur, next);
                if (seg >= remaining) return cur + (next - cur).Normalized * remaining;
                remaining -= seg; cur = next;
            }
            return path[path.Count - 1];
        }

        /// <summary>
        /// Assigns roles by distance to the target: the closest unit pursues, the next flanks,
        /// the third intercepts, any others pursue. Returns roles in the input order.
        /// </summary>
        public static PoliceRole[] AssignRoles(IList<float> distances)
        {
            var order = new List<int>(); for (int k = 0; k < distances.Count; k++) order.Add(k);
            order.Sort((a, b) => distances[a].CompareTo(distances[b]));
            var roles = new PoliceRole[distances.Count];
            for (int rank = 0; rank < order.Count; rank++)
                roles[order[rank]] = rank == 1 ? PoliceRole.Flanker : rank == 2 ? PoliceRole.Interceptor : PoliceRole.Pursuer;
            return roles;
        }

        /// <summary>
        /// Intersection <paramref name="blocksAhead"/> blocks ahead of a target moving along
        /// <paramref name="heading"/> (snapped to the dominant axis), clamped to the grid.
        /// </summary>
        public static (int, int) NodeAhead(CityLayout city, Vec2 target, Vec2 heading, int blocksAhead)
        {
            var (i, j) = city.NearestNode(target.X, target.Y);
            int di = 0, dj = 0;
            if (Math.Abs(heading.X) >= Math.Abs(heading.Y)) di = heading.X >= 0 ? 1 : -1; else dj = heading.Y >= 0 ? 1 : -1;
            return ((int)Mathx.Clamp(i + di * blocksAhead, 0, city.N), (int)Mathx.Clamp(j + dj * blocksAhead, 0, city.N));
        }

        /// <summary>
        /// Field-of-view test (line of sight is the caller's job): inside <paramref name="range"/>
        /// and within half of <paramref name="fovRad"/> of the facing, or anywhere inside <paramref name="nearRadius"/>.
        /// </summary>
        public static bool InView(Vec2 eye, float yaw, Vec2 target, float fovRad, float range, float nearRadius)
        {
            Vec2 d = target - eye; float dist = d.Length;
            if (dist < nearRadius) return true;
            if (dist > range) return false;
            return Math.Abs(Mathx.AngleDiff(yaw, d.ToYaw())) <= fovRad / 2;
        }

        /// <summary>
        /// Search waypoint number <paramref name="leg"/> for unit <paramref name="unit"/> of <paramref name="units"/>.
        /// Leg 0 is the last known position itself (each unit a few metres apart); later legs
        /// spiral outward through the unit's own sector of the circle and stay inside it.
        /// Advance the leg when the unit arrives (or gives up on a point).
        /// </summary>
        public static Vec2 SearchWaypoint(Vec2 lastKnown, float radius, int unit, int units, int leg)
        {
            units = Math.Max(1, units);
            float sector = Mathx.Tau / units, baseAngle = unit * sector;
            if (leg <= 0)
            {
                float a0 = baseAngle + sector * 0.5f;
                return new Vec2(lastKnown.X + (float)Math.Cos(a0) * 6, lastKnown.Y + (float)Math.Sin(a0) * 6);
            }
            float frac = Math.Min(0.9f, 0.25f + 0.2f * (leg - 1));
            float angle = baseAngle + sector * (0.2f + 0.6f * (((leg - 1) * 0.618f) % 1f));
            return new Vec2(lastKnown.X + (float)Math.Cos(angle) * radius * frac, lastKnown.Y + (float)Math.Sin(angle) * radius * frac);
        }

        /// <summary>Officer accuracy: improves with stars and against a still target, worse with distance.</summary>
        public static float OfficerAccuracy(float distance, float targetSpeed, int stars)
        {
            return Mathx.Clamp(0.5f + 0.035f * stars - distance * 0.011f - targetSpeed * 0.013f, 0.08f, 0.7f);
        }
    }
}
