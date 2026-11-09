using NeonHarbor.Core;
using NUnit.Framework;

namespace NeonHarbor.Tests
{
    /// <summary>Gun shop, spray garage, regeneration and GPS routes (design/gdd/neon-harbor.md § Shops).</summary>
    public class ShopTests
    {
        private static HarborConfig Cfg() { return new HarborConfig(); }

        [Test]
        public void BuyingAGun_ChargesAndGivesIt_OnlyOnce()
        {
            var c = Cfg(); var a = new Arsenal(); int money = 1000; float armor = 0;

            Assert.AreEqual(PurchaseResult.Bought, Shop.Buy(c, ShopItem.Pistol, ref money, a, ref armor));
            Assert.AreEqual(1000 - c.PricePistol, money); Assert.IsTrue(a.Owns(WeaponKind.Pistol));
            Assert.AreEqual(PurchaseResult.AlreadyOwned, Shop.Buy(c, ShopItem.Pistol, ref money, a, ref armor));
        }

        [Test]
        public void Ammo_NeedsTheWeapon_AndAddsTwoMagazines()
        {
            var c = Cfg(); var a = new Arsenal(); int money = 5000; float armor = 0;
            Assert.AreEqual(PurchaseResult.NeedsWeapon, Shop.Buy(c, ShopItem.SmgAmmo, ref money, a, ref armor));
            Assert.AreEqual(5000, money);
            Shop.Buy(c, ShopItem.Smg, ref money, a, ref armor); int r = a.ReserveOf(WeaponKind.Smg);
            Shop.Buy(c, ShopItem.SmgAmmo, ref money, a, ref armor);
            Assert.AreEqual(r + 60, a.ReserveOf(WeaponKind.Smg));
        }

        [Test]
        public void NotEnoughMoney_ChangesNothing()
        {
            var c = Cfg(); var a = new Arsenal(); int money = 10; float armor = 0;
            Assert.AreEqual(PurchaseResult.NotEnoughMoney, Shop.Buy(c, ShopItem.Armor, ref money, a, ref armor));
            Assert.AreEqual(10, money); Assert.AreEqual(0f, armor);
        }

        [Test]
        public void Armor_FillsToFull_AndIsRefusedWhenFull()
        {
            var c = Cfg(); var a = new Arsenal(); int money = 1000; float armor = 40;
            Assert.AreEqual(PurchaseResult.Bought, Shop.Buy(c, ShopItem.Armor, ref money, a, ref armor));
            Assert.AreEqual(100f, armor);
            Assert.AreEqual(PurchaseResult.AlreadyFull, Shop.Buy(c, ShopItem.Armor, ref money, a, ref armor));
        }

        [Test]
        public void Spray_RefusedOnFootOrWhileWatched_ChargesOtherwise()
        {
            var c = Cfg(); int money = 500;
            Assert.AreEqual(Shop.SprayResult.NoCar, Shop.Spray(c, false, false, ref money));
            Assert.AreEqual(Shop.SprayResult.PoliceWatching, Shop.Spray(c, true, true, ref money));
            Assert.AreEqual(500, money);
            Assert.AreEqual(Shop.SprayResult.Resprayed, Shop.Spray(c, true, false, ref money));
            Assert.AreEqual(500 - c.SprayCost, money);
            int poor = 5; Assert.AreEqual(Shop.SprayResult.NotEnoughMoney, Shop.Spray(c, true, false, ref poor));
        }

        [Test]
        public void Regen_WaitsThenHealsToCapOnly()
        {
            var c = Cfg();
            Assert.AreEqual(20f, Shop.Regenerate(c, 20, c.RegenDelay - 1, 1));
            Assert.AreEqual(20 + c.RegenRate, Shop.Regenerate(c, 20, c.RegenDelay + 1, 1), 1e-4f);
            Assert.AreEqual(c.RegenCap, Shop.Regenerate(c, c.RegenCap - 0.1f, 99, 1));
            Assert.AreEqual(80f, Shop.Regenerate(c, 80, 99, 1), "never lowers health above the cap");
        }

        [Test]
        public void Route_FollowsRoadNodes_EndToEnd()
        {
            var city = new CityLayout(Cfg());
            var from = new Vec2(10, 3); var to = new Vec2(city.Pitch * 3 + 20, city.Pitch * 2 - 4);

            var route = city.Route(from, to);

            Assert.AreEqual(from.X, route[0].X); Assert.AreEqual(to.Y, route[route.Count - 1].Y);
            for (int k = 1; k < route.Count - 2; k++)
            {
                var a = route[k]; var b = route[k + 1];
                Assert.IsTrue(a.X == b.X || a.Y == b.Y, "consecutive nodes share a road");
            }
        }
    }
}
