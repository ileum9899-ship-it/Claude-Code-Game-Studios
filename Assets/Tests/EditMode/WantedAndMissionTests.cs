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

        private static readonly Vec2 Origin = new Vec2(0, 0);

        [Test]
        public void LeavingTheSearchArea_Unseen_ClearsStars()
        {
            var c = Cfg(); var w = new WantedSystem(c); bool evaded = false;
            w.Evaded += () => evaded = true;
            w.AddHeat(2, "x");
            w.Tick(0.1f, true, Origin, 10, 50); // seen at the origin
            var far = new Vec2(w.SearchRadius + 20, 0);

            for (float t = 0; t < c.EvadeSeconds + 0.5f; t += 0.1f) w.Tick(0.1f, false, far, 10, 200);

            Assert.IsTrue(evaded); Assert.AreEqual(0, w.Stars);
        }

        [Test]
        public void HidingInsideTheSearchArea_DoesNotClearStars()
        {
            var c = Cfg(); var w = new WantedSystem(c);
            w.AddHeat(1, "x"); w.Tick(0.1f, true, Origin, 0, 50);

            for (float t = 0; t < c.EvadeSeconds * 3; t += 0.1f) w.Tick(0.1f, false, new Vec2(10, 0), 0, 200);

            Assert.IsTrue(w.Searching); Assert.AreEqual(1, w.Stars); Assert.AreEqual(0, w.EvadeTimer);
        }

        [Test]
        public void BeingSpotted_RecentresSearch_AndResetsEvade()
        {
            var c = Cfg(); var w = new WantedSystem(c);
            w.AddHeat(1, "x"); w.Tick(0.1f, true, Origin, 10, 50);
            var far = new Vec2(w.SearchRadius + 10, 0);
            w.Tick(c.EvadeSeconds - 1, false, far, 10, 200);
            Assert.Greater(w.EvadeTimer, 0);

            w.Tick(0.1f, true, far, 10, 40);

            Assert.AreEqual(0, w.EvadeTimer); Assert.AreEqual(far.X, w.LastKnown.X); Assert.IsFalse(w.Searching);
        }

        [Test]
        public void SearchRadius_GrowsWithStars()
        {
            var w = new WantedSystem(Cfg()); w.AddHeat(1, "x"); float one = w.SearchRadius;
            w.AddHeat(2, "x");
            Assert.Greater(w.SearchRadius, one);
        }

        [Test]
        public void UnseenCrime_DoesNotCount_WitnessCallsAfterDelay()
        {
            var c = Cfg(); var w = new WantedSystem(c); string reported = null;
            w.WitnessReported += r => reported = r;

            Assert.IsFalse(w.ReportCrime(1, "a", false, false));
            Assert.IsTrue(w.ReportCrime(1, "b", false, true));
            w.Tick(c.WitnessDelay - 0.5f, false, Origin, 0, 999);
            Assert.AreEqual(0, w.Stars);
            w.Tick(1f, false, new Vec2(5, 5), 0, 999);
            Assert.AreEqual(1, w.Stars); Assert.AreEqual("b", reported);
            Assert.AreEqual(5f, w.LastKnown.X, "the call reports the player's position");
        }

        [Test]
        public void CrimeSeenByPolice_CountsImmediately()
        {
            var w = new WantedSystem(Cfg());
            w.ReportCrime(2, "x", true, false);
            Assert.AreEqual(2, w.Stars);
        }

        [Test]
        public void StoppedNextToCop_IsBusted_MovingIsNot()
        {
            var c = Cfg(); var w = new WantedSystem(c); bool busted = false;
            w.Busted += () => busted = true;
            w.AddHeat(1, "x");

            for (float t = 0; t < c.BustSeconds * 2; t += 0.1f) w.Tick(0.1f, true, Origin, 15, 3);
            Assert.IsFalse(busted);
            for (float t = 0; t < c.BustSeconds + 0.3f; t += 0.1f) w.Tick(0.1f, true, Origin, 0.5f, 3);
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
