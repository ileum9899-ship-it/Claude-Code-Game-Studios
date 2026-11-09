using System;

namespace NeonHarbor.Core
{
    /// <summary>Items sold at the gun shop.</summary>
    public enum ShopItem { Pistol, Smg, PistolAmmo, SmgAmmo, Armor }

    /// <summary>Result of a purchase.</summary>
    public enum PurchaseResult { Bought, NotEnoughMoney, AlreadyOwned, NeedsWeapon, AlreadyFull }

    /// <summary>
    /// Gun-shop and spray-garage rules, engine-free so they can be tested.
    /// </summary>
    /// <example><code>
    /// var r = Shop.Buy(cfg, ShopItem.Pistol, ref money, arsenal, ref armor);
    /// </code></example>
    public static class Shop
    {
        /// <summary>Display name of an item.</summary>
        public static string Name(ShopItem item)
        {
            switch (item)
            {
                case ShopItem.Pistol: return "권총";
                case ShopItem.Smg: return "기관단총";
                case ShopItem.PistolAmmo: return "권총 탄약 ×24";
                case ShopItem.SmgAmmo: return "기관단총 탄약 ×60";
                default: return "방탄복";
            }
        }

        /// <summary>Price of an item.</summary>
        public static int Price(HarborConfig cfg, ShopItem item)
        {
            switch (item)
            {
                case ShopItem.Pistol: return cfg.PricePistol;
                case ShopItem.Smg: return cfg.PriceSmg;
                case ShopItem.PistolAmmo: return cfg.PricePistolAmmo;
                case ShopItem.SmgAmmo: return cfg.PriceSmgAmmo;
                default: return cfg.PriceArmor;
            }
        }

        /// <summary>Buys an item if allowed and affordable; money and inventory are updated only on success.</summary>
        public static PurchaseResult Buy(HarborConfig cfg, ShopItem item, ref int money, Arsenal arsenal, ref float armor)
        {
            int price = Price(cfg, item);
            switch (item)
            {
                case ShopItem.Pistol: if (arsenal.Owns(WeaponKind.Pistol)) return PurchaseResult.AlreadyOwned; break;
                case ShopItem.Smg: if (arsenal.Owns(WeaponKind.Smg)) return PurchaseResult.AlreadyOwned; break;
                case ShopItem.PistolAmmo: if (!arsenal.Owns(WeaponKind.Pistol)) return PurchaseResult.NeedsWeapon; break;
                case ShopItem.SmgAmmo: if (!arsenal.Owns(WeaponKind.Smg)) return PurchaseResult.NeedsWeapon; break;
                case ShopItem.Armor: if (armor >= 100) return PurchaseResult.AlreadyFull; break;
            }
            if (money < price) return PurchaseResult.NotEnoughMoney;
            money -= price;
            switch (item)
            {
                case ShopItem.Pistol: arsenal.Give(WeaponKind.Pistol); break;
                case ShopItem.Smg: arsenal.Give(WeaponKind.Smg); break;
                case ShopItem.PistolAmmo: arsenal.Give(WeaponKind.Pistol); break; // owned → two magazines
                case ShopItem.SmgAmmo: arsenal.Give(WeaponKind.Smg); break;
                case ShopItem.Armor: armor = 100; break;
            }
            return PurchaseResult.Bought;
        }

        /// <summary>Outcome of driving into the spray garage.</summary>
        public enum SprayResult { Resprayed, NotEnoughMoney, PoliceWatching, NoCar }

        /// <summary>
        /// Spray garage: needs a car and the fee; refused while police can see you. On success the
        /// car is repaired, its fire put out, and any wanted level cleared.
        /// </summary>
        public static SprayResult Spray(HarborConfig cfg, bool inCar, bool seenByPolice, ref int money)
        {
            if (!inCar) return SprayResult.NoCar;
            if (seenByPolice) return SprayResult.PoliceWatching;
            if (money < cfg.SprayCost) return SprayResult.NotEnoughMoney;
            money -= cfg.SprayCost;
            return SprayResult.Resprayed;
        }

        /// <summary>Health regeneration: after a quiet period, heal toward the cap (never above it, never reducing).</summary>
        public static float Regenerate(HarborConfig cfg, float health, float secondsSinceDamage, float dt)
        {
            if (secondsSinceDamage < cfg.RegenDelay || health >= cfg.RegenCap || health <= 0) return health;
            return Math.Min(cfg.RegenCap, health + cfg.RegenRate * dt);
        }
    }
}
