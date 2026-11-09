using NeonHarbor.Core;
using NUnit.Framework;

namespace NeonHarbor.Tests
{
    /// <summary>Wanted level, traffic lights and missions (design/gdd/neon-harbor.md).</summary>
    public class WantedAndMissionTests
    {
        private static HarborConfig Cfg() { return new HarborConfig(); }

        [Test]
        public void Heat_RaisesStars_AndCapsAtMax()
        {
            var w = new WantedSystem(Cfg()); int raised = 0;
            w.StarsRaised += (s, why) => raised = s;

            w.AddHeat(1, "a"); w.AddHeat(1, "b");
            Assert.AreEqual(2, w.Stars); Assert.AreEqual(2, raised);
            w.AddHeat(50, "c");
            Assert.AreEqual(Cfg().MaxStars, w.Stars);
        }

        [Test]
        public void StayingAway_ClearsStars()
        {
            var c = Cfg(); var w = new WantedSystem(c); bool evaded = false;
            w.Evaded += () => evaded = true;
            w.AddHeat(2, "x");

            for (float t = 0; t < c.EvadeSeconds + 0.5f; t += 0.1f) w.Tick(0.1f, c.EvadeRadius + 10, 20);

            Assert.IsTrue(evaded); Assert.AreEqual(0, w.Stars);
        }

        [Test]
        public void CopNearby_ResetsEvadeTimer()
        {
            var c = Cfg(); var w = new WantedSystem(c);
            w.AddHeat(1, "x");
            w.Tick(c.EvadeSeconds - 1, c.EvadeRadius + 10, 20);

            w.Tick(0.1f, 20, 20);

            Assert.AreEqual(0, w.EvadeTimer); Assert.AreEqual(1, w.Stars);
        }

        [Test]
        public void StoppedNextToCop_IsBusted_MovingIsNot()
        {
            var c = Cfg(); var w = new WantedSystem(c); bool busted = false;
            w.Busted += () => busted = true;
            w.AddHeat(1, "x");

            for (float t = 0; t < c.BustSeconds * 2; t += 0.1f) w.Tick(0.1f, 3, 15);
            Assert.IsFalse(busted);
            for (float t = 0; t < c.BustSeconds + 0.3f; t += 0.1f) w.Tick(0.1f, 3, 0.5f);
            Assert.IsTrue(busted);
        }

        [Test]
        public void DesiredCops_FollowsStars()
        {
            var w = new WantedSystem(Cfg());
            Assert.AreEqual(0, w.DesiredCops);
            w.AddHeat(3, "x");
            Assert.AreEqual(3, w.DesiredCops);
        }

        [Test]
        public void Lights_NeverGreenBothWays()
        {
            var l = new TrafficLights(18, 2.5f);
            for (float t = 0; t < 40; t += 0.25f)
            {
                var x = l.State(2, 3, 0, t); var z = l.State(2, 3, 1, t);
                Assert.IsFalse(x == Signal.Green && z == Signal.Green, $"t={t}");
                Assert.IsFalse(x != Signal.Red && z != Signal.Red, $"t={t}");
            }
        }

        [Test]
        public void Courier_SucceedsAtDropOff_PaysMoreForDistance()
        {
            var c = Cfg();
            var near = MissionRun.Courier(c, new Vec2(0, 0), new Vec2(0, 150));
            var far = MissionRun.Courier(c, new Vec2(0, 0), new Vec2(0, 330));

            Assert.Greater(far.Reward, near.Reward);
            Assert.AreEqual(MissionStatus.Running, far.Tick(0.1f, new Vec2(0, 0), 0));
            Assert.AreEqual(MissionStatus.Succeeded, far.Tick(0.1f, new Vec2(0, 329), 0));
        }

        [Test]
        public void Race_GoesThroughCheckpoints_InOrder()
        {
            var run = MissionRun.Race(Cfg(), new Vec2(0, 0), new[] { new Vec2(0, 50), new Vec2(50, 50) });

            Assert.AreEqual(MissionStatus.Running, run.Tick(0.1f, new Vec2(50, 50), 0)); // skipping ahead doesn't count
            Assert.AreEqual(MissionStatus.CheckpointReached, run.Tick(0.1f, new Vec2(0, 50), 0));
            Assert.AreEqual(MissionStatus.Succeeded, run.Tick(0.1f, new Vec2(50, 50), 0));
        }

        [Test]
        public void Mission_FailsWhenTimeRunsOut()
        {
            var run = MissionRun.Courier(Cfg(), new Vec2(0, 0), new Vec2(0, 200));

            Assert.AreEqual(MissionStatus.Failed, run.Tick(run.TimeLeft + 1, new Vec2(0, 0), 0));
        }

        [Test]
        public void Evade_OnlyCountsAfterArming()
        {
            var run = MissionRun.Evade(Cfg());
            run.NotifyEvaded();
            Assert.AreEqual(MissionStatus.Running, run.Tick(0.1f, new Vec2(0, 0), 0));

            run.Tick(0.1f, new Vec2(0, 0), 2);
            run.NotifyEvaded();

            Assert.AreEqual(MissionStatus.Succeeded, run.Tick(0.1f, new Vec2(0, 0), 0));
        }

        [Test]
        public void ConfigDto_KeepsDefaultsForMissingKeys()
        {
            var cfg = new HarborConfigDto { blocks = 6, car_grip = 12 }.ToConfig();

            Assert.AreEqual(6, cfg.Blocks); Assert.AreEqual(12f, cfg.CarGrip);
            Assert.AreEqual(new HarborConfig().CarAccel, cfg.CarAccel);
        }

        [Test]
        public void Rng_MatchesBrowserBuildSequence()
        {
            var r = new Rng(1337);
            // first value of mulberry32(1337) in JavaScript
            Assert.AreEqual(ExpectedFirst, r.Next(), 1e-6);
        }

        private const double ExpectedFirst = RngFixture.FirstOf1337;
    }

    /// <summary>Reference value computed with the browser build's mulberry32.</summary>
    internal static class RngFixture
    {
        public const double FirstOf1337 = 0.1844118325971067;
    }
}
