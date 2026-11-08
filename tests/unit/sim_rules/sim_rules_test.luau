-- Unit tests for SimRules (pure gameplay rules). Run from the repo root:
--   lune run tests/unit/sim_rules/sim_rules_test.luau
-- Exits 1 on any failure.

local process = require("@lune/process")

local Config = require("../../../src/shared/Config")
local EconomyMath = require("../../../src/shared/EconomyMath")
local SimRules = require("../../../src/shared/SimRules")

local rules = SimRules.new(Config, EconomyMath)
local passed, failed = 0, 0

local function test(name: string, fn: () -> ())
	local ok, err = pcall(fn)
	if ok then
		passed += 1
	else
		failed += 1
		print(`FAIL  {name}\n      {err}`)
	end
end

local function eq(actual: any, expected: any, label: string?)
	if actual ~= expected then
		error(`{label or "value"}: expected {tostring(expected)}, got {tostring(actual)}`, 2)
	end
end

local function withGems(n: number)
	local s = rules:newState()
	s.wallet.Gems = n
	return s
end

test("mine pays baseClick for a new player", function()
	local s = rules:newState()
	eq(rules:mine(s), Config.baseClick, "gain")
	eq(s.wallet.Gems, Config.baseClick, "wallet")
end)

test("pickaxe levels add effectPerLevel to each mine", function()
	local s = rules:newState()
	s.levels.Pickaxe = 4
	eq(rules:perMine(s), Config.baseClick + 4 * Config.upgrades.Pickaxe.effectPerLevel)
end)

test("buyUpgrade charges levelCost and adds a level", function()
	local up = Config.upgrades.Pickaxe
	local s = withGems(up.baseCost)
	eq(rules:buyUpgrade(s, "Pickaxe", false), 1, "bought")
	eq(s.levels.Pickaxe, 1, "level")
	eq(s.wallet.Gems, 0, "wallet")
end)

test("buyUpgrade refuses when short, unknown id, or bad type", function()
	local s = withGems(Config.upgrades.Pickaxe.baseCost - 1)
	eq(rules:buyUpgrade(s, "Pickaxe", false), 0, "short")
	eq(rules:buyUpgrade(s, "Laser", false), 0, "unknown")
	eq(rules:buyUpgrade(s, 42, false), 0, "number id")
	eq(s.wallet.Gems, Config.upgrades.Pickaxe.baseCost - 1, "wallet untouched")
end)

test("buy max buys exactly what the wallet covers", function()
	local up = Config.upgrades.Drill
	local cost3 = EconomyMath.cumulativeCostExact(up.baseCost, up.costGrowth, 0, 3)
	local s = withGems(cost3)
	eq(rules:buyUpgrade(s, "Drill", true), 3, "count")
	eq(s.wallet.Gems, 0, "wallet")
end)

test("buyUpgrade stops at maxLevel", function()
	local up = Config.upgrades.Pickaxe
	local s = withGems(1e15)
	s.levels.Pickaxe = up.maxLevel
	eq(rules:buyUpgrade(s, "Pickaxe", false), 0, "one")
	eq(rules:buyUpgrade(s, "Pickaxe", true), 0, "max")
	eq(rules:upgradePrice(s, "Pickaxe"), nil, "price is nil when maxed")
end)

test("tick carries fractional income", function()
	local s = rules:newState()
	s.levels.Drill = 1
	s.rebirths = 1 -- x1.5 -> 3 per second
	eq(rules:tick(s, 0.5), 1, "first half-second pays 1")
	eq(rules:tick(s, 0.5), 2, "carry makes up the remainder")
	eq(rules:tick(s, -1), 0, "negative dt pays nothing")
	eq(rules:tick(s, 0 / 0), 0, "NaN dt pays nothing")
end)

test("rebirth needs the requirement, resets gems and levels, keeps pets", function()
	local req = Config.rebirth.baseRequirement
	local s = withGems(req - 1)
	eq(rules:rebirth(s), false, "short")
	s.wallet.Gems = req
	s.levels.Pickaxe = 10
	s.pets.Mole = 2
	eq(rules:rebirth(s), true, "ok")
	eq(s.wallet.Gems, 0, "gems reset")
	eq(s.levels.Pickaxe, nil, "levels reset")
	eq(s.pets.Mole, 2, "pets kept")
	eq(s.rebirths, 1, "count")
	eq(rules:rebirthRequirement(s), math.floor(req * Config.rebirth.requirementGrowth), "next requirement")
end)

