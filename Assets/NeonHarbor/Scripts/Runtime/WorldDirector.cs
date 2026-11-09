using System;
using System.Collections.Generic;
using NeonHarbor.Core;
using UnityEngine;

namespace NeonHarbor.Runtime
{
    /// <summary>A pedestrian walking the sidewalk ring of a block.</summary>
    public sealed class Pedestrian
    {
        public enum State { Walk, Flee, Air, Down, Gone }
        public HumanRig Rig; public Block Block; public float T, Dir = 1, Speed, Timer, Yaw, Spin; public State Mode;
        public Vector3 Pos, Vel; public Vector3 Threat;
    }

    /// <summary>
    /// Owns everything that moves on its own: traffic (lane following, signals, car
    /// following), parked cars, police pursuit (graph routing + direct chase),
    /// pedestrians (walk / flee / knocked down), and car–car / car–person contacts.
    /// Keeps the population around the player.
    /// </summary>
    public sealed class WorldDirector
    {
        private readonly HarborConfig cfg;
        private readonly CityLayout city;
        private readonly HarborMaterials mats;
        private readonly TrafficLights lights;
        private readonly Transform root;
        private readonly System.Random rnd = new System.Random(99);

        public readonly List<Vehicle> Vehicles = new List<Vehicle>();
        public readonly List<Pedestrian> Peds = new List<Pedestrian>();

        /// <summary>Player hit a pedestrian with a car.</summary>
        public event Action PedHitByPlayer;
        /// <summary>Player rammed a police car (impact speed).</summary>
        public event Action<float> CopRammed;
        /// <summary>A collision happened near the player (0..1 strength, involves player).</summary>
        public event Action<float, bool> Crash;
        /// <summary>A car honked at a position.</summary>
        public event Action<Vector3> Honk;

        /// <summary>Set by the game: the player (for spawning, AI targets, contacts).</summary>
        public PlayerController Player;

        /// <summary>Creates the director.</summary>
        public WorldDirector(HarborConfig cfg, CityLayout city, HarborMaterials mats, Transform root)
        {
            this.cfg = cfg; this.city = city; this.mats = mats; this.root = root;
            lights = new TrafficLights(cfg.LightCycle, cfg.LightYellow);
        }

        /// <summary>Traffic-light schedule.</summary>
        public TrafficLights Lights => lights;

        private float R(float a, float b) { return a + (b - a) * (float)rnd.NextDouble(); }
        private T Pick<T>(IList<T> l) { return l[rnd.Next(l.Count)]; }

        // ------------------------------------------------------------------ spawning
        /// <summary>Spawns a car of a type with a random paint for that type.</summary>
        public Vehicle SpawnCar(CarType type, int? paintHex = null)
        {
            var spec = VehicleFactory.Specs[type];
            var v = new Vehicle(type, HarborMaterials.Hex(paintHex ?? Pick(spec.Paints)), cfg, mats, root);
            Vehicles.Add(v);
            return v;
        }

        /// <summary>Seats an NPC driver.</summary>
        public void AddDriver(Vehicle v, bool police = false)
        {
            var look = HumanRig.RandomLook(rnd); if (police) look.Top = HarborMaterials.Hex(0x1d2a44);
            var d = HumanRig.Create(mats, v.Parts.Seat, look, false); d.Sit(); d.Root.localScale = Vector3.one * 0.95f; v.Driver = d;
        }

        /// <summary>Removes the NPC driver from a car.</summary>
        public void RemoveDriver(Vehicle v) { if (v.Driver != null) { UnityEngine.Object.Destroy(v.Driver.Root.gameObject); v.Driver = null; } }

        /// <summary>Despawns a car.</summary>
        public void Remove(Vehicle v) { Vehicles.Remove(v); UnityEngine.Object.Destroy(v.Parts.Root.gameObject); }

