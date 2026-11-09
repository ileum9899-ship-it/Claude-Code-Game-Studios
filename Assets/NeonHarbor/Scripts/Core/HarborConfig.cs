using System;

namespace NeonHarbor.Core
{
    /// <summary>
    /// All tunable gameplay values. Defaults match <c>Resources/Data/harbor_balance.json</c>
    /// (and the browser build's CFG). See design/gdd/neon-harbor.md § Tuning Knobs.
    /// </summary>
    public sealed class HarborConfig
    {
        // city
        /// <summary>Blocks per side.</summary>
        public int Blocks = 8;
        /// <summary>Block edge length (m), sidewalk included.</summary>
        public float BlockSize = 48f;
        /// <summary>Road width (m), curb to curb.</summary>
        public float RoadWidth = 12f;
        /// <summary>Sidewalk depth (m).</summary>
        public float Sidewalk = 3.5f;
        /// <summary>City generator seed.</summary>
        public int Seed = 1337;

        // player on foot
        /// <summary>Walk speed (m/s).</summary>
        public float WalkSpeed = 2.4f;
        /// <summary>Run speed (m/s).</summary>
        public float RunSpeed = 6.2f;
        /// <summary>Jump take-off speed (m/s).</summary>
        public float JumpSpeed = 5.2f;
        /// <summary>Gravity (m/s²).</summary>
        public float Gravity = 18f;

        // vehicles
        /// <summary>Engine acceleration (m/s²).</summary>
        public float CarAccel = 13f;
        /// <summary>Reverse acceleration (m/s²).</summary>
        public float CarReverseAccel = 7f;
        /// <summary>Brake deceleration (m/s²).</summary>
        public float CarBrake = 26f;
        /// <summary>Top speed (m/s) for a mass-1 car.</summary>
        public float CarMaxSpeed = 52f;
        /// <summary>Top reverse speed (m/s).</summary>
        public float CarMaxReverse = 11f;
        /// <summary>Quadratic air drag coefficient.</summary>
        public float CarDrag = 0.0042f;
        /// <summary>Rolling resistance (m/s²) while coasting.</summary>
        public float CarRolling = 1.3f;
        /// <summary>Lateral grip rate (1/s).</summary>
        public float CarGrip = 9.5f;
        /// <summary>Lateral grip with the handbrake on (1/s).</summary>
        public float CarHandbrakeGrip = 1.4f;
        /// <summary>Max front-wheel angle (rad).</summary>
        public float CarSteerMax = 0.6f;
        /// <summary>Steering response rate.</summary>
        public float CarSteerSpeed = 3.2f;
        /// <summary>Wheelbase (m).</summary>
        public float CarWheelbase = 2.7f;

        // traffic
        /// <summary>Moving traffic cars kept around the player.</summary>
        public int TrafficCount = 34;
        /// <summary>Parked cars placed at start.</summary>
        public int ParkedCount = 46;
        /// <summary>Traffic cruise speed (m/s).</summary>
        public float TrafficCruise = 12f;
        /// <summary>Traffic speed through turns (m/s).</summary>
        public float TrafficTurnSpeed = 6f;
        /// <summary>Traffic following gap (m).</summary>
        public float FollowGap = 9f;
        /// <summary>Full traffic-light cycle (s).</summary>
        public float LightCycle = 18f;
        /// <summary>Yellow phase (s).</summary>
        public float LightYellow = 2.5f;

        // pedestrians
        /// <summary>Pedestrians kept around the player.</summary>
        public int PedCount = 46;
        /// <summary>Spawn radius (m).</summary>
        public float PedRadius = 70f;
        /// <summary>Walk speed (m/s).</summary>
        public float PedSpeed = 1.35f;
        /// <summary>Flee speed (m/s).</summary>
        public float PedFleeSpeed = 5.2f;
        /// <summary>Impact speed (m/s) that knocks a pedestrian down.</summary>
        public float PedHitSpeed = 4f;

        // wanted level
        /// <summary>Heat added for hitting a pedestrian.</summary>
        public float HeatPedHit = 1f;
        /// <summary>Heat added for taking an occupied car.</summary>
        public float HeatCarjack = 1f;
        /// <summary>Heat added for ramming a police car.</summary>
        public float HeatCopHit = 1f;
        /// <summary>Highest wanted level.</summary>
        public int MaxStars = 5;
        /// <summary>Distance (m) every cop must be beyond for the evade timer to run.</summary>
        public float EvadeRadius = 85f;
        /// <summary>Seconds out of reach to lose the wanted level.</summary>
        public float EvadeSeconds = 9f;
        /// <summary>Distance (m) at which a nearly stopped player is being arrested.</summary>
        public float BustRadius = 7f;
        /// <summary>Seconds needed to complete an arrest.</summary>
        public float BustSeconds = 2.5f;
        /// <summary>Fraction of money lost when arrested.</summary>
        public float BustFine = 0.1f;
        /// <summary>Police top-speed multiplier.</summary>
        public float CopSpeed = 1.25f;
        /// <summary>Police cars per star.</summary>
        public int CopsPerStar = 1;
        /// <summary>Police cars at most.</summary>
        public int MaxCops = 5;

