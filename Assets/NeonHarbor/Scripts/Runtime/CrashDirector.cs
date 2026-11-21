using System;
using System.Collections.Generic;
using NeonHarbor.Core;
using UnityEngine;

namespace NeonHarbor.Runtime
{
    /// <summary>
    /// Vehicle accidents: every wall or car impact goes through <see cref="CrashModel"/> —
    /// health loss, dented bodywork (body mesh vertices pushed in around the contact),
    /// smashed lights, debris and smoke, a slower car, and on fast head-on hits the player
    /// goes through the windscreen. Drivers of wrecked traffic cars get out. Port of the
    /// browser build's src/12-crash.js.
    /// </summary>
    public sealed class CrashDirector
    {
        private readonly HarborConfig cfg;
        private readonly CityLayout city;
        private readonly HarborMaterials mats;
        private readonly WorldDirector world;
        private readonly CombatDirector combat;
        private readonly PlayerController player;
        private readonly HashSet<Vehicle> dented = new HashSet<Vehicle>();
        private readonly Dictionary<Vehicle, float> bailTimers = new Dictionary<Vehicle, float>();
        private readonly HashSet<Vehicle> bailed = new HashSet<Vehicle>();
        private readonly System.Random rnd = new System.Random(91);
        private float smokeTimer;

        /// <summary>Player-involved crash (strength 0..1).</summary>
        public event Action<float> PlayerCrash;
        /// <summary>The player crashed into a civilian car (crime only if police see it).</summary>
        public event Action<string> TrafficOffence;
        /// <summary>A crash made noise at a position (police may hear it).</summary>
        public event Action<Vector3> Noise;
        /// <summary>The player went through the windscreen (injury).</summary>
        public event Action<float> Ejected;

        /// <summary>Creates the director.</summary>
        public CrashDirector(HarborConfig cfg, CityLayout city, HarborMaterials mats, WorldDirector world, CombatDirector combat, PlayerController player)
        {
            this.cfg = cfg; this.city = city; this.mats = mats; this.world = world; this.combat = combat; this.player = player;
        }

        private float R(float a, float b) { return a + (b - a) * (float)rnd.NextDouble(); }

        /// <summary>
        /// One impact on one car. <paramref name="normalIntoCar"/> points out of the obstacle, into the car;
        /// <paramref name="other"/> is the other car or null for a wall.
        /// </summary>
        public void Impact(Vehicle car, float speed, Vector3 point, Vector3 normalIntoCar, Vehicle other)
        {
            if (car.Exploded || speed < CrashModel.MinImpact) return;
            car.Health -= CrashModel.Damage(speed);
            float depth = CrashModel.DentDepth(speed);
            if (depth > 0) Dent(car, point, normalIntoCar, depth);
            bool front = Vector3.Dot(point - car.Position, car.Forward) > 0;
            if (CrashModel.LightsBreak(speed)) BreakLights(car, front);
            int n = Mathf.Min(14, 3 + (int)(speed * 0.5f));
            for (int k = 0; k < n; k++) combat.Fx(CombatDirector.FxKind.Spark, point + Vector3.up * 0.8f, new Vector3(R(-3, 3), R(1, 4), R(-3, 3)), R(0.2f, 0.45f), R(0.06f, 0.14f));
            if (speed > 9) for (int k = 0; k < 4; k++) combat.Fx(CombatDirector.FxKind.Smoke, point + Vector3.up * 0.8f, new Vector3(R(-1, 1), R(0.5f, 1.5f), R(-1, 1)), R(0.8f, 1.5f), 0.8f, 2);
            if (car != player.Car) return;
            PlayerCrash?.Invoke(Mathf.Min(1, speed / 20));
            float frontal = CrashModel.Frontal(new Vec2(car.Forward.x, car.Forward.z), new Vec2(normalIntoCar.x, normalIntoCar.z));
            if (CrashModel.EjectsDriver(speed, frontal))
            {
                float injury = CrashModel.EjectionInjury(speed);
                player.Eject(speed); Ejected?.Invoke(injury);
                return;
            }
            if (speed > 8) Noise?.Invoke(car.Position);
            if (other != null && other.Spec.Type != CarType.Police && speed > 6) TrafficOffence?.Invoke("교통사고");
        }

