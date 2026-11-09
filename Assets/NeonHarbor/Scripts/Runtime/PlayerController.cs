using System;
using NeonHarbor.Core;
using UnityEngine;

namespace NeonHarbor.Runtime
{
    /// <summary>
    /// The player: walking/running/jumping on foot with city collisions, entering,
    /// stealing and leaving cars (F), and driving (WASD, Space handbrake, H horn).
    /// Uses the classic Input Manager.
    /// </summary>
    public sealed class PlayerController
    {
        private readonly HarborConfig cfg;
        private readonly CityLayout city;
        private readonly HarborMaterials mats;
        private readonly WorldDirector world;
        private readonly HumanRig rig;
        /// <summary>The on-foot body (for aim / punch poses and the gun model).</summary>
        public HumanRig Rig => rig;
        /// <summary>Weapons and ammo.</summary>
        public readonly Arsenal Arsenal = new Arsenal();
        /// <summary>Armour 0..100 (soaks two thirds of damage).</summary>
        public float Armor;
        /// <summary>Horizontal velocity on foot (m/s).</summary>
        public Vector3 Velocity => vel;
        /// <summary>True while knocked down.</summary>
        public bool IsDown => downTimer > 0;
        private HumanRig seatedRig;
        private Vector3 vel;
        private bool onGround = true;
        private float downTimer;

        /// <summary>Feet position (or the car position while driving).</summary>
        public Vector3 Position;
        /// <summary>Facing on foot (rad).</summary>
        public float Yaw;
        /// <summary>Car being driven, or null.</summary>
        public Vehicle Car { get; private set; }
        /// <summary>Health 0..100.</summary>
        public float Health = 100;
        /// <summary>Money ($).</summary>
        public int Money;

        /// <summary>Message for the HUD.</summary>
        public event Action<string> Toast;
        /// <summary>Stole an occupied car (police car = true).</summary>
        public event Action<bool> Carjacked;
        /// <summary>Wall impact while driving (strength 0..1).</summary>
        public event Action<float> CarImpact;
        /// <summary>Horn pressed.</summary>
        public event Action HornPressed;
        /// <summary>Hit by a car on foot.</summary>
        public event Action KnockedDown;

        /// <summary>Creates the player at a spawn point.</summary>
        public PlayerController(HarborConfig cfg, CityLayout city, HarborMaterials mats, WorldDirector world, Transform parent, Vector3 spawn)
        {
            this.cfg = cfg; this.city = city; this.mats = mats; this.world = world;
            rig = HumanRig.Create(mats, parent, HumanRig.PlayerLook);
            Position = spawn; Money = cfg.StartMoney;
        }

        /// <summary>Car the player could get into right now.</summary>
        public Vehicle NearestEnterable()
        {
            Vehicle best = null; float bd = 4.2f;
            foreach (var v in world.Vehicles)
            {
                if (v.Exploded) continue;
                float d = Vector2.Distance(new Vector2(v.Position.x, v.Position.z), new Vector2(Position.x, Position.z));
                if (d < bd && v.Speed < 6) { bd = d; best = v; }
            }
            return best;
        }

        /// <summary>Gets into a car (jacking it if someone is driving).</summary>
        public void Enter(Vehicle v)
        {
            if (v.Mode == VehicleMode.Traffic)
            {
                var blk = city.BlockAt(v.Position.x + 8, v.Position.z) ?? city.BlockAt(v.Position.x, v.Position.z + 8) ?? city.Blocks[0];
                var p = world.SpawnPed(blk); p.Mode = Pedestrian.State.Flee; p.Timer = 6; p.Threat = Position;
                Carjacked?.Invoke(false);
            }
            else if (v.Spec.Type == CarType.Police && v.Mode == VehicleMode.Police) Carjacked?.Invoke(true);
            world.RemoveDriver(v);
            v.Mode = VehicleMode.Player; v.Input = default;
            seatedRig = HumanRig.Create(mats, v.Parts.Seat, HumanRig.PlayerLook, false); seatedRig.Sit(); seatedRig.Root.localScale = Vector3.one * 0.95f;
            Car = v; rig.Root.gameObject.SetActive(false);
            Toast?.Invoke(v.Spec.Name + " 탑승");
        }