        private (int, int) ChooseNext((int, int) prev, (int, int) cur)
        {
            var opts = city.Neighbours(cur.Item1, cur.Item2); opts.RemoveAll(n => n.Equals(prev));
            return opts.Count > 0 ? Pick(opts) : prev;
        }

        private bool SpawnTraffic(Vector3 near)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var a = (rnd.Next(city.N + 1), rnd.Next(city.N + 1)); var b = Pick(city.Neighbours(a.Item1, a.Item2));
                var piece = LanePiece.Segment(city, a, b); float s = R(0, piece.Length); var p = piece.Eval(s / piece.Length);
                float d = Vector2.Distance(new Vector2(p.X, p.Y), new Vector2(near.x, near.z));
                if (d < 70 || d > 230 || Crowded(p.X, p.Y, 9)) continue;
                var types = new[] { CarType.Sedan, CarType.Sedan, CarType.Sedan, CarType.Suv, CarType.Suv, CarType.Taxi, CarType.Sports, CarType.Van };
                var v = SpawnCar(types[rnd.Next(types.Length)]);
                v.Mode = VehicleMode.Traffic; v.Piece = piece; v.S = s; v.Want = cfg.TrafficCruise * R(0.85f, 1.15f); v.AiSpeed = v.Want * 0.9f; v.Next = ChooseNext(a, b);
                AddDriver(v); PlaceOnPiece(v);
                return true;
            }
            return false;
        }

        private bool Crowded(float x, float z, float r)
        {
            foreach (var v in Vehicles) if (Mathf.Abs(v.Model.Position.X - x) < r && Mathf.Abs(v.Model.Position.Y - z) < r) return true;
            return false;
        }

        /// <summary>Parks cars along the curbs and fills the streets with traffic and people.</summary>
        public void Populate(Vector3 playerPos, Vector3 heroCarPos, float heroYaw)
        {
            int n = 0;
            for (int a = 0; a < 400 && n < cfg.ParkedCount; a++)
            {
                var A = (rnd.Next(city.N + 1), rnd.Next(city.N + 1)); var B = Pick(city.Neighbours(A.Item1, A.Item2));
                var pc = LanePiece.Segment(city, A, B); var p = pc.Eval(R(14, pc.Length - 14) / pc.Length);
                var r = new Vec2(pc.F.Y, -pc.F.X); float x = p.X + r.X * 1.8f, z = p.Y + r.Y * 1.8f;
                if (Crowded(x, z, 7)) continue;
                var types = new[] { CarType.Sedan, CarType.Sedan, CarType.Suv, CarType.Sports, CarType.Van, CarType.Taxi };
                var v = SpawnCar(types[rnd.Next(types.Length)]); v.Mode = VehicleMode.Parked; v.Place(new Vector3(x, 0, z), pc.F.ToYaw()); n++;
            }
            var hero = SpawnCar(CarType.Sports, 0xd61f1f); hero.Mode = VehicleMode.Parked; hero.Place(heroCarPos, heroYaw);
            for (int k = 0; k < cfg.TrafficCount; k++) SpawnTraffic(playerPos);
            for (int k = 0; k < cfg.PedCount; k++) { var blk = city.BlockAt(playerPos.x + R(-60, 60), playerPos.z + R(-60, 60)); if (blk != null && blk.Type != BlockType.Harbor) SpawnPed(blk); }
        }

        /// <summary>Spawns a pedestrian on a block's sidewalk.</summary>
        public Pedestrian SpawnPed(Block blk, float? t = null)
        {
            var p = new Pedestrian { Rig = HumanRig.Create(mats, root, HumanRig.RandomLook(rnd)), Block = blk, T = t ?? (float)rnd.NextDouble(), Dir = rnd.NextDouble() < 0.5 ? 1 : -1, Speed = cfg.PedSpeed * R(0.8f, 1.2f) };
            var q = city.SidewalkPoint(blk, p.T); p.Pos = new Vector3(q.x, 0.18f, q.z);
            Peds.Add(p); return p;
        }

        private void RemovePed(Pedestrian p) { UnityEngine.Object.Destroy(p.Rig.Root.gameObject); Peds.Remove(p); }

        /// <summary>Spawns a police car 90–200 m from the player, facing them.</summary>
        public Vehicle SpawnPolice()
        {
            var (pi, pj) = city.NearestNode(Player.Position.x, Player.Position.z);
            for (int a = 0; a < 30; a++)
            {
                var node = (Mathf.Clamp(pi + rnd.Next(-3, 4), 0, city.N), Mathf.Clamp(pj + rnd.Next(-3, 4), 0, city.N));
                var p = city.NodePos(node.Item1, node.Item2);
                float d = Vector2.Distance(new Vector2(p.X, p.Y), new Vector2(Player.Position.x, Player.Position.z));
                if (d < 90 || d > 200 || Crowded(p.X, p.Y, 10)) continue;
                var v = SpawnCar(CarType.Police); v.Mode = VehicleMode.Police;
                v.Place(new Vector3(p.X, 0, p.Y), Mathf.Atan2(Player.Position.x - p.X, Player.Position.z - p.Y)); AddDriver(v, true);
                return v;
            }
            return null;
        }

        // ------------------------------------------------------------------ per frame
        /// <summary>Runs AI, contacts and spawning. Returns the distance to the nearest chasing cop.</summary>
        public float Tick(float dt, float time, int stars, int desiredCops)
        {
            int traffic = 0, cops = 0; float nearestCop = 1e9f;
            for (int k = Vehicles.Count - 1; k >= 0; k--)
            {
                var v = Vehicles[k];
                switch (v.Mode)
                {
                    case VehicleMode.Traffic: UpdateTraffic(v, dt, time); traffic++; break;
                    case VehicleMode.Wreck: v.Input = new VehicleInput { Brake = 0.3f }; v.Step(dt, city); traffic++; break;
                    case VehicleMode.Police:
                        if (stars == 0) { v.Mode = VehicleMode.Parked; break; }
                        UpdatePolice(v, dt, time); cops++;
                        nearestCop = Mathf.Min(nearestCop, Vector3.Distance(v.Position, Player.Position));
                        break;
                    case VehicleMode.Parked:
                        if (v.Speed > 0.05f || Mathf.Abs(v.Model.YawRate) > 0.01f) { v.Input = new VehicleInput { Brake = 0.6f }; v.Step(dt, city); }
                        if (v.Parts.SirenRed != null) SetSiren(v, false, false);
                        break;
                }
            }
            ResolveContacts();
            for (int k = Peds.Count - 1; k >= 0; k--) UpdatePed(Peds[k], dt);
            // population upkeep
            if (traffic < cfg.TrafficCount) SpawnTraffic(Player.Position);
            if (cops < desiredCops && UnityEngine.Random.value < dt * 0.8f) SpawnPolice();
            for (int k = Vehicles.Count - 1; k >= 0; k--)
            {
                var v = Vehicles[k];
                if (v == Player.Car) continue;
                float d = Vector3.Distance(v.Position, Player.Position);
                if (d > 260 && (v.Mode == VehicleMode.Traffic || v.Mode == VehicleMode.Wreck || (v.Spec.Type == CarType.Police && stars == 0))) Remove(v);
            }
            for (int k = Peds.Count - 1; k >= 0; k--) { var p = Peds[k]; if (p.Mode == Pedestrian.State.Gone || Vector3.Distance(p.Pos, Player.Position) > cfg.PedRadius * 1.6f) RemovePed(p); }
            for (int guard = 0; guard < 8 && Peds.Count < cfg.PedCount; guard++)
            {
                var blk = city.BlockAt(Player.Position.x + R(-cfg.PedRadius, cfg.PedRadius), Player.Position.z + R(-cfg.PedRadius, cfg.PedRadius));
                if (blk == null || blk.Type == BlockType.Harbor) continue;
                var p = SpawnPed(blk); if (Vector3.Distance(p.Pos, Player.Position) < 18 && Peds.Count > 20) RemovePed(p);
            }
            return nearestCop;
        }

        private void PlaceOnPiece(Vehicle v)
        {
            float t = Mathf.Clamp01(v.S / v.Piece.Length);
            var p = v.Piece.Eval(t); var tg = v.Piece.Tangent(t);
            float yaw = tg.ToYaw();
            v.Model.YawRate = Mathx.AngleDiff(v.Model.Yaw, yaw) / Mathf.Max(Time.deltaTime, 1e-4f);
            v.Model.Position = p; v.Model.Yaw = yaw; v.Model.Velocity = Vec2.FromYaw(yaw) * v.AiSpeed;
        }

        private float ObstacleAhead(Vehicle v, float maxDist)
        {
            var f = v.Model.Forward; var r = new Vec2(f.Y, -f.X); float best = maxDist;
            void Test(float x, float z, float rad)
            {
                float dx = x - v.Model.Position.X, dz = z - v.Model.Position.Y, along = dx * f.X + dz * f.Y, lat = Mathf.Abs(dx * r.X + dz * r.Y);
                if (along > 0 && along < best + 3 && lat < 1.4f + rad) best = Mathf.Min(best, along - v.Spec.Length / 2 - rad);
            }
            foreach (var o in Vehicles) if (o != v) Test(o.Model.Position.X, o.Model.Position.Y, o.Spec.Length / 2);
            foreach (var p in Peds) if (p.Mode != Pedestrian.State.Down && city.OnRoad(p.Pos.x, p.Pos.z)) Test(p.Pos.x, p.Pos.z, 0.4f);
            if (Player.Car == null) Test(Player.Position.x, Player.Position.z, 0.4f);
            return best;
        }

        private void UpdateTraffic(Vehicle v, float dt, float time)
        {
            float want = v.Piece.IsTurn && !v.Piece.Straight ? cfg.TrafficTurnSpeed : v.Want;
            if (!v.Piece.IsTurn)
            {
                float toEnd = v.Piece.Length - v.S; var st = lights.State(v.Piece.B.Item1, v.Piece.B.Item2, v.Piece.Axis, time);
                float stopAt = toEnd - 4.6f - v.Spec.Length / 2;
                if (st == Signal.Red || (st == Signal.Yellow && stopAt > v.AiSpeed * v.AiSpeed / 12f))
                    if (stopAt > -0.5f) want = Mathf.Min(want, Mathf.Max(0, Mathf.Sqrt(Mathf.Max(0, stopAt) * 2 * 5.5f) - 0.3f));
            }
            float gap = ObstacleAhead(v, cfg.FollowGap * 2.2f);
            if (gap < cfg.FollowGap * 2.2f) want = Mathf.Min(want, Mathf.Max(0, (gap - 2.2f) * 1.1f));
            v.Honk -= dt;
            if (gap < 3 && Player.Car == null && Vector3.Distance(Player.Position, v.Position) < 8 && v.Honk <= 0) { Honk?.Invoke(v.Position); v.Honk = 4; }
            float accel = want > v.AiSpeed ? 4 : 9;
            v.AiSpeed = want > v.AiSpeed ? Mathf.Min(want, v.AiSpeed + accel * dt) : Mathf.Max(want, v.AiSpeed - accel * dt);
            v.S += v.AiSpeed * dt;
            while (v.S >= v.Piece.Length)
            {
                v.S -= v.Piece.Length;
                var p = v.Piece;
                if (!p.IsTurn) v.Piece = LanePiece.Turn(city, p.A, p.B, v.Next);
                else { v.Piece = LanePiece.Segment(city, p.B, p.C); v.Next = ChooseNext(p.B, p.C); }
            }
            PlaceOnPiece(v);
            v.Model.SteerAngle = Mathf.Clamp(v.Model.YawRate * 0.35f, -0.5f, 0.5f);
            v.WheelSpin += v.AiSpeed * dt / v.Spec.Wheel;
            v.Braking = want < v.AiSpeed - 0.3f || v.AiSpeed < 0.2f;
            v.Roll = Mathx.Damp(v.Roll, Mathx.Clamp(v.AiSpeed * v.Model.YawRate * 0.012f, -0.06f, 0.06f), 6, dt);
            v.Height = 0;
            v.Sync(); SetBrakeLights(v);
        }

        private void UpdatePolice(Vehicle v, float dt, float time)
        {
            var tgt = Player.Position; float d = Vector3.Distance(new Vector3(tgt.x, 0, tgt.z), new Vector3(v.Position.x, 0, v.Position.z));
            v.Repath -= dt;
            if (v.Repath <= 0) { v.Repath = 1; v.Path = city.Path(city.NearestNode(v.Position.x, v.Position.z), city.NearestNode(tgt.x, tgt.z)); }
            Vector2 aim;
            if (d < 32 || v.Path.Count <= 1)
            {
                var pv = Player.Car != null ? Player.Car.Model.Velocity : new Vec2(0, 0);
                aim = new Vector2(tgt.x + pv.X * 0.45f, tgt.z + pv.Y * 0.45f);
            }
            else
            {
                int k = 0; var me = new Vector2(v.Position.x, v.Position.z);
                while (k < v.Path.Count - 1 && Vector2.Distance(ToV2(city.NodePos(v.Path[k].Item1, v.Path[k].Item2)), me) < 9) k++;
                aim = ToV2(city.NodePos(v.Path[k].Item1, v.Path[k].Item2));
            }
            float want = Mathf.Atan2(aim.x - v.Position.x, aim.y - v.Position.z), diff = Mathx.AngleDiff(v.Model.Yaw, want), sp = v.Speed;
            var input = new VehicleInput();
            if (v.Reverse > 0) { v.Reverse -= dt; input.Throttle = -1; input.Steer = -Mathf.Sign(diff); }
            else
            {
                bool onFootClose = d < 7 && Player.Car == null;
                input.Steer = Mathf.Clamp(diff * 2.2f, -1, 1);
                input.Throttle = onFootClose ? 0 : Mathf.Abs(diff) > 1.2f ? 0.45f : 1;
                input.Brake = onFootClose ? 1 : 0;
                input.Handbrake = Mathf.Abs(diff) > 1.0f && sp > 14;
                v.Stuck = sp < 1.2f && input.Throttle > 0 ? v.Stuck + dt : 0;
                if (v.Stuck > 1.4f) { v.Reverse = 1.3f; v.Stuck = 0; }
            }
            v.Input = input;
            v.Step(dt, city);
            SetSiren(v, true, Mathf.FloorToInt(time * 6) % 2 == 0);
            SetBrakeLights(v);
        }

        private static Vector2 ToV2(Vec2 v) { return new Vector2(v.X, v.Y); }

        private void SetSiren(Vehicle v, bool on, bool redPhase)
        {
            HarborMaterials.SetEmission(v.Parts.SirenRed.material, new Color(1, 0.05f, 0.05f) * (on && redPhase ? 8f : 0.3f));
            HarborMaterials.SetEmission(v.Parts.SirenBlue.material, new Color(0.1f, 0.2f, 1f) * (on && !redPhase ? 9f : 0.3f));
        }

        /// <summary>Swaps tail-light material when braking.</summary>
        public void SetBrakeLights(Vehicle v)
        {
            var m = v.Braking ? mats.BrakeLight : mats.TailLight;
            if (v.Parts.Tails[0].sharedMaterial != m) { v.Parts.Tails[0].sharedMaterial = m; v.Parts.Tails[1].sharedMaterial = m; }
        }

        // ------------------------------------------------------------------ pedestrians
        private void UpdatePed(Pedestrian p, float dt)
        {
            float perim = city.SidewalkPerimeter(p.Block);
            if (p.Mode == Pedestrian.State.Walk || p.Mode == Pedestrian.State.Flee)
            {
                float sp = p.Mode == Pedestrian.State.Flee ? cfg.PedFleeSpeed : p.Speed;
                if (p.Mode == Pedestrian.State.Flee)
                {
                    if ((p.Timer -= dt) <= 0) p.Mode = Pedestrian.State.Walk;
                    var a = city.SidewalkPoint(p.Block, p.T);
                    p.Dir = (p.Threat.x - a.x) * a.dx + (p.Threat.z - a.z) * a.dz > 0 ? -1 : 1;
                }
                p.T += p.Dir * sp * dt / perim;
                var q = city.SidewalkPoint(p.Block, p.T);
                p.Pos.x = Mathx.Damp(p.Pos.x, q.x, 10, dt); p.Pos.z = Mathx.Damp(p.Pos.z, q.z, 10, dt); p.Pos.y = 0.18f;
                p.Yaw = Mathf.Atan2(q.dx * p.Dir, q.dz * p.Dir);
                p.Rig.Root.rotation = Quaternion.Euler(0, p.Yaw * Mathf.Rad2Deg, 0);
                p.Rig.Animate(sp, dt, false);
            }
            else if (p.Mode == Pedestrian.State.Air)
            {
                p.Vel.y -= 18 * dt; p.Pos += p.Vel * dt;
                float x = p.Pos.x, z = p.Pos.z;
                if (city.CollideCircle(ref x, ref z, 0.4f, out _, out _)) { p.Vel.x *= -0.3f; p.Vel.z *= -0.3f; p.Pos.x = x; p.Pos.z = z; }
                p.Rig.Root.rotation *= Quaternion.Euler(p.Spin * dt * Mathf.Rad2Deg, 0, p.Spin * 0.6f * dt * Mathf.Rad2Deg);
                float g = city.GroundHeight(p.Pos.x, p.Pos.z);
                if (p.Pos.y <= g + 0.2f && p.Vel.y < 0) { p.Mode = Pedestrian.State.Down; p.Pos.y = g + 0.12f; p.Rig.Root.rotation = Quaternion.Euler(-90, p.Yaw * Mathf.Rad2Deg + R(-60, 60), 0); }
                p.Rig.Animate(0, dt, true);
            }
            else if (p.Mode == Pedestrian.State.Down && (p.Timer -= dt) < 0) p.Mode = Pedestrian.State.Gone;
            p.Rig.Root.position = p.Pos;
        }

        /// <summary>Knocks a pedestrian flying.</summary>
        public void Knock(Pedestrian p, float vx, float vz, bool byPlayer)
        {
            if (p.Mode == Pedestrian.State.Down || p.Mode == Pedestrian.State.Air) return;
            p.Mode = Pedestrian.State.Air; p.Timer = 12;
            p.Vel = new Vector3(vx * 1.05f + R(-1, 1), 3 + Mathf.Sqrt(vx * vx + vz * vz) * 0.22f, vz * 1.05f + R(-1, 1));
            p.Spin = R(-8, 8);
            Crash?.Invoke(0.5f, byPlayer);
            if (byPlayer) PedHitByPlayer?.Invoke();
            foreach (var o in Peds) if (o != p && o.Mode == Pedestrian.State.Walk && Vector3.Distance(o.Pos, p.Pos) < 25) { o.Mode = Pedestrian.State.Flee; o.Timer = 5; o.Threat = p.Pos; }
        }

        // ------------------------------------------------------------------ contacts
        private void ResolveContacts()
        {
            for (int a = 0; a < Vehicles.Count; a++)
                for (int b = a + 1; b < Vehicles.Count; b++)
                {
                    var A = Vehicles[a]; var B = Vehicles[b];
                    if (Mathf.Abs(A.Model.Position.X - B.Model.Position.X) > 8 || Mathf.Abs(A.Model.Position.Y - B.Model.Position.Y) > 8) continue;
                    var fa = A.Model.Forward; var fb = B.Model.Forward;
                    bool hit = false; float hnx = 0, hnz = 0, pen = 0;
                    for (int i = 0; i < 2; i++)
                        for (int j = 0; j < 2; j++)
                        {
                            float oa = (i == 0 ? 1 : -1) * A.Spec.Length * 0.28f, ob = (j == 0 ? 1 : -1) * B.Spec.Length * 0.28f;
                            float ax = A.Model.Position.X + fa.X * oa, az = A.Model.Position.Y + fa.Y * oa, bx = B.Model.Position.X + fb.X * ob, bz = B.Model.Position.Y + fb.Y * ob;
                            float dx = bx - ax, dz = bz - az, d = Mathf.Sqrt(dx * dx + dz * dz), R0 = A.Radius + B.Radius - 0.15f;
                            if (d < R0 && d > 1e-4f && R0 - d > pen) { hit = true; hnx = dx / d; hnz = dz / d; pen = R0 - d; }
                        }
                    if (!hit) continue;
                    foreach (var v in new[] { A, B }) if (v.Mode == VehicleMode.Traffic) v.Mode = VehicleMode.Wreck;
                    float ma = A.Spec.Mass, mb = B.Spec.Mass, tot = ma + mb;
                    A.Model.Position = new Vec2(A.Model.Position.X - hnx * pen * mb / tot, A.Model.Position.Y - hnz * pen * mb / tot);
                    B.Model.Position = new Vec2(B.Model.Position.X + hnx * pen * ma / tot, B.Model.Position.Y + hnz * pen * ma / tot);
                    float rv = (B.Model.Velocity.X - A.Model.Velocity.X) * hnx + (B.Model.Velocity.Y - A.Model.Velocity.Y) * hnz;
                    if (rv < 0)
                    {
                        float jimp = -1.3f * rv / (1 / ma + 1 / mb);
                        A.Model.Velocity = new Vec2(A.Model.Velocity.X - jimp * hnx / ma, A.Model.Velocity.Y - jimp * hnz / ma);
                        B.Model.Velocity = new Vec2(B.Model.Velocity.X + jimp * hnx / mb, B.Model.Velocity.Y + jimp * hnz / mb);
                        A.Model.YawRate += R(-1, 1) * -rv * 0.08f; B.Model.YawRate += R(-1, 1) * -rv * 0.08f;
                        if (-rv > 4)
                        {
                            bool involvesPlayer = A == Player.Car || B == Player.Car;
                            Crash?.Invoke(Mathf.Min(1, -rv / 20), involvesPlayer);
                            if (involvesPlayer)
                            {
                                Player.Car.Health -= -rv * 1.4f;
                                var other = A == Player.Car ? B : A;
                                if (other.Spec.Type == CarType.Police && -rv > 6) CopRammed?.Invoke(-rv);
                            }
                        }
                    }
                    A.Sync(); B.Sync();
                }

            foreach (var v in Vehicles)
            {
                float sp = v.Speed; if (sp < 1.5f) continue;
                var f = v.Model.Forward;
                foreach (var p in Peds)
                {
                    if (p.Mode != Pedestrian.State.Walk && p.Mode != Pedestrian.State.Flee) continue;
                    float dx = p.Pos.x - v.Model.Position.X, dz = p.Pos.z - v.Model.Position.Y;
                    if (Mathf.Abs(dx) > 4 || Mathf.Abs(dz) > 4) continue;
                    float along = dx * f.X + dz * f.Y, lat = Mathf.Abs(dx * f.Y - dz * f.X);
                    if (Mathf.Abs(along) < v.Spec.Length / 2 + 0.3f && lat < v.Spec.Width / 2 + 0.3f)
                    {
                        if (sp > cfg.PedHitSpeed) Knock(p, v.Model.Velocity.X, v.Model.Velocity.Y, v == Player.Car);
                        else { p.Mode = Pedestrian.State.Flee; p.Timer = 3; p.Threat = v.Position; }
                    }
                }
                if (Player.Car == null) Player.CheckHitBy(v);
            }
        }
    }
}