        // missions & money
        /// <summary>Courier reward range ($).</summary>
        public int CourierRewardMin = 250, CourierRewardMax = 900;
        /// <summary>Street-race reward ($).</summary>
        public int RaceReward = 1500;
        /// <summary>Evade-mission reward ($).</summary>
        public int EvadeReward = 1200;
        /// <summary>Courier time allowed per metre of straight-line distance (s).</summary>
        public float TimePerMeter = 0.16f;
        /// <summary>Courier base time (s).</summary>
        public float TimeBase = 25f;
        /// <summary>Cash pickups in the city.</summary>
        public int CashPickups = 24;
        /// <summary>Cash pickup value range ($).</summary>
        public int CashMin = 40, CashMax = 160;
        /// <summary>Starting money ($).</summary>
        public int StartMoney = 500;

        /// <summary>Real seconds per in-game day.</summary>
        public float DayLength = 720f;
    }

    /// <summary>
    /// Wire format of <c>harbor_balance.json</c> (snake_case, see .claude/rules/data-files.md).
    /// Absent / zero fields keep the defaults in <see cref="HarborConfig"/>.
    /// </summary>
    [Serializable]
    public sealed class HarborConfigDto
    {
#pragma warning disable 0649
        public int blocks; public float block_size, road_width, sidewalk; public int seed;
        public float walk_speed, run_speed, jump_speed, gravity;
        public float car_accel, car_reverse_accel, car_brake, car_max_speed, car_max_reverse, car_drag, car_rolling, car_grip, car_handbrake_grip, car_steer_max, car_steer_speed, car_wheelbase;
        public int traffic_count, parked_count; public float traffic_cruise, traffic_turn_speed, follow_gap, light_cycle, light_yellow;
        public int ped_count; public float ped_radius, ped_speed, ped_flee_speed, ped_hit_speed;
        public float heat_ped_hit, heat_carjack, heat_cop_hit; public int max_stars; public float evade_radius, evade_seconds, bust_radius, bust_seconds, bust_fine, cop_speed; public int cops_per_star, max_cops;
        public int courier_reward_min, courier_reward_max, race_reward, evade_reward; public float time_per_meter, time_base; public int cash_pickups, cash_min, cash_max, start_money;
        public float day_length;
#pragma warning restore 0649

        /// <summary>Maps the file onto defaults.</summary>
        public HarborConfig ToConfig()
        {
            var c = new HarborConfig();
            I(ref c.Blocks, blocks); F(ref c.BlockSize, block_size); F(ref c.RoadWidth, road_width); F(ref c.Sidewalk, sidewalk); I(ref c.Seed, seed);
            F(ref c.WalkSpeed, walk_speed); F(ref c.RunSpeed, run_speed); F(ref c.JumpSpeed, jump_speed); F(ref c.Gravity, gravity);
            F(ref c.CarAccel, car_accel); F(ref c.CarReverseAccel, car_reverse_accel); F(ref c.CarBrake, car_brake); F(ref c.CarMaxSpeed, car_max_speed);
            F(ref c.CarMaxReverse, car_max_reverse); F(ref c.CarDrag, car_drag); F(ref c.CarRolling, car_rolling); F(ref c.CarGrip, car_grip);
            F(ref c.CarHandbrakeGrip, car_handbrake_grip); F(ref c.CarSteerMax, car_steer_max); F(ref c.CarSteerSpeed, car_steer_speed); F(ref c.CarWheelbase, car_wheelbase);
            I(ref c.TrafficCount, traffic_count); I(ref c.ParkedCount, parked_count); F(ref c.TrafficCruise, traffic_cruise); F(ref c.TrafficTurnSpeed, traffic_turn_speed);
            F(ref c.FollowGap, follow_gap); F(ref c.LightCycle, light_cycle); F(ref c.LightYellow, light_yellow);
            I(ref c.PedCount, ped_count); F(ref c.PedRadius, ped_radius); F(ref c.PedSpeed, ped_speed); F(ref c.PedFleeSpeed, ped_flee_speed); F(ref c.PedHitSpeed, ped_hit_speed);
            F(ref c.HeatPedHit, heat_ped_hit); F(ref c.HeatCarjack, heat_carjack); F(ref c.HeatCopHit, heat_cop_hit); I(ref c.MaxStars, max_stars);
            F(ref c.EvadeRadius, evade_radius); F(ref c.EvadeSeconds, evade_seconds); F(ref c.BustRadius, bust_radius); F(ref c.BustSeconds, bust_seconds);
            F(ref c.BustFine, bust_fine); F(ref c.CopSpeed, cop_speed); I(ref c.CopsPerStar, cops_per_star); I(ref c.MaxCops, max_cops);
            I(ref c.CourierRewardMin, courier_reward_min); I(ref c.CourierRewardMax, courier_reward_max); I(ref c.RaceReward, race_reward); I(ref c.EvadeReward, evade_reward);
            F(ref c.TimePerMeter, time_per_meter); F(ref c.TimeBase, time_base); I(ref c.CashPickups, cash_pickups); I(ref c.CashMin, cash_min); I(ref c.CashMax, cash_max);
            I(ref c.StartMoney, start_money); F(ref c.DayLength, day_length);
            return c;
        }

        private static void I(ref int target, int v) { if (v > 0) target = v; }
        private static void F(ref float target, float v) { if (v > 0f) target = v; }
    }
}
