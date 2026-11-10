using System;
using System.Collections.Generic;
using NeonHarbor.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonHarbor.Runtime
{
    /// <summary>
    /// Everything violent: the player's weapons (fists, pistol, SMG) with aiming and
    /// hitscan, police officers who leave their cars and shoot back, vehicles that
    /// catch fire and explode, weapon / ammo / health / armour pickups, and the
    /// effects (tracers, muzzle flashes, sparks, hit puffs, fire, smoke, blasts).
    /// Port of the browser build's 09-combat.js.
    /// </summary>
    public sealed class CombatDirector
    {
        /// <summary>A police officer on foot.</summary>
        public sealed class Officer
        {
            public HumanRig Rig; public Vector3 Pos, Vel; public float Yaw, Cooldown, DownTimer, StrafeTimer, RouteTimer; public Vitals Vitals; public bool Down;
            public Vehicle Car; public int Side = 1, Strafe = 1, Burst; public List<Vector3> Route;
        }
        /// <summary>Set by the game: search point for an officer while police have lost sight.</summary>
        public Func<Officer, Vector3> SearchTarget;
        /// <summary>Officer field of view (radians, metres).</summary>
        public const float OfficerFov = 2.8f;

        private sealed class Pickup { public string Kind; public Transform Mesh; public float Respawn, Wait; public bool Permanent; }
        private sealed class Fx { public Transform T; public Material M; public Vector3 Pos, Vel; public float Life, Age, Size, Grow; public int Kind; public bool Alive; }

        private const int FxFire = 0, FxSmoke = 1, FxSpark = 2, FxFlash = 3, FxHit = 4;
        private readonly HarborConfig cfg;
        private readonly CityLayout city;
        private readonly HarborMaterials mats;
        private readonly WorldDirector world;
        private readonly PlayerController player;
        private readonly CameraRig cameraRig;
        private readonly Camera cam;
        private readonly Transform root;
        private readonly System.Random rnd = new System.Random(31);
        private readonly List<Pickup> pickups = new List<Pickup>();
        private readonly List<Fx> fx = new List<Fx>();
        private readonly List<(LineRenderer line, float age)> tracers = new List<(LineRenderer, float)>();
        private readonly Material fireMat, smokeMat, burnt;
        private readonly Mesh quad;
        private readonly Light flash, boom;
        private Transform gun, gunSmg;
        private float gunfireHeatCooldown, punchTimer, aimHold;

        /// <summary>Officers on foot.</summary>
        public readonly List<Officer> Officers = new List<Officer>();
        /// <summary>Seconds left on the hit-marker flash.</summary>
        public float HitMarker { get; private set; }
        /// <summary>True when the aim button is held.</summary>
        public bool Aiming { get; private set; }

        /// <summary>Wanted heat to add (amount, reason).</summary>
        public event Action<float, string, bool> Heat;
        /// <summary>Set by the game: where officers should run (player when seen, search area otherwise).</summary>
        public Func<object, Vector3> OfficerTarget;
        /// <summary>Message for the HUD.</summary>
        public event Action<string> Toast;
        /// <summary>The player took damage (0..1 strength).</summary>
        public event Action<float> PlayerHurt;
        /// <summary>Sound cues.</summary>
        public event Action<bool> Shot; public event Action Explosion; public event Action<bool> Click; public event Action Swing; public event Action PickedUp;

        /// <summary>Creates the director and places pickups.</summary>
        public CombatDirector(HarborConfig cfg, CityLayout city, HarborMaterials mats, WorldDirector world, PlayerController player, CameraRig cameraRig, Camera cam, Transform root, Vector3 spawn)
        {
            this.cfg = cfg; this.city = city; this.mats = mats; this.world = world; this.player = player; this.cameraRig = cameraRig; this.cam = cam; this.root = root;
            var glowTex = ProceduralTextures.RadialGlow();
            fireMat = new Material(mats.Pool) { name = "FxAdditive", mainTexture = glowTex };
            smokeMat = new Material(mats.Pool) { name = "FxSmoke", mainTexture = glowTex };
            smokeMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); smokeMat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            burnt = mats.Plain(new Color(0.09f, 0.08f, 0.07f), 0.05f);
            var qb = new MeshBuilder(); qb.Quad(new Vector3(-0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(0.5f, -0.5f, 0), Vector3.back, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            quad = qb.ToMesh("fxquad");
            flash = new GameObject("MuzzleLight").AddComponent<Light>(); flash.type = LightType.Point; flash.range = 14; flash.color = new Color(1f, 0.75f, 0.45f); flash.intensity = 0; flash.shadows = LightShadows.None;
            boom = new GameObject("BlastLight").AddComponent<Light>(); boom.type = LightType.Point; boom.range = 60; boom.color = new Color(1f, 0.55f, 0.24f); boom.intensity = 0; boom.shadows = LightShadows.None;
            player.Arsenal.Click += done => Click?.Invoke(done);
            PlacePickups(spawn);
        }

        private float R(float a, float b) { return a + (b - a) * (float)rnd.NextDouble(); }

        // ------------------------------------------------------------------ effects
        private void Emit(int kind, Vector3 pos, Vector3 vel, float life, float size, float grow = 1)
        {
            Fx f = null;
            foreach (var e in fx) if (!e.Alive && e.Kind == kind) { f = e; break; }
            if (f == null)
            {
                if (fx.Count > 400) return;
                var go = new GameObject("fx"); go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var r = go.AddComponent<MeshRenderer>(); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                r.material = kind == FxSmoke || kind == FxHit ? smokeMat : fireMat; // instance per sprite so each can fade
                f = new Fx { T = go.transform, M = r.material, Kind = kind }; fx.Add(f);
            }
            f.Pos = pos; f.Vel = vel; f.Life = life; f.Age = 0; f.Size = size; f.Grow = grow; f.Alive = true; f.T.gameObject.SetActive(true);
        }

        private static readonly Color[] FxColor = { new Color(1f, 0.45f, 0.12f), new Color(0.2f, 0.2f, 0.22f), new Color(1f, 0.75f, 0.3f), new Color(1f, 0.8f, 0.45f), new Color(0.35f, 0.02f, 0.02f) };

        private void UpdateFx(float dt)
        {
            var camRot = cam.transform.rotation;
            foreach (var f in fx)
            {
                if (!f.Alive) continue;
                f.Age += dt; if (f.Age >= f.Life) { f.Alive = false; f.T.gameObject.SetActive(false); continue; }
                float k = f.Age / f.Life;
                f.Pos += f.Vel * dt;
                if (f.Kind == FxSpark || f.Kind == FxHit) f.Vel.y -= 9 * dt; else if (f.Kind == FxSmoke) f.Vel *= 1 - dt * 0.6f;
                f.T.SetPositionAndRotation(f.Pos, camRot);
                f.T.localScale = Vector3.one * f.Size * (1 + (f.Grow - 1) * k);
                var c = FxColor[f.Kind];
                float strength = f.Kind == FxSmoke ? 0.55f * (1 - k) : f.Kind == FxHit ? 0.9f * (1 - k) : (1 - k) * (f.Kind == FxFlash ? 1.6f : 1.2f);
                f.M.SetColor("_TintColor", new Color(c.r, c.g, c.b, Mathf.Clamp01(strength)));
            }
            for (int i = tracers.Count - 1; i >= 0; i--)
            {
                var (line, age) = tracers[i]; age += dt;
                if (age > 0.07f) { line.enabled = false; tracers[i] = (line, 99); continue; }
                tracers[i] = (line, age);
                var c = new Color(1f, 0.85f, 0.5f, 1 - age / 0.07f); line.startColor = c; line.endColor = c;
            }
            flash.intensity = Mathf.Max(0, flash.intensity - dt * 60);
            boom.intensity = Mathf.Max(0, boom.intensity - dt * 14);
        }

        /// <summary>Draws a bullet tracer.</summary>
        public void Tracer(Vector3 a, Vector3 b)
        {
            LineRenderer lr = null; int idx = -1;
            for (int i = 0; i < tracers.Count; i++) if (tracers[i].age > 1) { lr = tracers[i].line; idx = i; break; }
            if (lr == null)
            {
                lr = new GameObject("Tracer").AddComponent<LineRenderer>(); lr.transform.SetParent(root, false);
                lr.material = fireMat; lr.widthMultiplier = 0.03f; lr.positionCount = 2; lr.shadowCastingMode = ShadowCastingMode.Off;
                tracers.Add((lr, 0)); idx = tracers.Count - 1;
            }
            lr.SetPosition(0, a); lr.SetPosition(1, b); lr.enabled = true; tracers[idx] = (lr, 0);
        }

        // ------------------------------------------------------------------ hit tests
        private enum HitKind { None, Wall, Ground, Ped, Officer, Car }
        private struct Hit { public HitKind Kind; public float T; public Pedestrian Ped; public Officer Officer; public Vehicle Car; }

        private Hit Cast(Vector3 o, Vector3 d, float range, float tMin, Vehicle ignore)
        {
            var best = new Hit { Kind = HitKind.None, T = range };
            float wall = city.Raycast(o.x, o.y, o.z, o.x + d.x * range, o.y + d.y * range, o.z + d.z * range) * range;
            if (wall < best.T) best = new Hit { Kind = HitKind.Wall, T = wall };
            if (d.y < -1e-4f) { float tg = -o.y / d.y; if (tg > tMin && tg < best.T) best = new Hit { Kind = HitKind.Ground, T = tg }; }
            foreach (var p in world.Peds)
            {
                if (p.Mode != Pedestrian.State.Walk && p.Mode != Pedestrian.State.Flee) continue;
                float t = HitTest.RayCylinder(o.x, o.y, o.z, d.x, d.y, d.z, p.Pos.x, p.Pos.z, p.Pos.y, p.Pos.y + 1.8f, 0.38f);
                if (t > tMin && t < best.T) best = new Hit { Kind = HitKind.Ped, T = t, Ped = p };
            }
            foreach (var of in Officers)
            {
                if (of.Down) continue;
                float t = HitTest.RayCylinder(o.x, o.y, o.z, d.x, d.y, d.z, of.Pos.x, of.Pos.z, of.Pos.y, of.Pos.y + 1.8f, 0.38f);
                if (t > tMin && t < best.T) best = new Hit { Kind = HitKind.Officer, T = t, Officer = of };
            }
            foreach (var v in world.Vehicles)
            {
                if (v == ignore || v.Exploded) continue;
                if (Mathf.Abs(v.Position.x - o.x) > range + 5 || Mathf.Abs(v.Position.z - o.z) > range + 5) continue;
                float t = HitTest.RayYawBox(o.x, o.y, o.z, d.x, d.y, d.z, v.Position.x, v.Position.z, v.Model.Yaw, v.Spec.Width / 2, v.Spec.Length / 2, v.Height + 0.25f, v.Height + 1.5f);
                if (t > tMin && t < best.T) best = new Hit { Kind = HitKind.Car, T = t, Car = v };
            }
            return best;
        }

        // ------------------------------------------------------------------ damage
        private void ScarePeds(Vector3 at, float radius, float seconds = 6)
        {
            foreach (var p in world.Peds)
                if ((p.Mode == Pedestrian.State.Walk || p.Mode == Pedestrian.State.Flee) && Vector3.Distance(p.Pos, at) < radius) { p.Mode = Pedestrian.State.Flee; p.Timer = seconds; p.Threat = at; }
        }

        private bool CopNearby(float r)
        {
            foreach (var v in world.Vehicles) if (v.Spec.Type == CarType.Police && !v.Exploded && Vector3.Distance(v.Position, player.Position) < r) return true;
            foreach (var o in Officers) if (!o.Down && Vector3.Distance(o.Pos, player.Position) < r) return true;
            return false;
        }

        private void DamagePed(Pedestrian p, float amount, Vector3 dir, bool byPlayer)
        {
            p.Health -= amount;
            for (int k = 0; k < 3; k++) Emit(FxHit, p.Pos + Vector3.up * 1.2f, new Vector3(dir.x * 2 + R(-1, 1), R(0.5f, 2), dir.z * 2 + R(-1, 1)), 0.35f, 0.18f);
            if (p.Health <= 0) { world.Knock(p, dir.x * 3, dir.z * 3, false); if (byPlayer) Heat?.Invoke(1, "보행자 공격", false); }
            else { p.Mode = Pedestrian.State.Flee; p.Timer = 7; p.Threat = player.Position; }
        }

        /// <summary>Damages a car; at zero health it starts burning and explodes 4.5 s later.</summary>
        public void DamageCar(Vehicle v, float amount, bool byPlayer)
        {
            v.Health -= amount;
            if (v.Mode == VehicleMode.Traffic && amount > 0) v.Mode = VehicleMode.Wreck;
            if (v.Health <= 0 && v.Burning <= 0 && !v.Exploded) { v.Burning = 4.5f; v.BurnByPlayer = byPlayer; }
        }

        private void Explode(Vehicle v)
        {
            v.Exploded = true; v.Burning = 0; v.Health = 0;
            Vector3 c = v.Position + Vector3.up;
            Explosion?.Invoke(); cameraRig.Shake = Mathf.Max(cameraRig.Shake, Mathf.Clamp(1.2f - Vector3.Distance(c, player.Position) / 40, 0.2f, 1));
            boom.transform.position = c; boom.intensity = 8;
            for (int k = 0; k < 26; k++) Emit(FxFire, c, new Vector3(R(-6, 6), R(1, 9), R(-6, 6)), R(0.5f, 1.1f), R(1.2f, 2.6f), 2.2f);
            for (int k = 0; k < 20; k++) Emit(FxSmoke, c, new Vector3(R(-2, 2), R(2, 6), R(-2, 2)), R(2.5f, 4.5f), R(2, 3.5f), 3);
            Emit(FxFlash, c, Vector3.zero, 0.25f, 9, 2);
            foreach (var r in v.Parts.Body.GetComponentsInChildren<MeshRenderer>()) if (r.sharedMaterial != mats.Glass && r.sharedMaterial != mats.Tire) r.sharedMaterial = burnt;
            v.Parts.Body.localPosition = new Vector3(0, 0.25f, 0);
            v.Model.Velocity = new Vec2(v.Model.Velocity.X + R(-2, 2), v.Model.Velocity.Y + R(-2, 2)); v.Model.YawRate += R(-2, 2);
            if (v.Mode != VehicleMode.Player) { v.Mode = VehicleMode.Parked; world.RemoveDriver(v); }
            const float Radius = 8f;
            foreach (var p in world.Peds)
            {
                float d = Vector3.Distance(p.Pos, c); if (d >= Radius || d < 1e-3f) continue;
                world.Knock(p, (p.Pos.x - c.x) / d * 9, (p.Pos.z - c.z) / d * 9, false);
            }
            foreach (var o in Officers.ToArray()) { float d = Vector3.Distance(o.Pos, c); if (d < Radius) HurtOfficer(o, 120 * HitTest.Falloff(d, Radius), false); }
            foreach (var w in world.Vehicles)
            {
                if (w == v || w.Exploded) continue;
                float d = Vector3.Distance(w.Position, v.Position); if (d >= Radius || d < 1e-3f) continue;
                DamageCar(w, 70 * HitTest.Falloff(d, Radius), v.BurnByPlayer);
                w.Model.Velocity = new Vec2(w.Model.Velocity.X + (w.Position.x - v.Position.x) / d * 6, w.Model.Velocity.Y + (w.Position.z - v.Position.z) / d * 6);
            }
            float pd = Vector3.Distance(c, player.Position);
            if (pd < Radius)
            {
                if (player.Car == v) player.Health = 0;
                else if (player.Car != null) DamageCar(player.Car, 60 * HitTest.Falloff(pd, Radius), true);
                else { player.Damage(110 * HitTest.Falloff(pd, Radius)); player.Knock(new Vector3((player.Position.x - c.x) / pd * 8, 5, (player.Position.z - c.z) / pd * 8)); }
                PlayerHurt?.Invoke(1);
            }
            ScarePeds(c, 60, 8);
            if (v.BurnByPlayer) Heat?.Invoke(1, "차량 폭발", false);
        }

        // ------------------------------------------------------------------ officers
        /// <summary>Two officers get out of a police car (it becomes cover).</summary>
        public void SpawnOfficersFrom(Vehicle car)
        {
            car.Unloaded = true;
            var f = car.Model.Forward; var right = new Vector3(f.Y, 0, -f.X);
            foreach (int side in new[] { 1, -1 })
            {
                var look = HumanRig.RandomLook(rnd); look.Top = HarborMaterials.Hex(0x1d2a44); look.Bottom = HarborMaterials.Hex(0x1d2a44); look.Hair = HarborMaterials.Hex(0x111111);
                var rig = HumanRig.Create(mats, root, look);
                AttachMesh(rig.Head, MeshBuilder.Cylinder(14), mats.Plain(HarborMaterials.Hex(0x141a26), 0.3f), new Vector3(0, 0.11f, 0), new Vector3(0.3f, 0.08f, 0.3f));
                AttachMesh(rig.ForeR, MeshBuilder.Cube(), mats.Plain(HarborMaterials.Hex(0x1b1d20), 0.7f), new Vector3(0, -0.46f, 0.06f), new Vector3(0.05f, 0.2f, 0.08f));
                Officers.Add(new Officer { Rig = rig, Pos = car.Position + right * side * 1.9f, Yaw = car.Model.Yaw, Vitals = new Vitals { Health = 60 }, Cooldown = R(0.4f, 1.2f), Car = car, Side = side, Strafe = side, StrafeTimer = R(1, 3) });
            }
            world.RemoveDriver(car);
            car.Mode = VehicleMode.Parked; car.Input = new VehicleInput { Brake = 1 };
        }

        private static void AttachMesh(Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale)
        {
            var go = new GameObject(mesh.name); go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private void HurtOfficer(Officer o, float amount, bool byPlayer)
        {
            if (o.Down) return;
            for (int k = 0; k < 3; k++) Emit(FxHit, o.Pos + Vector3.up * 1.2f, new Vector3(R(-1, 1), R(0.5f, 2), R(-1, 1)), 0.35f, 0.18f);
            if (o.Vitals.Damage(amount))
            {
                o.Down = true; o.DownTimer = 14; o.Pos.y = city.GroundHeight(o.Pos.x, o.Pos.z) + 0.12f;
                o.Rig.Root.SetPositionAndRotation(o.Pos, Quaternion.Euler(-90, o.Yaw * Mathf.Rad2Deg, 0));
                if (byPlayer) Heat?.Invoke(1, "경찰관 공격", true);
                Drop("ammo", o.Pos, 0);
            }
            else if (byPlayer) Heat?.Invoke(0.6f, "경찰관 공격", true);
        }

        private bool FriendlyInLine(Officer self, Vector3 from, Vector3 to)
        {
            Vector3 d = to - from; d.y = 0; float len2 = Mathf.Max(1e-4f, d.sqrMagnitude);
            bool Blocks(Vector3 p)
            {
                float t = ((p.x - from.x) * d.x + (p.z - from.z) * d.z) / len2; if (t <= 0.05f || t >= 0.95f) return false;
                return Vector2.Distance(new Vector2(from.x + d.x * t, from.z + d.z * t), new Vector2(p.x, p.z)) < 0.9f;
            }
            foreach (var q in Officers) if (q != self && !q.Down && Blocks(q.Pos)) return true;
            foreach (var v in world.Vehicles) if (v.Spec.Type == CarType.Police && v != player.Car && Blocks(v.Position)) return true;
            return false;
        }

        // walk straight when the way is clear, otherwise follow the street grid
        private void MoveOfficer(Officer o, Vector3 goal, float speed, float dt)
        {
            Vector3 t = goal;
            if (city.Raycast(o.Pos.x, 1, o.Pos.z, goal.x, 1, goal.z) < 0.999f)
            {
                o.RouteTimer -= dt;
                if (o.Route == null || o.RouteTimer <= 0)
                {
                    o.RouteTimer = 1; o.Route = new List<Vector3>();
                    foreach (var n in city.Path(city.NearestNode(o.Pos.x, o.Pos.z), city.NearestNode(goal.x, goal.z))) { var p = city.NodePos(n.Item1, n.Item2); o.Route.Add(new Vector3(p.X, 0, p.Y)); }
                }
                while (o.Route.Count > 0 && Vector2.Distance(new Vector2(o.Route[0].x, o.Route[0].z), new Vector2(o.Pos.x, o.Pos.z)) < 3) o.Route.RemoveAt(0);
                if (o.Route.Count > 0) t = o.Route[0];
            }
            else o.Route = null;
            Vector3 to = t - o.Pos; to.y = 0; float d = to.magnitude, s = d < 0.6f ? 0 : speed;
            if (d > 1e-3f) to /= d;
            o.Vel.x = Mathx.Damp(o.Vel.x, to.x * s, 10, dt); o.Vel.z = Mathx.Damp(o.Vel.z, to.z * s, 10, dt);
        }

        private void UpdateOfficers(float dt, int stars, bool playing, bool searching)
        {
            Vector3 real = player.Car != null ? player.Car.Position : player.Position;
            float targetSpeed = player.Car != null ? player.Car.Speed : new Vector2(player.Velocity.x, player.Velocity.z).magnitude;
            for (int k = Officers.Count - 1; k >= 0; k--)
            {
                var o = Officers[k];
                float dist = Vector3.Distance(o.Pos, player.Position);
                if (o.Down)
                {
                    o.DownTimer -= dt;
                    if (o.DownTimer < 0 || dist > 150) { UnityEngine.Object.Destroy(o.Rig.Root.gameObject); Officers.RemoveAt(k); }
                    continue;
                }
                if (stars == 0 && dist > 60) { UnityEngine.Object.Destroy(o.Rig.Root.gameObject); Officers.RemoveAt(k); continue; }
                float d = Vector2.Distance(new Vector2(real.x, real.z), new Vector2(o.Pos.x, o.Pos.z));
                Vector3 eye = o.Pos + Vector3.up * 1.5f, chest = new Vector3(real.x, player.Car != null ? 0.9f : player.Position.y + 1.2f, real.z);
                bool los = d < 70 && city.Raycast(eye.x, eye.y, eye.z, chest.x, chest.y, chest.z) >= 0.999f;
                bool combatMode = stars >= 2 && los && d < 32 && playing;
                float faceYaw = Mathf.Atan2(real.x - o.Pos.x, real.z - o.Pos.z);
                if (stars == 0) o.Vel *= 0.8f;
                else if (combatMode)
                {
                    Vector3 to = (real - o.Pos); to.y = 0; to = d > 1e-3f ? to / d : Vector3.forward; Vector3 side = new Vector3(to.z, 0, -to.x);
                    Vector3 goal;
                    if (o.Car != null && !o.Car.Exploded && Vector3.Distance(o.Car.Position, o.Pos) < 12 && player.Car == null)
                    {
                        // cover: put the cruiser between us and the target
                        Vector3 away = o.Car.Position - real; away.y = 0; away.Normalize(); Vector3 aside = new Vector3(away.z, 0, -away.x);
                        goal = o.Car.Position + away * 2.3f + aside * o.Side * 1.2f;
                    }
                    else
                    {
                        o.StrafeTimer -= dt; if (o.StrafeTimer <= 0) { o.Strafe = -o.Strafe; o.StrafeTimer = R(1.2f, 2.8f); }
                        float radial = d < 9 ? -1 : d > 18 ? 1 : 0;
                        goal = o.Pos + to * radial * 3 + side * o.Strafe * 2.5f;
                    }
                    MoveOfficer(o, goal, cfg.WalkSpeed * 1.4f, dt);
                    o.Cooldown -= dt;
                    if (o.Cooldown <= 0)
                    {
                        Vector3 muzzle = o.Pos + new Vector3(Mathf.Sin(faceYaw) * 0.6f, 1.45f, Mathf.Cos(faceYaw) * 0.6f);
                        if (FriendlyInLine(o, muzzle, chest)) o.Cooldown = 0.4f;
                        else
                        {
                            if (o.Burst <= 0) o.Burst = rnd.Next(2, 5);
                            o.Burst--; o.Cooldown = o.Burst > 0 ? 0.16f : R(0.8f, 1.5f);
                            bool hit = rnd.NextDouble() < PoliceTactics.OfficerAccuracy(d, targetSpeed, stars);
                            Vector3 end = hit ? chest : chest + new Vector3(R(-2, 2), R(-0.5f, 1.5f), R(-2, 2));
                            Tracer(muzzle, end); Emit(FxFlash, muzzle, Vector3.zero, 0.06f, 0.5f);
                            flash.transform.position = muzzle; flash.intensity = 3; Shot?.Invoke(false);
                            if (hit) { if (player.Car != null) DamageCar(player.Car, 6, false); else { player.Damage(7); PlayerHurt?.Invoke(0.5f); } }
                            else Emit(FxSpark, end, new Vector3(R(-1, 1), 2, R(-1, 1)), 0.25f, 0.12f);
                        }
                    }
                }
                else
                {
                    // close in when seen (arrest range at 1 star), otherwise sweep the search area
                    Vector3 goal = searching && SearchTarget != null ? SearchTarget(o) : real;
                    float stopAt = searching ? 2 : stars >= 2 ? 12 : 1.4f;
                    float gd = Vector2.Distance(new Vector2(goal.x, goal.z), new Vector2(o.Pos.x, o.Pos.z));
                    MoveOfficer(o, goal, gd > stopAt ? cfg.RunSpeed * 0.9f : 0, dt);
                    if (searching && new Vector2(o.Vel.x, o.Vel.z).sqrMagnitude > 0.01f) faceYaw = Mathf.Atan2(o.Vel.x, o.Vel.z);
                }
                o.Yaw += Mathx.AngleDiff(o.Yaw, faceYaw) * (1 - Mathf.Exp(-10 * dt));
                o.Pos += new Vector3(o.Vel.x, 0, o.Vel.z) * dt;
                float x = o.Pos.x, z = o.Pos.z; city.CollideCircle(ref x, ref z, 0.4f, out _, out _); o.Pos.x = x; o.Pos.z = z;
                foreach (var v in world.Vehicles) // don't walk through cars
                {
                    var f = v.Forward; Vector3 dv = o.Pos - v.Position; float al = Vector3.Dot(dv, f), lat = dv.x * f.z - dv.z * f.x, hl = v.Spec.Length / 2 + 0.35f, hw = v.Spec.Width / 2 + 0.35f;
                    if (Mathf.Abs(al) < hl && Mathf.Abs(lat) < hw)
                    {
                        float pa = hl - Mathf.Abs(al), pl = hw - Mathf.Abs(lat);
                        if (pa < pl) o.Pos += f * pa * Mathf.Sign(al); else o.Pos += new Vector3(f.z, 0, -f.x) * pl * Mathf.Sign(lat);
                    }
                }
                o.Pos.y = city.GroundHeight(o.Pos.x, o.Pos.z);
                o.Rig.Root.SetPositionAndRotation(o.Pos, Quaternion.Euler(0, o.Yaw * Mathf.Rad2Deg, 0));
                o.Rig.Animate(new Vector2(o.Vel.x, o.Vel.z).magnitude, dt, false);
                if (combatMode) { o.Rig.ArmR.localRotation = Quaternion.Euler(-84, 0, -6); o.Rig.ForeR.localRotation = Quaternion.identity; o.Rig.ArmL.localRotation = Quaternion.Euler(-80, 0, 26); }
            }
        }

        /// <summary>Distance to the nearest standing officer.</summary>
        public float NearestOfficer()
        {
            float n = 1e9f; foreach (var o in Officers) if (!o.Down) n = Mathf.Min(n, Vector3.Distance(o.Pos, player.Position)); return n;
        }

        // ------------------------------------------------------------------ pickups
        private static readonly Dictionary<string, Color> PickupColor = new Dictionary<string, Color>
        {
            ["pistol"] = new Color(1f, 0.8f, 0.13f), ["smg"] = new Color(1f, 0.43f, 0.07f), ["ammo"] = new Color(0.9f, 0.75f, 0.2f), ["health"] = new Color(0.12f, 1f, 0.2f), ["armor"] = new Color(0.1f, 0.35f, 1f),
        };

        private Transform PickupMesh(string kind)
        {
            var t = new GameObject("Pickup_" + kind).transform; t.SetParent(root, false);
            var mat = mats.Glow(PickupColor[kind], 3f); var mb = new MeshBuilder(); var cube = MeshBuilder.Cube(); var cyl = MeshBuilder.Cylinder(10);
            switch (kind)
            {
                case "health": mb.Add(cube, Matrix4x4.Scale(new Vector3(0.5f, 0.16f, 0.16f))); mb.Add(cube, Matrix4x4.Scale(new Vector3(0.16f, 0.5f, 0.16f))); break;
                case "armor": mb.Add(MeshBuilder.Cylinder(6, 0.75f), Matrix4x4.Scale(new Vector3(0.6f, 0.42f, 0.6f))); break;
                case "ammo": for (int k = 0; k < 3; k++) mb.Add(cyl, Matrix4x4.TRS(new Vector3((k - 1) * 0.13f, 0, 0), Quaternion.identity, new Vector3(0.1f, 0.3f, 0.1f))); break;
                default:
                    float len = kind == "smg" ? 0.62f : 0.34f;
                    mb.Add(cube, Matrix4x4.Scale(new Vector3(0.1f, 0.14f, len)));
                    mb.Add(cube, Matrix4x4.TRS(new Vector3(0, -0.14f, -len * 0.3f), Quaternion.identity, new Vector3(0.09f, 0.2f, 0.1f))); break;
            }
            mb.Add(cyl, Matrix4x4.TRS(new Vector3(0, -0.75f, 0), Quaternion.identity, new Vector3(1.2f, 0.02f, 1.2f)));
            var go = mb.ToGameObject("Icon", mat, t, false); go.isStatic = false;
            return t;
        }

        private void Drop(string kind, Vector3 pos, float respawn)
        {
            var mesh = PickupMesh(kind); mesh.position = new Vector3(pos.x, city.GroundHeight(pos.x, pos.z) + 0.85f, pos.z);
            pickups.Add(new Pickup { Kind = kind, Mesh = mesh, Respawn = respawn, Permanent = respawn > 0 });
        }

        private void PlacePickups(Vector3 spawn)
        {
            Drop("pistol", spawn + new Vector3(-6, 0, 1.2f), 30);
            Drop("health", spawn + new Vector3(6, 0, 1.2f), 30);
            var spots = new (string, float, float)[] { ("smg", 4.5f, 2.5f), ("armor", 2.5f, 3.5f), ("ammo", 5.5f, 4.5f), ("pistol", 1.5f, 1.5f), ("health", 6.5f, 6.5f), ("ammo", 2.5f, 6.5f), ("armor", 6.5f, 2.5f), ("smg", 1.5f, 5.5f) };
            foreach (var (kind, bi, bj) in spots)
            {
                var blk = city.BlockAt(bi * city.Pitch, bj * city.Pitch) ?? city.Blocks[0];
                var q = city.SidewalkPoint(blk, 0.13f); Drop(kind, new Vector3(q.x, 0.18f, q.z), 45);
            }
        }

        private bool Take(Pickup p)
        {
            var a = player.Arsenal;
            switch (p.Kind)
            {
                case "pistol":
                case "smg":
                    var k = p.Kind == "pistol" ? WeaponKind.Pistol : WeaponKind.Smg; var spec = WeaponSpec.All[(int)k];
                    bool first = !a.Owns(k); a.Give(k);
                    Toast?.Invoke(first ? $"{spec.Name} 획득 — 우클릭 조준, 좌클릭 사격" : $"{spec.Name} 탄약 +{spec.Magazine * 2}");
                    break;
                case "ammo": if (!a.GiveAmmo()) return false; Toast?.Invoke("탄약 보충"); break;
                case "health": if (player.Health >= 100) return false; player.Health = 100; Toast?.Invoke("체력 회복"); break;
                case "armor": if (player.Armor >= 100) return false; player.Armor = 100; Toast?.Invoke("방탄복 착용"); break;
            }
            PickedUp?.Invoke();
            return true;
        }

        private void UpdatePickups(float dt, float time)
        {
            for (int k = pickups.Count - 1; k >= 0; k--)
            {
                var p = pickups[k];
                if (p.Wait > 0) { p.Wait -= dt; if (p.Wait <= 0) p.Mesh.gameObject.SetActive(true); continue; }
                p.Mesh.Rotate(0, dt * 115, 0, Space.World);
                var pos = p.Mesh.position; pos.y = city.GroundHeight(pos.x, pos.z) + 0.85f + Mathf.Sin(time * 3 + k) * 0.08f; p.Mesh.position = pos;
                if (player.Car == null && Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(player.Position.x, player.Position.z)) < 1.3f && Take(p))
                {
                    if (p.Permanent) { p.Mesh.gameObject.SetActive(false); p.Wait = p.Respawn; }
                    else { UnityEngine.Object.Destroy(p.Mesh.gameObject); pickups.RemoveAt(k); }
                }
            }
        }

        // ------------------------------------------------------------------ player weapons
        private void EnsureGun()
        {
            if (gun != null) return;
            gun = new GameObject("Gun").transform; gun.SetParent(player.Rig.ForeR, false);
            gun.localPosition = new Vector3(0, -0.46f, 0.02f); gun.localRotation = Quaternion.Euler(90, 0, 0);
            var dark = mats.Plain(HarborMaterials.Hex(0x1b1d20), 0.7f);
            AttachMesh(gun, MeshBuilder.Cube(), dark, new Vector3(0, 0, 0.06f), new Vector3(0.05f, 0.08f, 0.2f));
            AttachMesh(gun, MeshBuilder.Cube(), dark, new Vector3(0, -0.07f, 0), new Vector3(0.045f, 0.11f, 0.05f));
            gunSmg = new GameObject("SmgParts").transform; gunSmg.SetParent(gun, false);
            AttachMesh(gunSmg, MeshBuilder.Cube(), dark, new Vector3(0, 0, 0.26f), new Vector3(0.055f, 0.07f, 0.24f));
            AttachMesh(gunSmg, MeshBuilder.Cube(), dark, new Vector3(0, -0.1f, 0.13f), new Vector3(0.04f, 0.16f, 0.05f));
        }

        private void Fire()
        {
            var a = player.Arsenal; var spec = a.Spec;
            if (spec.Melee) { Punch(); return; }
            aimHold = 1.2f;
            Vector3 o = cam.transform.position, d = cam.transform.forward;
            float spread = spec.Spread * (Aiming ? 1 : 2.2f) * (new Vector2(player.Velocity.x, player.Velocity.z).magnitude > 3 ? 1.6f : 1);
            d = (d + new Vector3(R(-spread, spread), R(-spread, spread), R(-spread, spread))).normalized;
            Vector3 body = player.Car != null ? player.Car.Position : player.Position;
            float startT = Vector3.Distance(o, body) - 0.2f;
            var hit = Cast(o, d, spec.Range + startT, Mathf.Max(0.5f, startT), player.Car);
            Vector3 end = o + d * hit.T;
            Vector3 muzzle = player.Car != null ? player.Car.Position + Vector3.up * 1.3f : player.Rig.ForeR.TransformPoint(new Vector3(0, -0.5f, 0.42f));
            Tracer(muzzle, end); Emit(FxFlash, muzzle, Vector3.zero, 0.05f, spec.Kind == WeaponKind.Smg ? 0.55f : 0.45f);
            flash.transform.position = muzzle; flash.intensity = 4;
            Shot?.Invoke(spec.Kind == WeaponKind.Smg);
            cameraRig.Shake = Mathf.Max(cameraRig.Shake, spec.Kind == WeaponKind.Smg ? 0.08f : 0.14f);
            var flat = new Vector3(d.x, 0, d.z).normalized;
            switch (hit.Kind)
            {
                case HitKind.Ped: DamagePed(hit.Ped, spec.Damage, flat, true); HitMarker = 0.18f; break;
                case HitKind.Officer: HurtOfficer(hit.Officer, spec.Damage, true); HitMarker = 0.18f; break;
                case HitKind.Car:
                    DamageCar(hit.Car, spec.Damage * 0.7f, true); HitMarker = 0.12f; Sparks(end);
                    if (hit.Car.Spec.Type == CarType.Police) Heat?.Invoke(1, "경찰차 사격", true);
                    break;
                case HitKind.None: break;
                default: Sparks(end); break;
            }
            ScarePeds(player.Position, 45);
            if (gunfireHeatCooldown <= 0) { Heat?.Invoke(1, "총기 사용", false); gunfireHeatCooldown = 6; }
        }

        private void Sparks(Vector3 at) { for (int k = 0; k < 4; k++) Emit(FxSpark, at, new Vector3(R(-2, 2), R(1, 3), R(-2, 2)), 0.3f, 0.1f); }

        private void Punch()
        {
            punchTimer = 0.25f; Swing?.Invoke();
            Vector3 f = new Vector3(Mathf.Sin(player.Yaw), 0, Mathf.Cos(player.Yaw));
            Pedestrian target = null; Officer officer = null; float best = WeaponSpec.All[0].Range;
            foreach (var p in world.Peds)
            {
                if (p.Mode != Pedestrian.State.Walk && p.Mode != Pedestrian.State.Flee) continue;
                Vector3 to = p.Pos - player.Position; to.y = 0; float d = to.magnitude;
                if (d < best && Vector3.Dot(to / Mathf.Max(d, 1e-3f), f) > 0.5f) { best = d; target = p; }
            }
            foreach (var o in Officers)
            {
                if (o.Down) continue; Vector3 to = o.Pos - player.Position; to.y = 0; float d = to.magnitude;
                if (d < best && Vector3.Dot(to / Mathf.Max(d, 1e-3f), f) > 0.5f) { best = d; officer = o; target = null; }
            }
            float dmg = WeaponSpec.All[0].Damage;
            if (target != null)
            {
                HitMarker = 0.15f; target.Health -= dmg;
                if (target.Health <= 0) { world.Knock(target, f.x * 3, f.z * 3, false); Heat?.Invoke(0.5f, "폭행", false); }
                else { target.Mode = Pedestrian.State.Flee; target.Timer = 6; target.Threat = player.Position; target.Pos += f * 0.4f; Heat?.Invoke(0.25f, "폭행", false); }
                if (CopNearby(30)) Heat?.Invoke(1, "경찰 앞에서 폭행", false);
            }
            else if (officer != null) { HitMarker = 0.15f; HurtOfficer(officer, dmg, true); }
        }

        private void UpdatePlayerWeapons(float dt, bool canAct)
        {
            EnsureGun();
            var a = player.Arsenal;
            if (Input.GetKeyDown(KeyCode.Alpha1)) a.Select(WeaponKind.Fist);
            if (Input.GetKeyDown(KeyCode.Alpha2)) a.Select(WeaponKind.Pistol);
            if (Input.GetKeyDown(KeyCode.Alpha3)) a.Select(WeaponKind.Smg);
            if (Input.GetKeyDown(KeyCode.Q)) a.Cycle();
            if (Input.GetKeyDown(KeyCode.R) && a.StartReload()) Click?.Invoke(true);
            a.Tick(dt);
            gunfireHeatCooldown -= dt; HitMarker -= dt; punchTimer -= dt; aimHold -= dt;
            Aiming = Input.GetMouseButton(1) && !a.Spec.Melee;
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            bool pressed = locked && Input.GetMouseButtonDown(0), held = locked && Input.GetMouseButton(0);
            if (canAct && !(player.Car != null && a.Spec.Melee) && a.TryFire(pressed, held)) Fire();
            gun.gameObject.SetActive(!a.Spec.Melee && player.Car == null);
            gunSmg.gameObject.SetActive(a.Current == WeaponKind.Smg);
            if (player.Car != null || player.IsDown) return;
            var r = player.Rig;
            if (!a.Spec.Melee && (Aiming || aimHold > 0))
            {
                player.FaceYaw(cameraRig.Yaw, dt);
                float pitch = -cameraRig.Pitch * 0.8f * Mathf.Rad2Deg;
                r.ArmR.localRotation = Quaternion.Euler(-90 + pitch, 0, -7); r.ForeR.localRotation = Quaternion.identity;
                r.ArmL.localRotation = Quaternion.Euler(-84 + pitch, 0, 29); r.ForeL.localRotation = Quaternion.Euler(-14, 0, 0);
                if (a.Reloading) { r.ArmL.localRotation = Quaternion.Euler(-34, 0, 0); r.ForeL.localRotation = Quaternion.Euler(-80, 0, 0); }
            }
            else if (punchTimer > 0)
            {
                float k = punchTimer / 0.25f;
                r.ArmR.localRotation = Quaternion.Euler(-90 * Mathf.Sin(k * Mathf.PI), 0, 11); r.ForeR.localRotation = Quaternion.Euler(-17 * k, 0, 0);
            }
        }

        private void UpdateBurning(float dt)
        {
            foreach (var v in world.Vehicles.ToArray())
            {
                if (v.Exploded) { if (rnd.NextDouble() < dt * 4) Emit(FxSmoke, v.Position + Vector3.up, new Vector3(R(-0.3f, 0.3f), R(1.5f, 3), R(-0.3f, 0.3f)), 3, 1.2f, 3); continue; }
                if (v.Burning <= 0) continue;
                float before = v.Burning; v.Burning -= dt;
                Vector3 eng = v.Position + v.Forward * v.Spec.Length * 0.35f + Vector3.up * 1.1f;
                if (rnd.NextDouble() < dt * 30) Emit(FxFire, eng, new Vector3(R(-0.5f, 0.5f), R(1.5f, 3), R(-0.5f, 0.5f)), 0.5f, 0.8f, 1.6f);
                if (rnd.NextDouble() < dt * 14) Emit(FxSmoke, eng, new Vector3(R(-0.5f, 0.5f), R(2, 4), R(-0.5f, 0.5f)), 2.5f, 1.2f, 3);
                if (v == player.Car && Mathf.Floor(before) != Mathf.Floor(v.Burning)) Toast?.Invoke("차에 불이 붙었습니다! F로 내리세요");
                if (v.Burning <= 0) Explode(v);
            }
        }

        /// <summary>Set by the game each frame: police have lost sight of the player.</summary>
        public bool Searching;

        /// <summary>Per-frame update.</summary>
        public void Tick(float dt, float time, int stars, bool playing)
        {
            UpdatePlayerWeapons(dt, playing && !player.IsDown);
            if (stars >= 2)
                foreach (var v in world.Vehicles.ToArray())
                {
                    if (v.Spec.Type != CarType.Police || v.Unloaded || v.Mode != VehicleMode.Police || v.Exploded) continue;
                    float d = Vector3.Distance(v.Position, player.Position);
                    if (d < (player.Car != null ? 14 : 26) && v.Speed < (player.Car != null ? 3 : 9)) SpawnOfficersFrom(v);
                }
            UpdateOfficers(dt, stars, playing, Searching);
            UpdateBurning(dt);
            UpdatePickups(dt, time);
            UpdateFx(dt);
        }

        /// <summary>Clears officers (respawn).</summary>
        public void Reset()
        {
            foreach (var o in Officers) UnityEngine.Object.Destroy(o.Rig.Root.gameObject);
            Officers.Clear();
        }
    }
}
