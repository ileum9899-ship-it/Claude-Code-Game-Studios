using NeonHarbor.Core;
using UnityEngine;

namespace NeonHarbor.Runtime
{
    /// <summary>What is controlling a vehicle.</summary>
    public enum VehicleMode { Parked, Traffic, Wreck, Police, Player }

    /// <summary>
    /// One car in the world: the engine-free <see cref="VehicleModel"/> plus its visuals,
    /// collision against the city, and per-mode AI state. Updated by <see cref="WorldDirector"/>.
    /// </summary>
    public sealed class Vehicle
    {
        public readonly CarSpec Spec;
        public readonly VehicleModel Model;
        public readonly VehicleFactory.Parts Parts;
        public VehicleMode Mode;
        public VehicleInput Input;
        public float Health = 100f, Height, Roll, Pitch, WheelSpin;
        public bool Braking;
        /// <summary>Seconds until a burning car explodes (0 = not burning).</summary>
        public float Burning;
        public bool BurnByPlayer, Exploded, Unloaded;
        public HumanRig Driver;

        // traffic AI
        public LanePiece Piece; public float S, AiSpeed, Want, Honk; public (int, int) Next;
        // police AI
        public System.Collections.Generic.List<(int, int)> Path = new System.Collections.Generic.List<(int, int)>();
        public float Repath, Stuck, Reverse;

        /// <summary>Radius of each of the two collision circles.</summary>
        public float Radius => Mathf.Max(1.1f, Spec.Width * 0.62f);
        /// <summary>World position (y = ride height).</summary>
        public Vector3 Position => new Vector3(Model.Position.X, Height, Model.Position.Y);
        /// <summary>Forward on the ground.</summary>
        public Vector3 Forward { get { var f = Model.Forward; return new Vector3(f.X, 0, f.Y); } }
        /// <summary>Speed magnitude (m/s).</summary>
        public float Speed => Model.Speed;

        /// <summary>Spawns a car.</summary>
        public Vehicle(CarType type, Color paint, HarborConfig cfg, HarborMaterials mats, Transform parent)
        {
            Spec = VehicleFactory.Specs[type];
            Model = new VehicleModel(cfg) { Mass = Spec.Mass, SpeedFactor = type == CarType.Police ? cfg.CopSpeed * 0.95f : 1f };
            Parts = VehicleFactory.Build(type, paint, mats, parent);
        }

        /// <summary>Physics step with city collisions. Returns the wall impact speed.</summary>
        public float Step(float dt, CityLayout city)
        {
            Model.Step(Input, dt);
            float impact = 0;
            var f = Model.Forward;
            for (int side = 0; side < 2; side++)
            {
                float off = (side == 0 ? 1 : -1) * Spec.Length * 0.3f;
                float px = Model.Position.X + f.X * off, pz = Model.Position.Y + f.Y * off, ox = px, oz = pz;
                if (city.CollideCircle(ref px, ref pz, Radius, out float nx, out float nz))
                {
                    Model.Position = new Vec2(Model.Position.X + px - ox, Model.Position.Y + pz - oz);
                    impact = Mathf.Max(impact, Model.Bounce(nx, nz));
                }
            }
            if (impact > 3) Health -= impact * 1.2f;
            float ground = city.GroundHeight(Model.Position.X, Model.Position.Y) > 0.1f ? 0.18f : 0f;
            Height = Mathx.Damp(Height, ground, 18, dt);
            float vF = Model.ForwardSpeed;
            Roll = Mathx.Damp(Roll, Mathx.Clamp(vF * Model.YawRate * 0.012f, -0.09f, 0.09f), 6, dt);
            Pitch = Mathx.Damp(Pitch, Mathx.Clamp(-Model.LastAccel * 0.006f, -0.06f, 0.06f), 6, dt);
            WheelSpin += vF * dt / Spec.Wheel;
            Braking = Input.Brake > 0.1f || (Input.Throttle < 0 && vF > 0.5f) || Input.Handbrake;
            Sync();
            return impact;
        }

