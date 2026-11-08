--!strict
-- Pure gameplay rules for Crystal Cavern Simulator.
--
-- No engine services and no requires: config and EconomyMath are injected through
-- SimRules.new, so the same code runs in a Roblox ModuleScript and under Lune in
-- tests/unit/sim_rules. Every function that changes a State is called only by the
-- server; the client uses the read-only helpers (prices, snapshot) for display.

export type State = {
	wallet: { [string]: number },
	levels: { [string]: number },
	pets: { [string]: number }, -- pet id -> count owned
	rebirths: number,
	carry: number, -- fractional passive income not yet paid
	totalMined: number,
}

export type Snapshot = {
	gems: number,
	perSecond: number,
	perMine: number,
	multiplier: number,
	rebirths: number,
	rebirthRequirement: number,
	levels: { [string]: number },
	prices: { [string]: number? }, -- nil when maxed
	pets: { [string]: number },
	equipped: { string },
	petCount: number,
}

local SimRules = {}
SimRules.__index = SimRules

export type Rules = typeof(setmetatable({} :: { config: any, econ: any }, SimRules))

--- Creates a rules object bound to a config table and the EconomyMath module.
function SimRules.new(config: any, econ: any): Rules
	return setmetatable({ config = config, econ = econ }, SimRules)
end

--- A fresh save for a new player.
function SimRules.newState(self: Rules): State
	return {
		wallet = { [self.config.currency] = 0 },
		levels = {},
		pets = {},
		rebirths = 0,
		carry = 0,
		totalMined = 0,
	}
end