        private void Dent(Vehicle car, Vector3 worldPoint, Vector3 normalIntoCar, float depth)
        {
            MeshFilter body = null;
            foreach (var mf in car.Parts.Body.GetComponentsInChildren<MeshFilter>()) if (mf.gameObject.name.StartsWith("CarBody")) { body = mf; break; }
            if (body == null) return;
            if (!dented.Contains(car)) { body.sharedMesh = UnityEngine.Object.Instantiate(body.sharedMesh); dented.Add(car); }
            var mesh = body.sharedMesh; var verts = mesh.vertices;
            Vector3 lp = body.transform.InverseTransformPoint(new Vector3(worldPoint.x, car.Position.y + 0.75f, worldPoint.z));
            Vector3 ln = body.transform.InverseTransformDirection(normalIntoCar).normalized;
            const float Radius = 0.95f;
            for (int k = 0; k < verts.Length; k++)
            {
                Vector3 d = verts[k] - lp; float dist = Mathf.Sqrt(d.x * d.x + d.y * d.y * 0.5f + d.z * d.z);
                if (dist >= Radius) continue;
                float f = depth * (1 - dist / Radius) * R(0.8f, 1.2f);
                verts[k] += ln * f + Vector3.down * f * 0.25f;
            }
            mesh.vertices = verts; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }

        private void BreakLights(Vehicle car, bool front)
        {
            var list = front ? car.Parts.Heads : car.Parts.Tails;
            var broken = mats.Plain(new Color(0.1f, 0.1f, 0.1f), 0.4f);
            foreach (var r in list) if (r != null) r.sharedMaterial = broken;
            if (front) car.HeadsBroken = true; else car.TailsBroken = true;
        }

        /// <summary>Engine smoke on damaged cars; drivers leave stopped wrecks.</summary>
        public void Tick(float dt)
        {
            smokeTimer += dt; bool puff = smokeTimer > 0.12f; if (puff) smokeTimer = 0;
            foreach (var v in world.Vehicles.ToArray())
            {
                if (v.Mode == VehicleMode.Wreck && v.Driver != null && !bailed.Contains(v))
                {
                    bailTimers.TryGetValue(v, out float t);
                    t = v.Speed > 0.5f ? 0 : t + dt; bailTimers[v] = t;
                    if (t > 1.5f) BailOut(v);
                }
                if (!puff || v.Exploded || v.Burning > 0) continue;
                int lvl = CrashModel.SmokeLevel(v.Health); if (lvl == 0) continue;
                Vector3 engine = v.Position + v.Forward * v.Spec.Length * 0.38f + Vector3.up * 1.1f;
                combat.Fx(CombatDirector.FxKind.Smoke, engine, new Vector3(R(-0.3f, 0.3f), R(1.2f, 2.2f), R(-0.3f, 0.3f)), lvl == 2 ? 2.2f : 1.4f, lvl == 2 ? 1.1f : 0.7f, 2.5f, lvl == 2 ? 0.06f : 0.35f);
            }
        }

        private void BailOut(Vehicle v)
        {
            bailed.Add(v); world.RemoveDriver(v);
            var side = new Vector3(-v.Forward.z, 0, v.Forward.x);
            Block blk = null;
            foreach (var probe in new[] { side * 8, -side * 8, v.Forward * 8, -v.Forward * 8 }) { blk = city.BlockAt(v.Position.x + probe.x, v.Position.z + probe.z); if (blk != null) break; }
            if (blk == null) return;
            var p = world.SpawnPed(blk); p.Pos = v.Position + side * 2; p.Pos.y = 0.18f;
            if (v.Health < 60) { p.Mode = Pedestrian.State.Flee; p.Timer = 6; p.Threat = v.Position; }
        }
    }
}