        /// <summary>Copies the model state onto the transforms.</summary>
        public void Sync()
        {
            Parts.Root.SetPositionAndRotation(Position, Quaternion.Euler(0, Model.Yaw * Mathf.Rad2Deg, 0));
            Parts.Body.localRotation = Quaternion.Euler(Pitch * Mathf.Rad2Deg, 0, Roll * Mathf.Rad2Deg);
            for (int k = 0; k < 4; k++)
            {
                Parts.WheelSpins[k].localRotation = Quaternion.Euler(WheelSpin * Mathf.Rad2Deg, 0, 0);
                Parts.WheelPivots[k].localRotation = Quaternion.Euler(0, k < 2 ? Model.SteerAngle * Mathf.Rad2Deg : 0, 0);
            }
        }

        /// <summary>Puts the car at a place and heading, at rest.</summary>
        public void Place(Vector3 pos, float yaw)
        {
            Model.Position = new Vec2(pos.x, pos.z); Model.Yaw = yaw; Model.Velocity = new Vec2(0, 0); Height = pos.y; Sync();
        }
    }

    /// <summary>
    /// One piece of a traffic car's route: a straight lane segment between two
    /// intersections, or a quadratic curve through an intersection. Drives on the right.
    /// </summary>
    public sealed class LanePiece
    {
        public const float Lane = 3f;
        public bool IsTurn, Straight; public (int, int) A, B, C; public Vec2 P0, P1, P2, F; public float Length; public int Axis;

        private static Vec2 Right(Vec2 f) { return new Vec2(f.Y, -f.X); } // right of travel in Unity's left-handed frame

        /// <summary>Lane segment from intersection a to b.</summary>
        public static LanePiece Segment(CityLayout c, (int, int) a, (int, int) b)
        {
            Vec2 A = c.NodePos(a.Item1, a.Item2), B = c.NodePos(b.Item1, b.Item2), f = (B - A).Normalized, r = Right(f);
            var p = new LanePiece { A = a, B = b, F = f, P0 = A + f * c.HalfRoad + r * Lane, P2 = B - f * c.HalfRoad + r * Lane, Axis = Mathf.Abs(f.X) > 0.5f ? 0 : 1 };
            p.P1 = (p.P0 + p.P2) * 0.5f; p.Length = Vec2.Distance(p.P0, p.P2);
            return p;
        }

        /// <summary>Curve through intersection b, from road a→b into road b→c.</summary>
        public static LanePiece Turn(CityLayout c, (int, int) a, (int, int) b, (int, int) cc)
        {
            Vec2 A = c.NodePos(a.Item1, a.Item2), B = c.NodePos(b.Item1, b.Item2), Cn = c.NodePos(cc.Item1, cc.Item2);
            Vec2 f1 = (B - A).Normalized, f2 = (Cn - B).Normalized, r1 = Right(f1), r2 = Right(f2);
            var p = new LanePiece { IsTurn = true, A = a, B = b, C = cc, P0 = B - f1 * c.HalfRoad + r1 * Lane, P2 = B + f2 * c.HalfRoad + r2 * Lane };
            p.Straight = Mathf.Abs(f1.Dot(f2)) > 0.9f;
            p.P1 = p.Straight ? (p.P0 + p.P2) * 0.5f : B + r1 * Lane + r2 * Lane;
            float len = 0; Vec2 prev = p.P0;
            for (int k = 1; k <= 8; k++) { var q = p.Eval(k / 8f); len += Vec2.Distance(q, prev); prev = q; }
            p.Length = len;
            return p;
        }

        /// <summary>Point at t ∈ [0, 1].</summary>
        public Vec2 Eval(float t) { float u = 1 - t; return P0 * (u * u) + P1 * (2 * u * t) + P2 * (t * t); }
        /// <summary>Tangent at t.</summary>
        public Vec2 Tangent(float t) { return (P1 - P0) * (2 * (1 - t)) + (P2 - P1) * (2 * t); }
    }
}
