using System;

namespace NeonHarbor.Core
{
    /// <summary>Driver inputs for one step.</summary>
    public struct VehicleInput
    {
        /// <summary>-1 (reverse/brake) .. 1 (accelerate).</summary>
        public float Throttle;
        /// <summary>0..1 service brake.</summary>
        public float Brake;
        /// <summary>-1 .. 1; positive steer increases yaw (turns toward +x: a right turn in Unity's left-handed frame).</summary>
        public float Steer;
        /// <summary>Handbrake: low rear grip, lets the car drift.</summary>
        public bool Handbrake;
    }

    /// <summary>
    /// Arcade "bicycle" car model on the ground plane: engine/brake/drag along the
    /// heading, speed-sensitive steering, lateral grip that breaks into a drift.
    /// Collisions are handled by the caller. Same maths as the browser build's stepCar.
    /// </summary>
    /// <example><code>
    /// var car = new VehicleModel(cfg) { Position = new Vec2(10, 10) };
    /// car.Step(new VehicleInput { Throttle = 1 }, 0.016f);
    /// </code></example>
    public sealed class VehicleModel
    {
        private readonly HarborConfig cfg;

        /// <summary>Position (x, z).</summary>
        public Vec2 Position;
        /// <summary>Velocity (x, z) m/s.</summary>
        public Vec2 Velocity;
        /// <summary>Heading; forward = (sin yaw, cos yaw).</summary>
        public float Yaw;
        /// <summary>Yaw rate rad/s.</summary>
        public float YawRate;
        /// <summary>Current front-wheel angle (rad).</summary>
        public float SteerAngle;
        /// <summary>Mass factor (1 = sedan); heavier cars are slower.</summary>
        public float Mass = 1f;
        /// <summary>Top-speed multiplier (police use &gt; 1).</summary>
        public float SpeedFactor = 1f;
        /// <summary>Sideways slip speed (m/s) last step — drives tyre squeal.</summary>
        public float Slip { get; private set; }
        /// <summary>Longitudinal acceleration last step (m/s²) — drives body pitch.</summary>
        public float LastAccel { get; private set; }

        /// <summary>Creates a car.</summary>
        public VehicleModel(HarborConfig config) { cfg = config ?? throw new ArgumentNullException(nameof(config)); }

        /// <summary>Unit forward.</summary>
        public Vec2 Forward => Vec2.FromYaw(Yaw);
        /// <summary>Speed along the heading (negative when reversing).</summary>
        public float ForwardSpeed => Velocity.Dot(Forward);
        /// <summary>Speed magnitude.</summary>
        public float Speed => Velocity.Length;
        /// <summary>Top speed for this car.</summary>
        public float TopSpeed => cfg.CarMaxSpeed * SpeedFactor / (float)Math.Sqrt(Mass);

        /// <summary>Integrates one step.</summary>
        public void Step(VehicleInput input, float dt)
        {
            Vec2 f = Forward, r = new Vec2(f.Y, -f.X);
            float vF = Velocity.Dot(f), vR = Velocity.Dot(r);
            float maxF = TopSpeed;
            float acc = 0;
            if (input.Throttle > 0)
                acc += (vF < -0.5f ? cfg.CarBrake : cfg.CarAccel * (1 - Mathx.Clamp(vF / maxF, 0, 1) * 0.85f)) * input.Throttle * (SpeedFactor > 1 ? 1.15f : 1f);
            if (input.Throttle < 0)
                acc += (vF > 0.5f ? -cfg.CarBrake : -cfg.CarReverseAccel * (vF > -cfg.CarMaxReverse ? 1 : 0)) * -input.Throttle;
            if (input.Brake > 0) acc -= Mathx.Sign(vF) * Math.Min(Math.Abs(vF) / dt, cfg.CarBrake * input.Brake);
            acc -= cfg.CarDrag * vF * Math.Abs(vF) + Mathx.Sign(vF) * Math.Min(Math.Abs(vF) / dt, cfg.CarRolling * (input.Throttle == 0 ? 1f : 0.2f));
            if (input.Handbrake) acc -= Mathx.Sign(vF) * Math.Min(Math.Abs(vF) / dt, 6f);
            vF += acc * dt;
            LastAccel = acc;

            float steerTarget = Mathx.Clamp(input.Steer, -1, 1) * cfg.CarSteerMax * Mathx.Lerp(1f, 0.3f, Mathx.Clamp(Math.Abs(vF) / 40f, 0, 1));
            SteerAngle = Mathx.Damp(SteerAngle, steerTarget, cfg.CarSteerSpeed * 2, dt);
            float yawRate = vF / cfg.CarWheelbase * (float)Math.Tan(SteerAngle) * (input.Handbrake ? 1.45f : 1f);
            YawRate = Mathx.Damp(YawRate, yawRate, input.Handbrake ? 5f : 9f, dt);
            Yaw += YawRate * dt;

            float grip = input.Handbrake ? cfg.CarHandbrakeGrip : cfg.CarGrip * (1 - Mathx.Clamp((Math.Abs(vR) - 4) / 14f, 0, 0.6f));
            Slip = Math.Abs(vR);
            vR = Mathx.Damp(vR, 0, grip, dt);
            Vec2 nf = Forward, nr = new Vec2(nf.Y, -nf.X);
            Velocity = new Vec2(nf.X * vF + nr.X * vR, nf.Y * vF + nr.Y * vR);
            Position = Position + Velocity * dt;
        }

        /// <summary>
        /// Bounces off a wall with outward normal (nx, nz); returns the impact speed (m/s) into the wall.
        /// </summary>
        public float Bounce(float nx, float nz)
        {
            float vn = Velocity.X * nx + Velocity.Y * nz;
            if (vn >= 0) return 0;
            Velocity = new Vec2(Velocity.X - vn * 1.35f * nx, Velocity.Y - vn * 1.35f * nz) * 0.8f;
            YawRate *= 0.5f;
            return -vn;
        }
    }
}
