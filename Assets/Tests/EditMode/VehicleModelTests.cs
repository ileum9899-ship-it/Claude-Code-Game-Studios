using NeonHarbor.Core;
using NUnit.Framework;

namespace NeonHarbor.Tests
{
    /// <summary>Arcade car model (design/gdd/neon-harbor.md § Driving).</summary>
    public class VehicleModelTests
    {
        private const float Dt = 1f / 60f;
        private static VehicleModel NewCar() { return new VehicleModel(new HarborConfig()); }
        private static void Run(VehicleModel car, VehicleInput input, float seconds) { for (float t = 0; t < seconds; t += Dt) car.Step(input, Dt); }

        [Test]
        public void Throttle_AcceleratesForward_BelowTopSpeed()
        {
            var car = NewCar();

            Run(car, new VehicleInput { Throttle = 1 }, 30f);

            Assert.Greater(car.ForwardSpeed, car.TopSpeed * 0.6f);
            Assert.LessOrEqual(car.ForwardSpeed, car.TopSpeed);
            Assert.Greater(car.Position.Y, 100f); // yaw 0 drives toward +z
        }

        [Test]
        public void Brake_StopsTheCar()
        {
            var car = NewCar();
            Run(car, new VehicleInput { Throttle = 1 }, 5f);

            Run(car, new VehicleInput { Brake = 1 }, 4f);

            Assert.Less(car.Speed, 0.1f);
        }

        [Test]
        public void PositiveSteer_IncreasesYaw_TowardPlusX()
        {
            var car = NewCar();
            Run(car, new VehicleInput { Throttle = 1 }, 2f);

            Run(car, new VehicleInput { Throttle = 0.5f, Steer = 1 }, 1f);

            Assert.Greater(car.Yaw, 0.3f);
        }

        [Test]
        public void Handbrake_LetsTheCarSlide()
        {
            var grip = NewCar(); var drift = NewCar();
            Run(grip, new VehicleInput { Throttle = 1 }, 4f); Run(drift, new VehicleInput { Throttle = 1 }, 4f);

            float maxGripSlip = 0, maxDriftSlip = 0;
            for (int k = 0; k < 40; k++)
            {
                grip.Step(new VehicleInput { Steer = 1 }, Dt); drift.Step(new VehicleInput { Steer = 1, Handbrake = true }, Dt);
                maxGripSlip = System.Math.Max(maxGripSlip, grip.Slip); maxDriftSlip = System.Math.Max(maxDriftSlip, drift.Slip);
            }

            Assert.Greater(maxDriftSlip, maxGripSlip);
        }

        [Test]
        public void Reverse_IsCapped()
        {
            var car = NewCar();

            Run(car, new VehicleInput { Throttle = -1 }, 10f);

            Assert.Less(car.ForwardSpeed, 0);
            Assert.GreaterOrEqual(car.ForwardSpeed, -new HarborConfig().CarMaxReverse - 1f);
        }

        [Test]
        public void Bounce_ReflectsIntoWallVelocity_AndReportsImpact()
        {
            var car = NewCar(); car.Velocity = new Vec2(0, 20);

            float impact = car.Bounce(0, -1);

            Assert.AreEqual(20f, impact, 0.001f);
            Assert.Less(car.Velocity.Y, 0);
        }

        [Test]
        public void HeavierCar_HasLowerTopSpeed()
        {
            var light = NewCar(); var heavy = NewCar(); heavy.Mass = 1.6f;

            Assert.Less(heavy.TopSpeed, light.TopSpeed);
        }
    }
}
