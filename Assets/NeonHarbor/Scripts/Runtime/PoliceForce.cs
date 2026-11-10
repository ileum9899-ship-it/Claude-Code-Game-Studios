using System.Collections.Generic;
using NeonHarbor.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonHarbor.Runtime
{
    /// <summary>
    /// Police brains. Cruisers follow the road graph with pure-pursuit steering, brake for
    /// corners, steer around buildings and cars with whisker rays, and work as a team:
    /// the closest unit chases and rams (PIT on the rear quarter), the next flanks to the
    /// intersection ahead, the third intercepts further on. Lost sight → each unit sweeps
    /// its own sector of the search area. Stuck units reverse toward open space and, if
    /// hopeless and out of view, are relocated. At 3+ stars roadblocks appear ahead of a
    /// driving player; at 4+ a helicopter follows with a searchlight (gunner at 5).
    /// Port of the browser build's src/11-police.js; maths in <see cref="PoliceTactics"/>.
    /// </summary>
    public sealed class PoliceForce
    {
        private sealed class Brain
        {
            public List<Vector3> Route = new List<Vector3>(); public float RouteTimer, GoalX, GoalZ, Stuck, Reverse, RevSteer, CutTimer; public int StuckCount; public PoliceRole Role;
        }
        private sealed class Heli { public Transform T, Rotor, Cone; public Material ConeMat; public Light Spot; public Vector3 Pos, Vel; public float Cooldown = 2; public bool Sees; }

        private readonly HarborConfig cfg;
        private readonly CityLayout city;
        private readonly HarborMaterials mats;
        private readonly WorldDirector world;
        private readonly CombatDirector combat;
        private readonly PlayerController player;
        private readonly WantedSystem wanted;
        private readonly Transform root;
        private readonly System.Random rnd = new System.Random(57);
        private readonly Dictionary<Vehicle, Brain> brains = new Dictionary<Vehicle, Brain>();
        private readonly Dictionary<object, int> units = new Dictionary<object, int>();
        private sealed class SearchLeg { public int Leg; public float Timer; public Vec2 Anchor; }
        private readonly Dictionary<object, SearchLeg> legs = new Dictionary<object, SearchLeg>();
        private readonly List<Vehicle> roadblock = new List<Vehicle>();
        private readonly List<Vec2> routeScratch = new List<Vec2>();
        private Vector3 roadblockAt;
        private float roleTimer, roadblockCooldown = 8, clock;
        private Heli heli;

        /// <summary>Message for the HUD.</summary>
        public event System.Action<string> Toast;
        /// <summary>Helicopter gunfire hit the player (damage).</summary>
        public event System.Action<float> HeliHit;
        /// <summary>Helicopter fired (sound).</summary>
        public event System.Action HeliShot;

        /// <summary>Creates the force; call <see cref="Tick"/> every frame and <see cref="Drive"/> for each police car.</summary>
        public PoliceForce(HarborConfig cfg, CityLayout city, HarborMaterials mats, WorldDirector world, CombatDirector combat, PlayerController player, WantedSystem wanted, Transform root)
        {
            this.cfg = cfg; this.city = city; this.mats = mats; this.world = world; this.combat = combat; this.player = player; this.wanted = wanted; this.root = root;
        }

        private float R(float a, float b) { return a + (b - a) * (float)rnd.NextDouble(); }
        private Vector3 Target => player.Car != null ? player.Car.Position : player.Position;
        private Vector3 TargetVel => player.Car != null ? new Vector3(player.Car.Model.Velocity.X, 0, player.Car.Model.Velocity.Y) : new Vector3(player.Velocity.x, 0, player.Velocity.z);
        private bool Clear(Vector3 a, Vector3 b, float y = 1f) { return city.Raycast(a.x, y, a.z, b.x, y, b.z) >= 0.999f; }

        /// <summary>True when the helicopter has the player in view.</summary>
        public bool HeliSees => heli != null && heli.Sees;

        /// <summary>Search-area point for a unit (car or officer): each unit sweeps its own sector.</summary>
        public Vector3 SearchTarget(object unit)
        {
            if (!units.TryGetValue(unit, out int idx)) { idx = units.Count; units[unit] = idx; }
            if (!legs.TryGetValue(unit, out var leg)) legs[unit] = leg = new SearchLeg { Anchor = wanted.LastKnown };
            if (Vec2.Distance(leg.Anchor, wanted.LastKnown) > 5) { leg.Anchor = wanted.LastKnown; leg.Leg = 0; leg.Timer = 0; } // new sighting: start over at it
            var p = PoliceTactics.SearchWaypoint(wanted.LastKnown, wanted.SearchRadius, idx % 4, Mathf.Clamp(units.Count, 1, 4), leg.Leg);
            Vector3 at = unit is Vehicle v ? v.Position : unit is CombatDirector.Officer o ? o.Pos : heli != null && unit == (object)heli ? heli.Pos : Vector3.zero;
            leg.Timer += Time.deltaTime;
            if (Vector2.Distance(new Vector2(at.x, at.z), new Vector2(p.X, p.Y)) < 14 || leg.Timer > 15) { leg.Leg++; leg.Timer = 0; }
            return new Vector3(p.X, 0, p.Y);
        }

        /// <summary>Field of view of a police car / officer toward a point (line of sight not included).</summary>
        public static bool InView(Vector3 eye, float yaw, Vector3 target, float fov, float range, float near)
        {
            return PoliceTactics.InView(new Vec2(eye.x, eye.z), yaw, new Vec2(target.x, target.z), fov, range, near);
        }

        // ------------------------------------------------------------------ driving
        private List<Vector3> RouteTo(Vehicle car, Brain b, Vector3 goal)
        {
            b.RouteTimer -= Time.deltaTime;
            if (b.Route.Count == 0 || b.RouteTimer <= 0 || Vector2.Distance(new Vector2(goal.x, goal.z), new Vector2(b.GoalX, b.GoalZ)) > 12)
            {
                b.RouteTimer = 0.8f; b.GoalX = goal.x; b.GoalZ = goal.z; b.Route.Clear();
                var nodes = city.Path(city.NearestNode(car.Position.x, car.Position.z), city.NearestNode(goal.x, goal.z));
                var f = car.Forward; b.Route.Add(car.Position);
                for (int k = 0; k < nodes.Count; k++)
                {
                    var n = city.NodePos(nodes[k].Item1, nodes[k].Item2); var p = new Vector3(n.X, 0, n.Y);
                    if (k == 0 && nodes.Count > 1 && Vector3.Dot(p - car.Position, f) < 0 && Vector3.Distance(p, car.Position) < 14) continue; // don't double back
                    b.Route.Add(p);
                }
                b.Route.Add(goal);
            }
            return b.Route;
        }

        private float Probe(Vehicle car, float angle, float reach)
        {
            float yaw = car.Model.Yaw + angle; Vector3 d = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw)), p = car.Position;
            return city.Raycast(p.x + d.x * 2, 1, p.z + d.z * 2, p.x + d.x * reach, 1, p.z + d.z * reach);
        }

        private void Steer(Vehicle car, List<Vector3> route, Vector3? direct, bool ram, float? stopAt, float topSpeed)
        {
            float sp = car.Speed;
            route[0] = car.Position;
            routeScratch.Clear(); foreach (var p in route) routeScratch.Add(new Vec2(p.x, p.z));
            Vec2 aim = direct.HasValue ? new Vec2(direct.Value.x, direct.Value.z) : PoliceTactics.LookAhead(routeScratch, new Vec2(car.Position.x, car.Position.z), 7 + sp * 0.45f);
            float diff = Mathx.AngleDiff(car.Model.Yaw, Mathf.Atan2(aim.X - car.Position.x, aim.Y - car.Position.z));
            float vmax = topSpeed, along = 0;
            for (int k = 1; k + 1 < routeScratch.Count && along < 120; k++)
            {
                along += Vec2.Distance(routeScratch[k], routeScratch[k - 1]);
                float ang = PoliceTactics.TurnAngle(routeScratch[k - 1], routeScratch[k], routeScratch[k + 1]);
                if (ang > 0.3f) { vmax = Mathf.Min(vmax, PoliceTactics.SpeedForCorner(ang, along - 6, topSpeed)); break; }
            }
            if (stopAt.HasValue) vmax = Mathf.Min(vmax, Mathf.Sqrt(2 * 12 * Mathf.Max(0, stopAt.Value)));
            float reach = 6 + sp * 0.6f, cl = Probe(car, 0.38f, reach), cc = Probe(car, 0, reach), cr = Probe(car, -0.38f, reach);
            float carAhead = 1; var f = car.Forward;
            foreach (var v in world.Vehicles)
            {
                if (v == car || v == player.Car) continue;
                Vector3 d = v.Position - car.Position; float al = Vector3.Dot(d, f), lat = Mathf.Abs(d.x * f.z - d.z * f.x);
                if (al > 0 && al < reach && lat < 2.2f) carAhead = Mathf.Min(carAhead, al / reach);
            }
            float block = Mathf.Min(cc, carAhead);
            // in Unity positive yaw turns toward +x (right): steer toward the side with more room
            float toRoomier = cl > cr ? 1 : -1; // cl probes yaw + 0.38, i.e. the side a positive steer turns toward
            if (block < 0.6f) { diff += toRoomier * (0.6f - block) * 2.2f; vmax = Mathf.Min(vmax, 6 + block * 20); }
            else if (Mathf.Min(cl, cr) < 0.45f) diff += toRoomier * 0.35f;
            var input = new VehicleInput
            {
                Steer = Mathf.Clamp(diff * 2.4f, -1, 1),
                Throttle = sp < vmax - 1 ? 1 : sp > vmax + 2 ? -1 : 0.25f,
                Brake = sp > vmax + 6 ? 0.6f : 0,
                Handbrake = Mathf.Abs(diff) > 1.1f && sp > 13,
            };
            if (ram) { input.Throttle = 1; input.Brake = 0; }
            car.Input = input;
        }

        private void Relocate(Vehicle car, Brain b)
        {
            var (pi, pj) = city.NearestNode(player.Position.x, player.Position.z);
            for (int a = 0; a < 20; a++)
            {
                int i = Mathf.Clamp(pi + rnd.Next(-3, 4), 0, city.N), j = Mathf.Clamp(pj + rnd.Next(-3, 4), 0, city.N);
                var n = city.NodePos(i, j); var p = new Vector3(n.X, 0, n.Y); float d = Vector3.Distance(p, player.Position);
                if (d < 80 || d > 190 || Clear(p, player.Position, 1.5f)) continue;
                car.Place(p, Mathf.Atan2(player.Position.x - p.x, player.Position.z - p.z)); b.Route.Clear(); b.StuckCount = 0; return;
            }
        }

        /// <summary>One frame for a pursuing police car.</summary>
        public void Drive(Vehicle car, float dt)
        {
            if (!brains.TryGetValue(car, out var b)) brains[car] = b = new Brain();
            if (roadblock.Contains(car)) { car.Input = new VehicleInput { Brake = 1 }; car.Step(dt, city); return; }
            float top = cfg.CarMaxSpeed * cfg.CopSpeed * 0.95f, sp = car.Speed, d = Vector3.Distance(Target, car.Position);
            if (b.Reverse > 0) { b.Reverse -= dt; car.Input = new VehicleInput { Throttle = -1, Steer = b.RevSteer }; }
            else if (wanted.Searching) Steer(car, RouteTo(car, b, SearchTarget(car)), null, false, null, 16);
            else
            {
                Vector3 goal, real = Target, pv = TargetVel; Vector3? direct = null; bool ram = false; float? stopAt = null;
                if (b.Role == PoliceRole.Pursuer || d < 40 || b.CutTimer > 0)
                {
                    var ic = PoliceTactics.Intercept(new Vec2(car.Position.x, car.Position.z), Mathf.Max(sp, 20), new Vec2(real.x, real.z), new Vec2(pv.x, pv.z), 2.5f);
                    goal = new Vector3(ic.X, 0, ic.Y);
                    if (d < 30 && Clear(car.Position, goal))
                    {
                        direct = goal;
                        if (player.Car != null && d < 14)
                        {
                            // PIT: aim at the rear quarter on our side of the target
                            var tf = player.Car.Forward; var tr = new Vector3(tf.z, 0, -tf.x);
                            float side = Mathf.Sign(Vector3.Dot(car.Position - real, tr)); if (side == 0) side = 1;
                            direct = real - tf * 1.6f + tr * side * 0.5f; ram = true;
                        }
                    }
                    if (player.Car == null) { stopAt = d - 9; ram = false; }
                    b.CutTimer = Mathf.Max(0, b.CutTimer - dt);
                }
                else
                {
                    Vector3 h = pv.magnitude > 2 ? pv : new Vector3(Mathf.Sin(player.Car != null ? player.Car.Model.Yaw : player.Yaw), 0, Mathf.Cos(player.Car != null ? player.Car.Model.Yaw : player.Yaw));
                    var node = PoliceTactics.NodeAhead(city, new Vec2(real.x, real.z), new Vec2(h.x, h.z), b.Role == PoliceRole.Interceptor ? 2 : 1);
                    var np = city.NodePos(node.Item1, node.Item2); goal = new Vector3(np.X, 0, np.Y);
                    if (Vector3.Distance(goal, car.Position) < 12 || Vector3.Dot(goal - real, h) < 0) b.CutTimer = 4;
                }
                Steer(car, RouteTo(car, b, goal), direct, ram, stopAt, top);
            }
            if (b.Reverse <= 0)
            {
                b.Stuck = sp < 1.5f && car.Input.Throttle > 0.3f ? b.Stuck + dt : 0;
                if (b.Stuck > 1f)
                {
                    b.Stuck = 0; b.Reverse = 1.2f; b.StuckCount++;
                    float l = Probe(car, -0.6f, 8), r = Probe(car, 0.6f, 8);
                    b.RevSteer = r > l ? -1 : 1; // reversing with opposite lock swings the nose toward the open side
                    if (b.StuckCount >= 4 && d > 70) Relocate(car, b);
                }
                else if (sp > 6) b.StuckCount = 0;
            }
            car.Step(dt, city);
        }

        // ------------------------------------------------------------------ per frame: roles, roadblocks, helicopter
        /// <summary>Assigns roles, manages roadblocks and the helicopter.</summary>
        public void Tick(float dt, float time, float night)
        {
            clock = time;
            roleTimer -= dt;
            if (roleTimer <= 0)
            {
                roleTimer = 0.5f;
                var chasers = new List<Vehicle>(); var dists = new List<float>();
                foreach (var v in world.Vehicles) if (v.Mode == VehicleMode.Police && !roadblock.Contains(v) && !v.Exploded) { chasers.Add(v); dists.Add(Vector3.Distance(v.Position, Target)); }
                var roles = PoliceTactics.AssignRoles(dists);
                for (int k = 0; k < chasers.Count; k++) { if (!brains.TryGetValue(chasers[k], out var b)) brains[chasers[k]] = b = new Brain(); b.Role = roles[k]; }
                var dead = new List<object>(); foreach (var u in units.Keys) if (u is Vehicle v && !world.Vehicles.Contains(v) || u is CombatDirector.Officer o && !combat.Officers.Contains(o)) dead.Add(u);
                foreach (var u in dead) { units.Remove(u); legs.Remove(u); }
                var gone = new List<Vehicle>(); foreach (var v in brains.Keys) if (!world.Vehicles.Contains(v)) gone.Add(v);
                foreach (var v in gone) brains.Remove(v);
            }
            UpdateRoadblock(dt);
            UpdateHeli(dt, night);
        }

        private void UpdateRoadblock(float dt)
        {
            roadblockCooldown -= dt;
            if (roadblock.Count > 0)
            {
                if (wanted.Stars == 0 || Vector3.Distance(roadblockAt, player.Position) > 230)
                {
                    foreach (var c in roadblock) if (world.Vehicles.Contains(c) && !c.Exploded && c != player.Car) world.Remove(c);
                    roadblock.Clear();
                }
                return;
            }
            if (wanted.Stars < 3 || player.Car == null || player.Car.Speed < 8 || roadblockCooldown > 0) return;
            var v = player.Car.Model.Velocity;
            var node = PoliceTactics.NodeAhead(city, player.Car.Model.Position, v, 2);
            var np = city.NodePos(node.Item1, node.Item2); var at = new Vector3(np.X, 0, np.Y);
            float dist = Vector3.Distance(at, player.Position);
            if (dist < 70 || dist > 180) return;
            bool alongX = Mathf.Abs(v.X) >= Mathf.Abs(v.Y); float dir = alongX ? Mathf.Sign(v.X) : Mathf.Sign(v.Y);
            Vector3 b = alongX ? at - new Vector3(dir * (city.HalfRoad + 6), 0, 0) : at - new Vector3(0, 0, dir * (city.HalfRoad + 6));
            foreach (int s in new[] { -1, 1 })
            {
                var c = world.SpawnCar(CarType.Police); c.Mode = VehicleMode.Police;
                Vector3 p = b + (alongX ? new Vector3(0, 0, s * 3.1f) : new Vector3(s * 3.1f, 0, 0));
                float yaw = alongX ? (s > 0 ? 0 : Mathf.PI) + 0.25f * s : (s > 0 ? Mathf.PI / 2 : -Mathf.PI / 2) + 0.25f * s; // broadside across the lane
                c.Place(p, yaw); roadblock.Add(c);
                combat.SpawnOfficersFrom(c);
            }
            roadblockAt = b; roadblockCooldown = 30;
            Toast?.Invoke("전방에 경찰 바리케이드!");
        }

        private Heli BuildHeli()
        {
            var h = new Heli { Pos = player.Position + new Vector3(120, 45, 120) };
            h.T = new GameObject("PoliceHelicopter").transform; h.T.SetParent(root, false);
            var dark = mats.Paint(HarborMaterials.Hex(0x1a2030)); var white = mats.Paint(new Color(0.92f, 0.92f, 0.92f));
            var mb = new MeshBuilder(); mb.Add(MeshBuilder.Sphere(10, 16), Matrix4x4.Scale(new Vector3(1.6f, 1.45f, 2.7f)));
            mb.Add(MeshBuilder.Cylinder(8, 0.55f), Matrix4x4.TRS(new Vector3(0, 0.3f, -4.6f), Quaternion.Euler(90, 0, 0), new Vector3(0.45f, 6, 0.45f)));
            mb.Add(MeshBuilder.Cube(), Matrix4x4.TRS(new Vector3(0, 0.9f, -7.4f), Quaternion.identity, new Vector3(0.1f, 1.4f, 0.9f)));
            mb.Add(MeshBuilder.Cube(), Matrix4x4.TRS(new Vector3(0.9f, -1.6f, 0), Quaternion.identity, new Vector3(0.1f, 0.1f, 3.4f)));
            mb.Add(MeshBuilder.Cube(), Matrix4x4.TRS(new Vector3(-0.9f, -1.6f, 0), Quaternion.identity, new Vector3(0.1f, 0.1f, 3.4f)));
            var body = mb.ToGameObject("Body", dark, h.T); body.isStatic = false;
            var sb = new MeshBuilder(); sb.Add(MeshBuilder.Cube(), Matrix4x4.TRS(new Vector3(0, -0.2f, 0.3f), Quaternion.identity, new Vector3(3.15f, 0.35f, 2f)));
            var stripe = sb.ToGameObject("Stripe", white, h.T); stripe.isStatic = false;
            h.Rotor = new GameObject("Rotor").transform; h.Rotor.SetParent(h.T, false); h.Rotor.localPosition = new Vector3(0, 1.7f, 0);
            var rb = new MeshBuilder(); rb.Add(MeshBuilder.Cube(), Matrix4x4.Scale(new Vector3(11, 0.06f, 0.35f))); rb.Add(MeshBuilder.Cube(), Matrix4x4.Scale(new Vector3(0.35f, 0.06f, 11)));
            var rotor = rb.ToGameObject("Blades", dark, h.Rotor); rotor.isStatic = false;
            h.Cone = new GameObject("Searchlight").transform; h.Cone.SetParent(h.T, false);
            var cb = new MeshBuilder(); cb.Add(MeshBuilder.Cylinder(24, 0.05f), Matrix4x4.TRS(new Vector3(0, -22, 0), Quaternion.identity, new Vector3(12, 44, 12)));
            h.ConeMat = new Material(mats.Pool) { mainTexture = Texture2D.whiteTexture };
            var cone = cb.ToGameObject("Cone", h.ConeMat, h.Cone, false); cone.isStatic = false;
            h.Spot = new GameObject("Spot").AddComponent<Light>(); h.Spot.transform.SetParent(h.T, false); h.Spot.type = LightType.Spot; h.Spot.range = 120; h.Spot.spotAngle = 18; h.Spot.color = new Color(1f, 0.95f, 0.82f); h.Spot.shadows = LightShadows.None;
            return h;
        }

        private void UpdateHeli(float dt, float night)
        {
            if (wanted.Stars < 4)
            {
                if (heli == null) return;
                heli.Vel.y += 3 * dt; heli.Pos += heli.Vel * dt; heli.T.position = heli.Pos; heli.Sees = false;
                if (heli.Pos.y > 140) { Object.Destroy(heli.T.gameObject); heli = null; }
                return;
            }
            if (heli == null) { heli = BuildHeli(); Toast?.Invoke("경찰 헬기가 떴습니다"); }
            Vector3 real = Target, goal = wanted.Searching ? SearchTarget(heli) : new Vector3(real.x, 0, real.z);
            Vector3 to = new Vector3(goal.x - heli.Pos.x - 10, 45 - heli.Pos.y, goal.z - heli.Pos.z - 10);
            Vector3 desired = Vector3.ClampMagnitude(to * 0.8f, 30);
            heli.Vel = Vector3.Lerp(heli.Vel, desired, 1 - Mathf.Exp(-1.2f * dt)); heli.Pos += heli.Vel * dt;
            heli.T.SetPositionAndRotation(heli.Pos, Quaternion.Euler(Mathf.Clamp(heli.Vel.magnitude * 0.7f, 0, 17), Mathf.Atan2(heli.Vel.x, heli.Vel.z) * Mathf.Rad2Deg, 0));
            heli.Rotor.Rotate(0, dt * 2300, 0, Space.Self);
            Vector3 aim = wanted.Searching ? goal : new Vector3(real.x, 0, real.z), dir = aim - heli.Pos;
            heli.Cone.rotation = Quaternion.FromToRotation(Vector3.down, dir.normalized);
            heli.Cone.localScale = new Vector3(1, Mathf.Clamp(dir.magnitude / 44, 0.6f, 2), 1);
            heli.ConeMat.SetColor("_TintColor", new Color(1f, 0.95f, 0.8f, 0.05f + night * 0.14f));
            heli.Spot.transform.rotation = Quaternion.LookRotation(dir); heli.Spot.intensity = 1 + night * 6;
            float horiz = Vector2.Distance(new Vector2(real.x, real.z), new Vector2(heli.Pos.x, heli.Pos.z));
            heli.Sees = horiz < 75 && city.Raycast(heli.Pos.x, heli.Pos.y - 2, heli.Pos.z, real.x, 1.2f, real.z) >= 0.999f;
            if (heli.Sees && wanted.Stars >= 5)
            {
                heli.Cooldown -= dt;
                if (heli.Cooldown <= 0)
                {
                    heli.Cooldown = R(1.2f, 2f); bool hit = rnd.NextDouble() < 0.3;
                    var end = new Vector3(real.x + (hit ? 0 : R(-3, 3)), 1, real.z + (hit ? 0 : R(-3, 3)));
                    combat.Tracer(heli.Pos + Vector3.down * 1.5f, end); HeliShot?.Invoke();
                    if (hit) { if (player.Car != null) combat.DamageCar(player.Car, 8, false); else HeliHit?.Invoke(9); }
                }
            }
        }
    }
}
