using NeonHarbor.Core;
using NUnit.Framework;

namespace NeonHarbor.Tests
{
    /// <summary>Accidents and the longer, smarter police search (design/gdd/neon-harbor.md § Accidents, § Wanted level).</summary>
    public class CrashAndSearchTests
    {
        private static HarborConfig Cfg() { return new HarborConfig(); }

        [Test]
        public void Damage_ZeroForBumps_GrowsFasterThanSpeed()
        {
            Assert.AreEqual(0f, CrashModel.Damage(2));
            float d10 = CrashModel.Damage(10), d20 = CrashModel.Damage(20);
            Assert.Greater(d10, 0); Assert.Greater(d20, d10 * 2);
            Assert.LessOrEqual(CrashModel.Damage(200), 120f);
        }

        [Test]
        public void Dents_AreCapped_AndLightsBreakOnHardHits()
        {
            Assert.AreEqual(0f, CrashModel.DentDepth(3));
            Assert.AreEqual(0.28f, CrashModel.DentDepth(80), 1e-5f);
            Assert.IsFalse(CrashModel.LightsBreak(5)); Assert.IsTrue(CrashModel.LightsBreak(12));
        }

        [Test]
        public void Ejection_OnlyOnFastHeadOnImpacts()
        {
            var fwd = new Vec2(0, 1);
            float headOn = CrashModel.Frontal(fwd, new Vec2(0, -1)), side = CrashModel.Frontal(fwd, new Vec2(1, 0)), rear = CrashModel.Frontal(fwd, new Vec2(0, 1));
            Assert.AreEqual(1f, headOn, 1e-5f); Assert.AreEqual(0f, side, 1e-5f); Assert.AreEqual(0f, rear, 1e-5f);
            Assert.IsTrue(CrashModel.EjectsDriver(30, headOn));
            Assert.IsFalse(CrashModel.EjectsDriver(30, side));
            Assert.IsFalse(CrashModel.EjectsDriver(15, headOn));
            Assert.GreaterOrEqual(CrashModel.EjectionInjury(24), 15f);
        }

        [Test]
        public void Smoke_AndPerformance_FollowHealth()
        {
            Assert.AreEqual(0, CrashModel.SmokeLevel(80)); Assert.AreEqual(1, CrashModel.SmokeLevel(40)); Assert.AreEqual(2, CrashModel.SmokeLevel(10));
            Assert.AreEqual(1f, CrashModel.Performance(100)); Assert.Less(CrashModel.Performance(0), 0.6f);
        }

        [Test]
        public void EvadeTime_GrowsWithStars()
        {
            var w = new WantedSystem(Cfg()); w.AddHeat(1, "x"); float one = w.EvadeTime;
            w.AddHeat(3, "x");
            Assert.Greater(w.EvadeTime, one); Assert.GreaterOrEqual(one, 20f, "one star lasts at least 20 s out of the search area");
        }

        [Test]
        public void Reveal_RecentresSearch_AndRestartsEscape()
        {
            var c = Cfg(); var w = new WantedSystem(c); string how = null;
            w.Revealed += h => how = h;
            w.AddHeat(2, "x"); w.Tick(0.1f, true, new Vec2(0, 0), new Vec2(10, 0), 50);
            var far = new Vec2(w.SearchRadius + 30, 0);
            w.Tick(5, false, far, new Vec2(10, 0), 200);
            Assert.Greater(w.EvadeTimer, 0);

            w.Reveal(far, "총소리");

            Assert.AreEqual(0, w.EvadeTimer); Assert.AreEqual(far.X, w.LastKnown.X); Assert.AreEqual("총소리", how);
            Assert.IsTrue(w.Searching, "heard, not seen");
        }

        [Test]
        public void Reveal_IgnoredWhenNotWanted()
        {
            var w = new WantedSystem(Cfg()); bool fired = false; w.Revealed += _ => fired = true;
            w.Reveal(new Vec2(5, 5), "x");
            Assert.IsFalse(fired);
        }

        [Test]
        public void PredictedDrift_FollowsLastVelocity_AndIsCapped()
        {
            var w = new WantedSystem(Cfg()); w.AddHeat(1, "x");
            w.Tick(0.1f, true, new Vec2(0, 0), new Vec2(0, 15), 50);
            w.Tick(2f, false, new Vec2(0, 10), new Vec2(0, 15), 200);
            Assert.Greater(w.PredictedDrift.Y, 20f); Assert.AreEqual(0f, w.PredictedDrift.X, 1e-3f);
            w.Tick(60f, false, new Vec2(0, 10), new Vec2(0, 15), 200);
            Assert.LessOrEqual(w.PredictedDrift.Length, w.SearchRadius * 0.7f + 1e-3f);
        }

        [Test]
        public void SearchWithDrift_LooksWhereTheTargetWasHeading()
        {
            var lk = new Vec2(100, 100); var drift = new Vec2(0, 60);
            Assert.Less(Vec2.Distance(PoliceTactics.SearchWaypoint(lk, 120, 0, 3, 0, drift), lk), 8f, "still checks the last known spot first");
            var second = PoliceTactics.SearchWaypoint(lk, 120, 0, 3, 1, drift);
            Assert.Greater(second.Y - lk.Y, 25f, "then goes the way the target was heading");
            for (int leg = 1; leg < 10; leg++)
                for (int u = 0; u < 3; u++) Assert.LessOrEqual(Vec2.Distance(PoliceTactics.SearchWaypoint(lk, 120, u, 3, leg, drift), lk), 108.01f);
        }
    }
}
