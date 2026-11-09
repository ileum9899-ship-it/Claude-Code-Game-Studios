using NeonHarbor.Core;
using NUnit.Framework;

namespace NeonHarbor.Tests
{
    /// <summary>City generation, road graph and collision (design/gdd/neon-harbor.md § City).</summary>
    public class CityLayoutTests
    {
        private static CityLayout NewCity() { return new CityLayout(new HarborConfig()); }

        [Test]
        public void SameSeed_ProducesSameCity()
        {
            var a = NewCity(); var b = NewCity();

            Assert.AreEqual(a.Colliders.Count, b.Colliders.Count);
            for (int k = 0; k < a.Colliders.Count; k++) Assert.AreEqual(a.Colliders[k].H, b.Colliders[k].H);
        }

        [Test]
        public void Blocks_CoverGrid_WithEveryDistrict()
        {
            var c = NewCity();

            Assert.AreEqual(c.N * c.N, c.Blocks.Count);
            foreach (BlockType t in System.Enum.GetValues(typeof(BlockType)))
                Assert.IsTrue(c.Blocks.Exists(b => b.Type == t), t.ToString());
        }

        [Test]
        public void NoCollider_TouchesARoad()
        {
            var c = NewCity();

            foreach (var box in c.Colliders)
            {
                Assert.IsFalse(c.OnRoad((box.X0 + box.X1) / 2, (box.Z0 + box.Z1) / 2), "collider centre on road");
                Assert.IsFalse(c.OnRoad(box.X0 + 0.01f, box.Z0 + 0.01f), "collider corner on road");
            }
        }

        [Test]
        public void Downtown_IsTallerThanResidential()
        {
            var c = NewCity();
            float down = 0, res = 0; int nd = 0, nr = 0;
            foreach (var b in c.Blocks)
                foreach (var bl in b.Buildings)
                {
                    if (b.Type == BlockType.Downtown) { down += bl.Top; nd++; }
                    if (b.Type == BlockType.Residential) { res += bl.Top; nr++; }
                }

            Assert.Greater(down / nd, 2 * (res / nr));
        }

        [Test]
        public void Path_IsManhattanShortest()
        {
            var c = NewCity();

            var p = c.Path((0, 0), (3, 5));

            Assert.AreEqual(9, p.Count); // 8 steps + start
            Assert.AreEqual((0, 0), p[0]);
            Assert.AreEqual((3, 5), p[p.Count - 1]);
        }

        [Test]
        public void OnRoad_DetectsIntersectionsAndBlocks()
        {
            var c = NewCity();

            Assert.IsTrue(c.OnRoad(c.Pitch * 2, c.Pitch * 3));
            Assert.IsTrue(c.OnRoad(c.Pitch * 2 + c.Pitch / 2, c.Pitch * 3 + 1));
            Assert.IsFalse(c.OnRoad(c.Pitch * 2 + c.Pitch / 2, c.Pitch * 3 + c.Pitch / 2));
            Assert.AreEqual(-100f, c.GroundHeight(-50, -50));
        }

        [Test]
        public void CollideCircle_PushesOutOfBuilding()
        {
            var c = NewCity();
            var box = c.Colliders[0];
            float x = (box.X0 + box.X1) / 2, z = (box.Z0 + box.Z1) / 2;

            bool hit = c.CollideCircle(ref x, ref z, 0.5f, out _, out _);

            Assert.IsTrue(hit);
            bool outside = x <= box.X0 - 0.49f || x >= box.X1 + 0.49f || z <= box.Z0 - 0.49f || z >= box.Z1 + 0.49f;
            Assert.IsTrue(outside);
        }

        [Test]
        public void Raycast_HitsBuilding_ClearAlongRoad()
        {
            var c = NewCity();
            var box = c.Colliders[0];
            float cx = (box.X0 + box.X1) / 2, cz = (box.Z0 + box.Z1) / 2;

            Assert.Less(c.Raycast(cx, 2, box.Z0 - 20, cx, 2, cz), 1f);
            Assert.AreEqual(1f, c.Raycast(c.Pitch * 2, 1, c.Pitch * 2 + 5, c.Pitch * 2, 1, c.Pitch * 3 - 5));
        }

        [Test]
        public void SidewalkPoint_StaysOnTheBlockRing()
        {
            var c = NewCity(); var blk = c.Blocks[10];

            for (int k = 0; k < 20; k++)
            {
                var p = c.SidewalkPoint(blk, k / 20f);
                Assert.IsTrue(p.x >= blk.X0 && p.x <= blk.X1 && p.z >= blk.Z0 && p.z <= blk.Z1);
                Assert.IsFalse(c.OnRoad(p.x, p.z));
            }
        }
    }
}
