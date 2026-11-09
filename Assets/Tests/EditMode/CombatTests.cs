using NeonHarbor.Core;
using NUnit.Framework;

namespace NeonHarbor.Tests
{
    /// <summary>Weapons, armour and hit tests (design/gdd/neon-harbor.md § Combat).</summary>
    public class CombatTests
    {
        private static void Tick(Arsenal a, float seconds) { for (float t = 0; t < seconds; t += 0.01f) a.Tick(0.01f); }

        [Test]
        public void StartsWithFists_GunsMustBePickedUp()
        {
            var a = new Arsenal();
            Assert.AreEqual(WeaponKind.Fist, a.Current);
            Assert.IsFalse(a.Select(WeaponKind.Pistol));
            a.Give(WeaponKind.Pistol);
            Assert.AreEqual(WeaponKind.Pistol, a.Current);
            Assert.AreEqual(12, a.Clip); Assert.AreEqual(36, a.Reserve);
        }

        [Test]
        public void Pistol_IsSemiAuto_SmgIsAuto()
        {
            var a = new Arsenal(); a.Give(WeaponKind.Pistol);
            Assert.IsTrue(a.TryFire(true, true));
            Tick(a, 1f);
            Assert.IsFalse(a.TryFire(false, true), "holding the trigger must not re-fire a pistol");
            a.Give(WeaponKind.Smg);
            int shots = 0;
            for (int k = 0; k < 100; k++) { if (a.TryFire(false, true)) shots++; a.Tick(0.01f); }
            Assert.Greater(shots, 8);
        }

        [Test]
        public void FireRate_IsRespected()
        {
            var a = new Arsenal(); a.Give(WeaponKind.Pistol);
            Assert.IsTrue(a.TryFire(true, true));
            a.Tick(0.1f);
            Assert.IsFalse(a.TryFire(true, true));
            a.Tick(0.2f);
            Assert.IsTrue(a.TryFire(true, true));
        }

        [Test]
        public void EmptyMagazine_ReloadsFromReserve()
        {
            var a = new Arsenal(); a.Give(WeaponKind.Pistol);
            for (int k = 0; k < 12; k++) { Assert.IsTrue(a.TryFire(true, true)); a.Tick(0.3f); }
            Assert.IsTrue(a.Reloading);
            Tick(a, 1.5f);
            Assert.AreEqual(12, a.Clip); Assert.AreEqual(24, a.Reserve);
        }

        [Test]
        public void NoAmmoLeft_ClicksInsteadOfFiring()
        {
            var a = new Arsenal(); a.Give(WeaponKind.Pistol); bool clicked = false;
            a.Click += done => { if (!done) clicked = true; };
            for (int k = 0; k < 200 && (a.Clip > 0 || a.Reserve > 0 || a.Reloading); k++) { a.TryFire(true, true); a.Tick(0.3f); }

            Assert.IsFalse(a.TryFire(true, true));
            Assert.IsTrue(clicked);
        }

        [Test]
        public void SecondPickup_AndAmmoBox_AddMagazines()
        {
            var a = new Arsenal();
            Assert.IsFalse(a.GiveAmmo());
            a.Give(WeaponKind.Smg); int r = a.Reserve;
            a.Give(WeaponKind.Smg); Assert.AreEqual(r + 60, a.Reserve);
            Assert.IsTrue(a.GiveAmmo()); Assert.AreEqual(r + 120, a.Reserve);
        }

        [Test]
        public void Armor_SoaksTwoThirds()
        {
            var v = new Vitals { Health = 100, Armor = 100 };
            v.Damage(30);
            Assert.AreEqual(100 - 19.8f, v.Armor, 0.01f);
            Assert.AreEqual(100 - 10.2f, v.Health, 0.01f);
            var bare = new Vitals { Health = 40 };
            Assert.IsTrue(bare.Damage(40));
        }

        [Test]
        public void RayCylinder_HitsBody_MissesAboveHead()
        {
            float t = HitTest.RayCylinder(0, 1, 0, 0, 0, 1, 0, 10, 0, 1.8f, 0.4f);
            Assert.AreEqual(9.6f, t, 0.01f);
            Assert.AreEqual(-1f, HitTest.RayCylinder(0, 2.5f, 0, 0, 0, 1, 0, 10, 0, 1.8f, 0.4f));
            Assert.AreEqual(-1f, HitTest.RayCylinder(0, 1, 0, 0, 0, -1, 0, 10, 0, 1.8f, 0.4f), "behind the shooter");
        }

        [Test]
        public void RayYawBox_RespectsRotation()
        {
            // a 4.6 m car rotated 90°: it now spans x, so a ray along z hitting 2 m off-centre in x still hits
            float t = HitTest.RayYawBox(2, 1, -10, 0, 0, 1, 0, 0, (float)System.Math.PI / 2, 0.9f, 2.3f, 0.25f, 1.5f);
            Assert.AreEqual(9.1f, t, 0.01f);
            Assert.AreEqual(-1f, HitTest.RayYawBox(2, 1, -10, 0, 0, 1, 0, 0, 0, 0.9f, 2.3f, 0.25f, 1.5f));
        }

        [Test]
        public void OfficerAccuracy_DropsWithDistanceAndSpeed()
        {
            Assert.Greater(HitTest.OfficerHitChance(5, 0), HitTest.OfficerHitChance(30, 0));
            Assert.Greater(HitTest.OfficerHitChance(10, 0), HitTest.OfficerHitChance(10, 20));
            Assert.AreEqual(0.08f, HitTest.OfficerHitChance(500, 50), 1e-5f);
        }

        [Test]
        public void ExplosionFalloff_IsLinearToRadius()
        {
            Assert.AreEqual(1f, HitTest.Falloff(0, 8)); Assert.AreEqual(0.5f, HitTest.Falloff(4, 8)); Assert.AreEqual(0f, HitTest.Falloff(9, 8));
        }

        [Test]
        public void NegativeHeat_IsClampedToZero()
        {
            var w = new WantedSystem(new HarborConfig());
            w.AddHeat(-5, "x");
            Assert.AreEqual(0, w.Stars); Assert.AreEqual(0f, w.Heat);
        }
    }
}