test("only the best maxEquippedPets pets count", function()
	local s = rules:newState()
	s.pets = { Mole = 5, Dragon = 1, Golem = 1 }
	local eqd = rules:equippedPets(s)
	eq(#eqd, Config.maxEquippedPets, "count")
	eq(eqd[1], "Dragon", "best first")
	eq(eqd[2], "Golem", "second")
	local expected = 1 + Config.pets.Dragon.bonus + Config.pets.Golem.bonus + Config.pets.Mole.bonus
	eq(math.abs(rules:multiplier(s) - expected) < 1e-9, true, "multiplier")
end)

test("openEgg charges, grants the rolled pet, and respects inventory", function()
	local egg = Config.eggs.Stone
	local s = withGems(egg.price.Gems * 2)
	local pet = rules:openEgg(s, "Stone", 0) -- unit 0 -> first outcome
	eq(pet, egg.outcomes[1].id, "first outcome")
	eq(s.wallet.Gems, egg.price.Gems, "charged once")
	local last = rules:openEgg(s, "Stone", 1) -- unit 1 -> last outcome
	eq(last, egg.outcomes[#egg.outcomes].id, "last outcome")
	local none, why = rules:openEgg(s, "Stone", 0.5)
	eq(none, nil, "broke")
	eq(why, "not enough Gems", "reason")
	s.wallet.Gems = 1e12
	s.pets = { Mole = Config.maxPetInventory }
	local full, why2 = rules:openEgg(s, "Stone", 0.5)
	eq(full, nil, "full")
	eq(why2, "inventory full", "reason")
	eq(select(2, rules:openEgg(s, "Nope", 0.5)), "invalid", "unknown egg")
end)

test("egg odds sum to 100", function()
	for id, egg in Config.eggs do
		local total = 0
		for _, p in EconomyMath.oddsPercent(egg.outcomes) do
			total += p
		end
		eq(math.abs(total - 100) < 1e-9, true, id)
	end
end)

test("repair fixes tampered data and drops unknown ids", function()
	local now = 1000000
	local saved = {
		wallet = { Gems = -5 },
		levels = { Pickaxe = 1e9, Laser = 3 },
		pets = { Mole = 2.7, Unicorn = 1 },
		rebirths = 0 / 0,
		lastSeen = now,
	}
	local s, notes = rules:repair(saved, now)
	eq(s.wallet.Gems, 0, "negative gems")
	eq(s.levels.Pickaxe, Config.upgrades.Pickaxe.maxLevel, "clamped level")
	eq(s.levels.Laser, nil, "unknown upgrade dropped")
	eq(s.pets.Mole, 2, "floored pet count")
	eq(s.pets.Unicorn, nil, "unknown pet dropped")
	eq(s.rebirths, 0, "NaN rebirths")
	eq(#notes >= 5, true, "notes recorded")
end)

test("repair handles nil and garbage saves", function()
	local s = rules:repair(nil, 0)
	eq(s.wallet.Gems, 0, "nil")
	local s2 = rules:repair("garbage", 0)
	eq(s2.rebirths, 0, "string")
end)

test("offline income is capped, scaled, and ignores future timestamps", function()
	local now = 1000000
	local drill = Config.upgrades.Drill
	local saved = { levels = { Drill = 5 }, lastSeen = now - 100 * 3600 }
	local _, _, paid = rules:repair(saved, now)
	local rate = 5 * drill.effectPerLevel
	eq(paid, math.floor(Config.offline.maxHours * 3600 * rate * Config.offline.efficiency), "capped")
	local _, _, future = rules:repair({ levels = { Drill = 5 }, lastSeen = now + 999 }, now)
	eq(future, 0, "future lastSeen pays 0")
end)

test("serialize round-trips through repair", function()
	local s = rules:newState()
	s.wallet.Gems = 1234
	s.levels.Pickaxe = 7
	s.pets.Bat = 3
	s.rebirths = 2
	local now = 5000
	local back = rules:repair(rules:serialize(s, now), now)
	eq(back.wallet.Gems, 1234, "gems")
	eq(back.levels.Pickaxe, 7, "level")
	eq(back.pets.Bat, 3, "pets")
	eq(back.rebirths, 2, "rebirths")
end)

print(`{passed} passed, {failed} failed`)
process.exit(if failed > 0 then 1 else 0)