        /// <summary>Gets out if the car is slow enough.</summary>
        public void Exit()
        {
            if (Car == null || Car.Speed > 4) return;
            var f = Car.Model.Forward; var left = new Vector3(-f.Y, 0, f.X); // driver side (Unity: left = -right)
            Position = Car.Position + left * 2f; Position.y = 0.18f;
            float x = Position.x, z = Position.z; city.CollideCircle(ref x, ref z, 0.5f, out _, out _); Position.x = x; Position.z = z;
            Yaw = Car.Model.Yaw; rig.Root.gameObject.SetActive(true);
            if (seatedRig != null) { UnityEngine.Object.Destroy(seatedRig.Root.gameObject); seatedRig = null; }
            Car.Input = new VehicleInput { Brake = 1, Handbrake = true }; Car.Mode = VehicleMode.Parked;
            Car = null;
        }

        /// <summary>Moves the player back to a spawn point on foot (after arrest / knockout).</summary>
        public void Respawn(Vector3 spawn)
        {
            if (Car != null) { if (seatedRig != null) UnityEngine.Object.Destroy(seatedRig.Root.gameObject); seatedRig = null; Car.Mode = VehicleMode.Parked; Car = null; }
            rig.Root.gameObject.SetActive(true); rig.Root.rotation = Quaternion.identity;
            Position = spawn; vel = Vector3.zero; Health = 100; Armor = 0; downTimer = 0;
        }

        /// <summary>Applies damage through armour.</summary>
        public void Damage(float amount)
        {
            var v = new Vitals { Health = Health, Armor = Armor }; v.Damage(amount); Health = v.Health; Armor = v.Armor;
        }

        /// <summary>Throws the player (explosions).</summary>
        public void Knock(Vector3 velocity)
        {
            if (Car != null || downTimer > 0) return;
            vel = velocity; onGround = false; downTimer = 1.6f; KnockedDown?.Invoke();
        }

        /// <summary>Turns the on-foot body toward a yaw (aiming).</summary>
        public void FaceYaw(float yaw, float dt) { Yaw += Mathx.AngleDiff(Yaw, yaw) * (1 - Mathf.Exp(-18 * dt)); }

        /// <summary>Called by the world when a car may have hit the player on foot.</summary>
        public void CheckHitBy(Vehicle v)
        {
            var f = v.Model.Forward; float dx = Position.x - v.Model.Position.X, dz = Position.z - v.Model.Position.Y;
            float along = dx * f.X + dz * f.Y, lat = Mathf.Abs(dx * f.Y - dz * f.X);
            if (Mathf.Abs(along) >= v.Spec.Length / 2 + 0.35f || lat >= v.Spec.Width / 2 + 0.35f || Position.y > 1.2f) return;
            if (v.Speed > 5 && downTimer <= 0)
            {
                vel = new Vector3(v.Model.Velocity.X * 0.8f, 4 + v.Speed * 0.15f, v.Model.Velocity.Y * 0.8f); onGround = false; downTimer = 1.6f;
                Health -= v.Speed * 3.2f; KnockedDown?.Invoke();
            }
        }

        /// <summary>One frame of input and movement. <paramref name="cameraYaw"/> makes walking camera-relative.</summary>
        public void Tick(float dt, float cameraYaw)
        {
            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Return))
            {
                if (Car != null) Exit(); else { var c = NearestEnterable(); if (c != null) Enter(c); }
            }
            if (Car != null) { Drive(dt); return; }

