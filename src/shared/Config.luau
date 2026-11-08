--!strict
-- Crystal Cavern Simulator — every tunable gameplay number lives here.
-- Shared by server and client: the client reads it to show prices and odds, the
-- server reads it to charge and roll. The server never takes an amount from the
-- client, so exposing this table is safe.
-- Rationale for each value: design/game-brief.md § Tuning Knobs.

export type Upgrade = {
	displayName: string,
	description: string,
	currency: string,
	baseCost: number,
	costGrowth: number,
	effectPerLevel: number,
	maxLevel: number,
}

export type Pet = {
	displayName: string,
	rarity: string,
	bonus: number, -- added to the pet multiplier while equipped (0.1 = +10%)
}

export type Egg = {
	displayName: string,
	price: { [string]: number },
	outcomes: { { id: string, weight: number } },
}

local Config = {
	currency = "Gems",

	-- Gems per mine action and per second before upgrades and multipliers.
	baseClick = 1,
	baseIncome = 0,

	upgrades = {
		Pickaxe = {
			displayName = "Pickaxe",
			description = "+1 Gem per mine",
			currency = "Gems",
			baseCost = 15,
			costGrowth = 1.15,
			effectPerLevel = 1,
			maxLevel = 100,
		},
		Drill = {
			displayName = "Drill",
			description = "+2 Gems per second",
			currency = "Gems",
			baseCost = 50,
			costGrowth = 1.12,
			effectPerLevel = 2,
			maxLevel = 100,
		},
	} :: { [string]: Upgrade },

	-- Display order for the shop (dictionary order is not stable).
	upgradeOrder = { "Pickaxe", "Drill" },

	rebirth = {
		baseRequirement = 50000,
		requirementGrowth = 1.8,
		multiplierPerRebirth = 0.5,
	},

	offline = {
		maxHours = 8,
		efficiency = 0.5,
	},

	pets = {
		Mole = { displayName = "Mole", rarity = "Common", bonus = 0.1 },
		Bat = { displayName = "Cave Bat", rarity = "Uncommon", bonus = 0.25 },
		Golem = { displayName = "Pebble Golem", rarity = "Rare", bonus = 0.6 },
		Dragon = { displayName = "Crystal Dragon", rarity = "Epic", bonus = 1.5 },
		Phoenix = { displayName = "Prism Phoenix", rarity = "Legendary", bonus = 4 },
	} :: { [string]: Pet },

	maxEquippedPets = 3,
	maxPetInventory = 50,

	eggs = {
		Stone = {
			displayName = "Stone Egg",
			price = { Gems = 500 },
			outcomes = {
				{ id = "Mole", weight = 60 },
				{ id = "Bat", weight = 30 },
				{ id = "Golem", weight = 9 },
				{ id = "Dragon", weight = 1 },
			},
		},
		Geode = {
			displayName = "Geode Egg",
			price = { Gems = 25000 },
			outcomes = {
				{ id = "Bat", weight = 50 },
				{ id = "Golem", weight = 35 },
				{ id = "Dragon", weight = 13 },
				{ id = "Phoenix", weight = 2 },
			},
		},
	} :: { [string]: Egg },

	eggOrder = { "Stone", "Geode" },

	-- Remote rate limits (token bucket per player).
	rateLimits = {
		mine = { capacity = 12, refillPerSecond = 10 },
		action = { capacity = 6, refillPerSecond = 3 },
	},

	world = {
		crystalCount = 24,
		ringInner = 20,
		ringOuter = 70,
		clickDistance = 32,
		seed = 7, -- fixed so the cavern layout is the same in every server
	},

	save = {
		storeName = "CrystalCavernSim_v1",
		schema = 1,
		autosaveSeconds = 60,
		lockStaleSeconds = 300,
		loadAttempts = 4,
	},
}

return Config