--- The pet ids that count as equipped: the best `maxEquippedPets` individual pets
--- owned (duplicates count separately), highest bonus first, ties by id.
function SimRules.equippedPets(self: Rules, state: State): { string }
	local list = {}
	for id, count in state.pets do
		if self.config.pets[id] then
			for _ = 1, count do
				table.insert(list, id)
			end
		end
	end
	local pets = self.config.pets
	table.sort(list, function(a, b)
		if pets[a].bonus ~= pets[b].bonus then
			return pets[a].bonus > pets[b].bonus
		end
		return a < b
	end)
	local equipped = {}
	for i = 1, math.min(#list, self.config.maxEquippedPets) do
		equipped[i] = list[i]
	end
	return equipped
end

--- Total pets owned.
function SimRules.petCount(self: Rules, state: State): number
	local n = 0
	for _, count in state.pets do
		n += count
	end
	return n
end

--- Rebirth multiplier times pet multiplier.
function SimRules.multiplier(self: Rules, state: State): number
	local petBonus = 0
	for _, id in self:equippedPets(state) do
		petBonus += self.config.pets[id].bonus
	end
	local rebirth = self.econ.rebirthMultiplier(self.config.rebirth.multiplierPerRebirth, state.rebirths)
	return rebirth * (1 + petBonus)
end

--- Gems earned by one mine action (floored, at least 1).
function SimRules.perMine(self: Rules, state: State): number
	local up = self.config.upgrades.Pickaxe
	local base = self.config.baseClick + (state.levels.Pickaxe or 0) * up.effectPerLevel
	return math.max(1, math.floor(base * self:multiplier(state)))
end

--- Passive Gems per second (may be fractional; `tick` carries the remainder).
function SimRules.perSecond(self: Rules, state: State): number
	local up = self.config.upgrades.Drill
	local base = self.config.baseIncome + (state.levels.Drill or 0) * up.effectPerLevel
	return base * self:multiplier(state)
end

--- One mine action. Returns the Gems gained.
function SimRules.mine(self: Rules, state: State): number
	local gained = self:perMine(state)
	local c = self.config.currency
	state.wallet[c] = (state.wallet[c] or 0) + gained
	state.totalMined += gained
	return gained
end

--- Pays passive income for `dt` seconds. Returns the whole Gems paid.
function SimRules.tick(self: Rules, state: State, dt: number): number
	if not (dt > 0) or dt ~= dt then
		return 0
	end
	state.carry += self:perSecond(state) * dt
	local whole = math.floor(state.carry)
	state.carry -= whole
	local c = self.config.currency
	state.wallet[c] = (state.wallet[c] or 0) + whole
	return whole
end

--- Price of the next level of an upgrade, or nil when the id is unknown or maxed.
function SimRules.upgradePrice(self: Rules, state: State, upgradeId: string): number?
	local up = self.config.upgrades[upgradeId]
	if not up then
		return nil
	end
	local owned = state.levels[upgradeId] or 0
	if owned >= up.maxLevel then
		return nil
	end
	return self.econ.levelCost(up.baseCost, up.costGrowth, owned)
end

--- Buys one level, or as many as affordable when `max` is true.
--- Returns the number of levels bought (0 when nothing was bought).
function SimRules.buyUpgrade(self: Rules, state: State, upgradeId: unknown, max: unknown): number
	if type(upgradeId) ~= "string" then
		return 0
	end
	local up = self.config.upgrades[upgradeId]
	if not up then
		return 0
	end
	local owned = state.levels[upgradeId] or 0
	if owned >= up.maxLevel then
		return 0
	end
	local count, total
	if max == true then
		count, total = self.econ.maxAffordable(up.baseCost, up.costGrowth, owned, state.wallet[up.currency] or 0, up.maxLevel)
	else
		count, total = 1, self.econ.levelCost(up.baseCost, up.costGrowth, owned)
	end
	if count <= 0 then
		return 0
	end
	if not self.econ.trySpend(state.wallet, { [up.currency] = total }) then
		return 0
	end
	state.levels[upgradeId] = owned + count
	return count
end

--- Gems needed for the next rebirth.
function SimRules.rebirthRequirement(self: Rules, state: State): number
	local rb = self.config.rebirth
	return self.econ.rebirthRequirement(rb.baseRequirement, rb.requirementGrowth, state.rebirths)
end

--- Rebirths if the wallet holds the requirement: resets Gems and upgrades, keeps
--- pets. Returns true on success.
function SimRules.rebirth(self: Rules, state: State): boolean
	local c = self.config.currency
	if (state.wallet[c] or 0) < self:rebirthRequirement(state) then
		return false
	end
	state.wallet[c] = 0
	table.clear(state.levels)
	state.carry = 0
	state.rebirths += 1
	return true
end

--- Buys and opens an egg. `unit` is a uniform random number in [0, 1] from the
--- server's Random. Returns the pet id, or nil plus a reason.
function SimRules.openEgg(self: Rules, state: State, eggId: unknown, unit: number): (string?, string?)
	if type(eggId) ~= "string" then
		return nil, "invalid"
	end
	local egg = self.config.eggs[eggId]
	if not egg then
		return nil, "invalid"
	end
	if self:petCount(state) >= self.config.maxPetInventory then
		return nil, "inventory full"
	end
	if not self.econ.trySpend(state.wallet, egg.price) then
		return nil, "not enough Gems"
	end
	local petId = self.econ.pickWeighted(egg.outcomes, unit)
	state.pets[petId] = (state.pets[petId] or 0) + 1
	return petId, nil
end

--- Repairs loaded save data (never errors) and pays offline income from the
--- server clock. Returns the state, a list of repaired fields, and Gems paid offline.
function SimRules.repair(self: Rules, saved: any, now: number): (State, { string }, number)
	local econ = self.econ
	local notes = {}
	local data = if type(saved) == "table" then saved else {}
	local state = self:newState()

	local wallet = if type(data.wallet) == "table" then data.wallet else {}
	for currency, amount in wallet do
		if type(currency) == "string" then
			local fixed = econ.sanitizeAmount(amount)
			if fixed ~= amount then
				table.insert(notes, `wallet.{currency}`)
			end
			state.wallet[currency] = fixed
		end
	end

	local levels = if type(data.levels) == "table" then data.levels else {}
	for id, level in levels do
		local up = self.config.upgrades[id]
		if up then
			local fixed = econ.sanitizeLevel(level, up.maxLevel)
			if fixed ~= level then
				table.insert(notes, `levels.{id}`)
			end
			state.levels[id] = fixed
		else
			table.insert(notes, `dropped upgrade {tostring(id)}`)
		end
	end

	local pets = if type(data.pets) == "table" then data.pets else {}
	local total = 0
	for id, count in pets do
		if type(id) == "string" and self.config.pets[id] then
			local fixed = econ.sanitizeAmount(count)
			fixed = math.min(fixed, self.config.maxPetInventory - total)
			if fixed ~= count then
				table.insert(notes, `pets.{id}`)
			end
			if fixed > 0 then
				state.pets[id] = fixed
				total += fixed
			end
		else
			table.insert(notes, `dropped pet {tostring(id)}`)
		end
	end

	state.rebirths = econ.sanitizeAmount(data.rebirths)
	state.totalMined = econ.sanitizeAmount(data.totalMined)

	-- Offline income from the server clock only; a missing or future timestamp pays 0.
	local lastSeen = data.lastSeen
	if type(lastSeen) ~= "number" or not (lastSeen <= now) then
		lastSeen = now
	end
	local off = self.config.offline
	local paid = econ.offlineIncome(now - lastSeen, self:perSecond(state), off.maxHours, off.efficiency)
	local c = self.config.currency
	state.wallet[c] = (state.wallet[c] or 0) + paid
	return state, notes, paid
end

--- Plain table to store in the DataStore (no NaN, no fractions).
function SimRules.serialize(self: Rules, state: State, now: number): { [string]: any }
	local econ = self.econ
	local wallet = {}
	for k, v in state.wallet do
		wallet[k] = econ.sanitizeAmount(v)
	end
	return {
		schema = self.config.save.schema,
		wallet = wallet,
		levels = table.clone(state.levels),
		pets = table.clone(state.pets),
		rebirths = econ.sanitizeAmount(state.rebirths),
		totalMined = econ.sanitizeAmount(state.totalMined),
		lastSeen = now,
	}
end

--- Everything the HUD needs, computed on the server.
function SimRules.snapshot(self: Rules, state: State): Snapshot
	local prices = {}
	for id in self.config.upgrades do
		prices[id] = self:upgradePrice(state, id)
	end
	return {
		gems = state.wallet[self.config.currency] or 0,
		perSecond = self:perSecond(state),
		perMine = self:perMine(state),
		multiplier = self:multiplier(state),
		rebirths = state.rebirths,
		rebirthRequirement = self:rebirthRequirement(state),
		levels = table.clone(state.levels),
		prices = prices,
		pets = table.clone(state.pets),
		equipped = self:equippedPets(state),
		petCount = self:petCount(state),
	}
end

return SimRules
