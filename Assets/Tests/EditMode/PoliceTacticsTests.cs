using NeonHarbor.Core;
using NUnit.Framework;

namespace NeonHarbor.Tests
{
    /// <summary>Police AI maths (design/gdd/neon-harbor.md § Police AI).</summary>
    public class PoliceTacticsTests
    {
        [Test]
        public void Intercept_LeadsAMovingTarget()
        {
            var aim = PoliceTactics.Intercept(new Vec2(0, 0), 30, new Vec2(0, 50), new Vec2(20, 0), 5);
            Assert.Greater(aim.X, 10f, "aims ahead of the target, not at it");
            // the chaser really can reach that point in the same time as the target
            float tTarget = (aim.X - 0) / 20f, tChaser = aim.Length / 30f;
            Assert.AreEqual(tTarget, tChaser, 0.05f);
        }

        [Test]
        public void Intercept_StationaryTarget_IsTheTarget_AndLeadIsCapped()
        {
            var still = PoliceTactics.Intercept(new Vec2(0, 0), 25, new Vec2(10, 10), new Vec2(0, 0), 3);
            Assert.AreEqual(10f, still.X, 1e-3f); Assert.AreEqual(10f, still.Y, 1e-3f);
            var fast = PoliceTactics.Intercept(new Vec2(0, 0), 10, new Vec2(0, 100), new Vec2(50, 0), 2);
            Assert.AreEqual(100f, fast.X, 1e-3f, "unreachable target → capped lead");
        }

        [Test]
        public void CornerSpeed_SlowsForSharpTurnsNearby()
        {
            float straight = PoliceTactics.SpeedForCorner(0, 5, 50), right = PoliceTactics.SpeedForCorner(1.57f, 5, 50), farRight = PoliceTactics.SpeedForCorner(1.57f, 80, 50);
            Assert.AreEqual(50f, straight, 1e-3f);
            Assert.Less(right, 16f);
            Assert.Greater(farRight, right, "far from the corner it may still go fast");
        }

        [Test]
        public void LookAhead_WalksAlongThePolyline()
        {
            var path = new[] { new Vec2(0, 0), new Vec2(0, 10), new Vec2(10, 10) };
            var p = PoliceTactics.LookAhead(path, new Vec2(0.5f, 2), 12);
            Assert.AreEqual(4f, p.X, 0.01f); Assert.AreEqual(10f, p.Y, 0.01f);
            var end = PoliceTactics.LookAhead(path, new Vec2(0, 0), 999);
            Assert.AreEqual(10f, end.X); Assert.AreEqual(10f, end.Y);
        }

        [Test]
        public void Roles_ClosestPursues_ThenFlankerThenInterceptor()
        {
            var roles = PoliceTactics.AssignRoles(new[] { 80f, 20f, 50f, 120f });
            Assert.AreEqual(PoliceRole.Pursuer, roles[1]);
            Assert.AreEqual(PoliceRole.Flanker, roles[2]);
            Assert.AreEqual(PoliceRole.Interceptor, roles[0]);
            Assert.AreEqual(PoliceRole.Pursuer, roles[3]);
        }

        [Test]
        public void NodeAhead_FollowsTheDominantHeading()
        {
            var city = new CityLayout(new HarborConfig());
            var n = PoliceTactics.NodeAhead(city, new Vec2(city.Pitch * 3, city.Pitch * 3 + 20), new Vec2(0.2f, 1), 2);
            Assert.AreEqual((3, 5), n);
            var clamped = PoliceTactics.NodeAhead(city, new Vec2(city.Pitch * 8, 0), new Vec2(1, 0), 3);
            Assert.AreEqual(city.N, clamped.Item1);
        }

        [Test]
        public void InView_UsesConeAndNearRadius()
        {
            var eye = new Vec2(0, 0);
            Assert.IsTrue(PoliceTactics.InView(eye, 0, new Vec2(0, 50), 2.4f, 90, 15), "straight ahead");
            Assert.IsFalse(PoliceTactics.InView(eye, 0, new Vec2(0, -50), 2.4f, 90, 15), "behind");
            Assert.IsTrue(PoliceTactics.InView(eye, 0, new Vec2(0, -10), 2.4f, 90, 15), "behind but very close");
            Assert.IsFalse(PoliceTactics.InView(eye, 0, new Vec2(0, 120), 2.4f, 90, 15), "too far");
        }

        [Test]
        public void Search_StartsAtLastKnownPosition()
        {
            var lk = new Vec2(100, 100);
            for (int u = 0; u < 3; u++) Assert.Less(Vec2.Distance(PoliceTactics.SearchWaypoint(lk, 120, u, 3, 0), lk), 8f);
        }

        [Test]
        public void Search_SpiralsOutward_InsideTheCircle_AndUnitsSplitUp()
        {
            var lk = new Vec2(100, 100);
            float prev = 0;
            for (int leg = 1; leg < 12; leg++)
            {
                for (int u = 0; u < 3; u++) Assert.LessOrEqual(Vec2.Distance(PoliceTactics.SearchWaypoint(lk, 80, u, 3, leg), lk), 80 * 0.9f + 0.01f);
                float r = Vec2.Distance(PoliceTactics.SearchWaypoint(lk, 80, 0, 3, leg), lk);
                Assert.GreaterOrEqual(r, prev - 1e-3f, "never moves back inward"); prev = r;
            }
            var a = PoliceTactics.SearchWaypoint(lk, 80, 0, 3, 3); var b = PoliceTactics.SearchWaypoint(lk, 80, 1, 3, 3);
            Assert.Greater(Vec2.Distance(a, b), 20f);
        }

        [Test]
        public void OfficerAccuracy_RisesWithStars_FallsWithDistanceAndSpeed()
        {
            Assert.Greater(PoliceTactics.OfficerAccuracy(15, 0, 5), PoliceTactics.OfficerAccuracy(15, 0, 2));
            Assert.Greater(PoliceTactics.OfficerAccuracy(8, 0, 3), PoliceTactics.OfficerAccuracy(30, 0, 3));
            Assert.Greater(PoliceTactics.OfficerAccuracy(15, 0, 3), PoliceTactics.OfficerAccuracy(15, 15, 3));
        }
    }
}
