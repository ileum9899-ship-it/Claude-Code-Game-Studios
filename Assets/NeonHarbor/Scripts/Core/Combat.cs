using System;

namespace NeonHarbor.Core
{
    /// <summary>Weapons the player can carry.</summary>
    public enum WeaponKind { Fist = 0, Pistol = 1, Smg = 2 }

    /// <summary>Static properties of a weapon (same numbers as the browser build's WEAPONS).</summary>
    public sealed class WeaponSpec
    {
        public WeaponKind Kind; public string Name; public bool Melee, Automatic;
        /// <summary>Damage per hit.</summary>
        public float Damage;
        /// <summary>Seconds between shots / punches.</summary>
        public float FireInterval;
        /// <summary>Rounds per magazine (0 = melee).</summary>
        public int Magazine;
        /// <summary>Reload time (s).</summary>
        public float ReloadTime;
        /// <summary>Random aim error (radians-ish, applied per axis) while aiming; doubled from the hip.</summary>
        public float Spread;
        /// <summary>Max range (m).</summary>
        public float Range;
        /// <summary>Total rounds when first picked up.</summary>
        public int StartAmmo;

        /// <summary>All weapons, indexed by <see cref="WeaponKind"/>.</summary>
        public static readonly WeaponSpec[] All =
        {
            new WeaponSpec { Kind = WeaponKind.Fist, Name = "주먹", Melee = true, Damage = 18, FireInterval = 0.42f, Range = 1.7f },
            new WeaponSpec { Kind = WeaponKind.Pistol, Name = "권총", Damage = 34, FireInterval = 0.26f, Magazine = 12, ReloadTime = 1.3f, Spread = 0.010f, Range = 140, StartAmmo = 48 },
            new WeaponSpec { Kind = WeaponKind.Smg, Name = "기관단총", Automatic = true, Damage = 19, FireInterval = 0.085f, Magazine = 30, ReloadTime = 1.8f, Spread = 0.028f, Range = 100, StartAmmo = 120 },
        };
    }

    /// <summary>
    /// The player's weapons: ownership, magazine + reserve ammo, fire cooldown and
    /// reload timer. Pure state machine — the caller does aiming and hit tests.
    /// </summary>
    /// <example><code>
    /// var arms = new Arsenal();
    /// arms.Give(WeaponKind.Pistol);
    /// if (arms.TryFire(triggerPressed: true, triggerHeld: true)) { /* cast the shot */ }
    /// arms.Tick(dt);
    /// </code></example>
    public sealed class Arsenal
    {
        private readonly bool[] owned = { true, false, false };
        private readonly int[] clip = new int[3], reserve = new int[3];
        private float cooldown, reload;

        /// <summary>Selected weapon.</summary>
        public WeaponKind Current { get; private set; } = WeaponKind.Fist;
        /// <summary>Spec of the selected weapon.</summary>
        public WeaponSpec Spec => WeaponSpec.All[(int)Current];
        /// <summary>Rounds in the magazine of the selected weapon.</summary>
        public int Clip => clip[(int)Current];
        /// <summary>Spare rounds of the selected weapon.</summary>
        public int Reserve => reserve[(int)Current];
        /// <summary>True while reloading.</summary>
        public bool Reloading => reload > 0;
        /// <summary>Raised when a reload finishes or an empty trigger is pulled (true = reload done).</summary>
        public event Action<bool> Click;

        /// <summary>True when the weapon is owned.</summary>
        public bool Owns(WeaponKind k) { return owned[(int)k]; }
        /// <summary>Spare rounds of a weapon.</summary>
        public int ReserveOf(WeaponKind k) { return reserve[(int)k]; }
        /// <summary>Magazine rounds of a weapon.</summary>
        public int ClipOf(WeaponKind k) { return clip[(int)k]; }

        /// <summary>Picks up a weapon: first time gives a full magazine + start ammo and selects it; later gives two magazines.</summary>
        public void Give(WeaponKind k)
        {
            var s = WeaponSpec.All[(int)k];
            if (s.Melee) return;
            if (!owned[(int)k]) { owned[(int)k] = true; clip[(int)k] = s.Magazine; reserve[(int)k] = s.StartAmmo - s.Magazine; Select(k); }
            else reserve[(int)k] += s.Magazine * 2;
        }

        /// <summary>Ammo box: two magazines for every owned gun. Returns false if no gun is owned.</summary>
        public bool GiveAmmo()
        {
            bool any = false;
            for (int k = 1; k < 3; k++) if (owned[k]) { reserve[k] += WeaponSpec.All[k].Magazine * 2; any = true; }
            return any;
        }

        /// <summary>Selects an owned weapon (cancels a reload).</summary>
        public bool Select(WeaponKind k) { if (!owned[(int)k]) return false; Current = k; reload = 0; return true; }

        /// <summary>Selects the next owned weapon.</summary>
        public void Cycle() { for (int n = 1; n <= 3; n++) { var k = (WeaponKind)(((int)Current + n) % 3); if (owned[(int)k]) { Select(k); return; } } }