            if (downTimer > 0)
            {
                downTimer -= dt; vel.y -= cfg.Gravity * dt; Position += vel * dt;
                float g0 = city.GroundHeight(Position.x, Position.z);
                if (Position.y <= g0) { Position.y = g0; vel *= 0.6f; vel.y = 0; }
                rig.Root.SetPositionAndRotation(Position, Quaternion.Euler(-90 * Mathf.Clamp01(downTimer * 2), Yaw * Mathf.Rad2Deg, 0));
                rig.Animate(0, dt, true);
                return;
            }
            float mx = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            float mz = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            bool run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float speed = Mathf.Abs(mx) + Mathf.Abs(mz) > 0 ? (run ? cfg.RunSpeed : cfg.WalkSpeed) : 0;
            Vector3 fwd = new Vector3(Mathf.Sin(cameraYaw), 0, Mathf.Cos(cameraYaw)), right = new Vector3(Mathf.Cos(cameraYaw), 0, -Mathf.Sin(cameraYaw));
            Vector3 dir = fwd * mz + right * mx;
            if (dir.sqrMagnitude > 1e-4f) { dir.Normalize(); Yaw += Mathx.AngleDiff(Yaw, Mathf.Atan2(dir.x, dir.z)) * (1 - Mathf.Exp(-14 * dt)); }
            vel.x = Mathx.Damp(vel.x, dir.x * speed, onGround ? 12 : 2, dt); vel.z = Mathx.Damp(vel.z, dir.z * speed, onGround ? 12 : 2, dt);
            if (onGround && Input.GetKeyDown(KeyCode.Space)) { vel.y = cfg.JumpSpeed; onGround = false; }
            vel.y -= cfg.Gravity * dt;
            Position += vel * dt;
            float px = Position.x, pz = Position.z; city.CollideCircle(ref px, ref pz, 0.4f, out _, out _); Position.x = px; Position.z = pz;
            foreach (var v in world.Vehicles) // cars are solid
            {
                var f = v.Model.Forward; float dx = Position.x - v.Model.Position.X, dz = Position.z - v.Model.Position.Y;
                float along = dx * f.X + dz * f.Y, lat = dx * f.Y - dz * f.X, hl = v.Spec.Length / 2 + 0.35f, hw = v.Spec.Width / 2 + 0.35f;
                if (Mathf.Abs(along) < hl && Mathf.Abs(lat) < hw && Position.y < 1.3f)
                {
                    float pa = hl - Mathf.Abs(along), pl = hw - Mathf.Abs(lat);
                    if (pa < pl) { Position.x += f.X * pa * Mathf.Sign(along); Position.z += f.Y * pa * Mathf.Sign(along); }
                    else { Position.x += f.Y * pl * Mathf.Sign(lat); Position.z += -f.X * pl * Mathf.Sign(lat); }
                }
            }
            float g = city.GroundHeight(Position.x, Position.z);
            if (g < -50) Health = 0; // into the harbour
            float step = Position.y - g;
            if (step <= 0.25f && vel.y <= 0) { Position.y = Mathx.Damp(Position.y, g, 30, dt); vel.y = 0; onGround = true; } else if (step > 0.3f) onGround = false;
            rig.Root.SetPositionAndRotation(Position, Quaternion.Euler(0, Yaw * Mathf.Rad2Deg, 0));
            rig.Animate(new Vector2(vel.x, vel.z).magnitude, dt, !onGround);
        }

        private void Drive(float dt)
        {
            var c = Car;
            float throttle = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            float steer = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            if (c.Health <= 0 && !c.Exploded && c.Burning <= 0) { c.Burning = 4.5f; c.BurnByPlayer = false; }
            if (c.Health <= 0) throttle *= 0.3f;
            c.Input = new VehicleInput { Throttle = throttle, Steer = steer, Handbrake = Input.GetKey(KeyCode.Space) };
            if (Input.GetKeyDown(KeyCode.H)) HornPressed?.Invoke();
            float impact = c.Step(dt, city);
            if (impact > 4) CarImpact?.Invoke(Mathf.Min(1, impact / 22));
            world.SetBrakeLights(c);
            Position = new Vector3(c.Position.x, 0.18f, c.Position.z);
        }
    }
}