        /// <summary>Starts a reload if it would add rounds.</summary>
        public bool StartReload()
        {
            var s = Spec; int i = (int)Current;
            if (s.Melee || reload > 0 || clip[i] >= s.Magazine || reserve[i] <= 0) return false;
            reload = s.ReloadTime; return true;
        }

        /// <summary>
        /// Attempts to fire. Semi-automatic weapons need a fresh press; automatic ones fire while held.
        /// Returns true when a shot (or punch) happens; empty magazines auto-reload.
        /// </summary>
        public bool TryFire(bool triggerPressed, bool triggerHeld)
        {
            var s = Spec; int i = (int)Current;
            if (!(s.Automatic ? triggerHeld : triggerPressed) || cooldown > 0 || reload > 0) return false;
            if (s.Melee) { cooldown = s.FireInterval; return true; }
            if (clip[i] <= 0)
            {
                if (!StartReload()) { Click?.Invoke(false); cooldown = 0.3f; }
                return false;
            }
            clip[i]--; cooldown = s.FireInterval;
            if (clip[i] == 0) StartReload();
            return true;
        }

        /// <summary>Advances cooldown and reload timers.</summary>
        public void Tick(float dt)
        {
            cooldown -= dt;
            if (reload > 0)
            {
                reload -= dt;
                if (reload <= 0)
                {
                    int i = (int)Current, n = Math.Min(Spec.Magazine - clip[i], reserve[i]);
                    clip[i] += n; reserve[i] -= n; Click?.Invoke(true);
                }
            }
        }
    }

    /// <summary>Health with an armour layer that soaks two thirds of incoming damage.</summary>
    public struct Vitals
    {
        public float Health, Armor;

        /// <summary>Applies damage; returns true when health reaches zero.</summary>
        public bool Damage(float amount)
        {
            if (Armor > 0) { float soaked = Math.Min(Armor, amount * 0.66f); Armor -= soaked; amount -= soaked; }
            Health -= amount;
            return Health <= 0;
        }
    }

    /// <summary>Ray tests used by hitscan weapons.</summary>
    public static class HitTest
    {
        /// <summary>
        /// Ray (o + d·t) against an upright cylinder at (cx, cz) from y0 to y1 with radius r.
        /// Returns the entry distance, or -1 when missed or behind the origin.
        /// </summary>
        public static float RayCylinder(float ox, float oy, float oz, float dx, float dy, float dz, float cx, float cz, float y0, float y1, float r)
        {
            float px = ox - cx, pz = oz - cz, a = dx * dx + dz * dz, b = 2 * (px * dx + pz * dz), c = px * px + pz * pz - r * r;
            if (a < 1e-8f) return -1;
            float disc = b * b - 4 * a * c; if (disc < 0) return -1;
            float t = (-b - (float)Math.Sqrt(disc)) / (2 * a); if (t < 0) return -1;
            float y = oy + dy * t; return y >= y0 && y <= y1 ? t : -1;
        }

        /// <summary>
        /// Ray against a box rotated by <paramref name="yaw"/> around Y (car body):
        /// half extents (hw, hl) on the ground, height y0..y1. Returns entry distance or -1.
        /// </summary>
        public static float RayYawBox(float ox, float oy, float oz, float dx, float dy, float dz, float cx, float cz, float yaw, float hw, float hl, float y0, float y1)
        {
            float s = (float)Math.Sin(-yaw), c = (float)Math.Cos(-yaw);
            float lx = ox - cx, lz = oz - cz;
            float lox = lx * c + lz * s, loz = -lx * s + lz * c, ldx = dx * c + dz * s, ldz = -dx * s + dz * c;
            float t0 = 0, t1 = float.MaxValue;
            if (!Slab(lox, ldx, -hw, hw, ref t0, ref t1) || !Slab(oy, dy, y0, y1, ref t0, ref t1) || !Slab(loz, ldz, -hl, hl, ref t0, ref t1)) return -1;
            return t0;
        }

        private static bool Slab(float o, float d, float mn, float mx, ref float t0, ref float t1)
        {
            if (Math.Abs(d) < 1e-9f) return o >= mn && o <= mx;
            float ta = (mn - o) / d, tb = (mx - o) / d;
            if (ta > tb) { float t = ta; ta = tb; tb = t; }
            t0 = Math.Max(t0, ta); t1 = Math.Min(t1, tb);
            return t0 <= t1;
        }

        /// <summary>Officer hit chance: worse with distance and target speed.</summary>
        public static float OfficerHitChance(float distance, float targetSpeed)
        {
            return Mathx.Clamp(0.55f - distance * 0.012f - targetSpeed * 0.012f, 0.08f, 0.55f);
        }

        /// <summary>Explosion falloff: full at the centre, zero at the radius.</summary>
        public static float Falloff(float distance, float radius) { return distance >= radius ? 0 : 1 - distance / radius; }
    }
}
