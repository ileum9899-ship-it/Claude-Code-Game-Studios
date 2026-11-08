--!strict
-- Crystal Cavern Simulator — server entry point.
-- Owns every player's State. Remotes carry intent only ("mine", "buy Pickaxe");
-- every amount and price is computed here from Config.

local DataStoreService = game:GetService("DataStoreService")
local Players = game:GetService("Players")
local ReplicatedStorage = game:GetService("ReplicatedStorage")
local RunService = game:GetService("RunService")

local Shared = ReplicatedStorage:WaitForChild("Shared")
local Config = require(Shared:WaitForChild("Config"))
local EconomyMath = require(Shared:WaitForChild("EconomyMath"))
local SimRules = require(Shared:WaitForChild("SimRules"))
local PlayerData = require(script.Parent:WaitForChild("PlayerData"))
local RateLimiter = require(script.Parent:WaitForChild("RateLimiter"))
local World = require(script.Parent:WaitForChild("World"))

local Remotes = ReplicatedStorage:WaitForChild("Remotes")
local MineRemote = Remotes:WaitForChild("Mine") :: RemoteEvent
local BuyUpgradeRemote = Remotes:WaitForChild("BuyUpgrade") :: RemoteEvent
local RebirthRemote = Remotes:WaitForChild("Rebirth") :: RemoteEvent
local OpenEggRemote = Remotes:WaitForChild("OpenEgg") :: RemoteEvent
local StateChanged = Remotes:WaitForChild("StateChanged") :: RemoteEvent
local Notify = Remotes:WaitForChild("Notify") :: RemoteEvent

local rules = SimRules.new(Config, EconomyMath)
local rng = Random.new()
local mineLimiter = RateLimiter.new(Config.rateLimits.mine.capacity, Config.rateLimits.mine.refillPerSecond)
local actionLimiter = RateLimiter.new(Config.rateLimits.action.capacity, Config.rateLimits.action.refillPerSecond)

local saves = PlayerData.new({
	store = DataStoreService:GetDataStore(Config.save.storeName),
	jobId = if game.JobId ~= "" then game.JobId else "studio",
	now = os.time,
	wait = function(seconds: number)
		task.wait(seconds)
	end,
	lockStaleSeconds = Config.save.lockStaleSeconds,
	loadAttempts = Config.save.loadAttempts,
	warn = warn,
})

type Session = {
	state: SimRules.State,
	key: string,
	persistent: boolean, -- false when playing on unsaved fallback data (Studio only)
}
local sessions: { [Player]: Session } = {}

local function keyFor(player: Player): string
	return "Player_" .. player.UserId
end

local function push(player: Player, session: Session)
	local snap = rules:snapshot(session.state)
	StateChanged:FireClient(player, snap)
	local stats = player:FindFirstChild("leaderstats")
	if stats then
		local gems = stats:FindFirstChild("Gems") :: StringValue
		local rebirths = stats:FindFirstChild("Rebirths") :: IntValue
		gems.Value = EconomyMath.formatNumber(snap.gems)
		rebirths.Value = snap.rebirths
	end
end

local function makeLeaderstats(player: Player)
	local stats = Instance.new("Folder")
	stats.Name = "leaderstats"
	local gems = Instance.new("StringValue")
	gems.Name = "Gems"
	gems.Value = "0"
	gems.Parent = stats
	local rebirths = Instance.new("IntValue")
	rebirths.Name = "Rebirths"
	rebirths.Parent = stats
	stats.Parent = player
end

local function saveSession(player: Player, session: Session, release: boolean)
	if not session.persistent then
		return
	end
	saves:save(session.key, rules:serialize(session.state, os.time()), release)
end

-- Mining is shared by the HUD button and clicking a crystal in the world.
local function tryMine(player: Player): boolean
	local session = sessions[player]
	if not session or not mineLimiter:allow(player) then
		return false
	end
	rules:mine(session.state)
	push(player, session)
	return true
end

local function onPlayerAdded(player: Player)
	makeLeaderstats(player)
	local key = keyFor(player)
	local ok, saved, reason = saves:load(key)

	if not player:IsDescendantOf(Players) then
		-- Left while loading: give the lock back without changing their data.
		if ok then
			saves:save(key, saved, true)
		end
		return
	end

	local persistent = true
	if not ok then
		if RunService:IsStudio() then
			warn(`[Save] Load failed ({reason}). Playing on unsaved data. Enable Studio API access to test saving.`)
			persistent = false
			saved = nil
		else
			player:Kick("We couldn't load your save safely. Please rejoin in a minute.")
			return
		end
	end

	local state, notes, offlinePaid = rules:repair(saved, os.time())
	if #notes > 0 then
		warn(`[Save] Repaired {player.Name}: {table.concat(notes, ", ")}`)
	end
	local session: Session = { state = state, key = key, persistent = persistent }
	sessions[player] = session
	push(player, session)
	if offlinePaid > 0 then
		Notify:FireClient(player, "offline", offlinePaid)
	end
end

Players.PlayerAdded:Connect(onPlayerAdded)
for _, player in Players:GetPlayers() do
	task.spawn(onPlayerAdded, player) -- joined before this script connected (Studio)
end

Players.PlayerRemoving:Connect(function(player: Player)
	local session = sessions[player]
	sessions[player] = nil
	mineLimiter:forget(player)
	actionLimiter:forget(player)
	if session then
		saveSession(player, session, true)
	end
end)

game:BindToClose(function()
	local pending = 0
	for player, session in sessions do
		pending += 1
		task.spawn(function()
			saveSession(player, session, true)
			pending -= 1
		end)
	end
	while pending > 0 do
		task.wait()
	end
end)

MineRemote.OnServerEvent:Connect(function(player: Player)
	tryMine(player)
end)

BuyUpgradeRemote.OnServerEvent:Connect(function(player: Player, upgradeId: unknown, max: unknown)
	local session = sessions[player]
	if not session or not actionLimiter:allow(player) then
		return
	end
	if rules:buyUpgrade(session.state, upgradeId, max) > 0 then
		push(player, session)
	end
end)

RebirthRemote.OnServerEvent:Connect(function(player: Player)
	local session = sessions[player]
	if not session or not actionLimiter:allow(player) then
		return
	end
	if rules:rebirth(session.state) then
		push(player, session)
		Notify:FireClient(player, "rebirth", session.state.rebirths)
	end
end)

OpenEggRemote.OnServerEvent:Connect(function(player: Player, eggId: unknown)
	local session = sessions[player]
	if not session or not actionLimiter:allow(player) then
		return
	end
	local petId, reason = rules:openEgg(session.state, eggId, rng:NextNumber())
	if petId then
		push(player, session)
		Notify:FireClient(player, "pet", petId)
	elseif reason and reason ~= "invalid" then
		Notify:FireClient(player, "error", reason)
	end
end)

World.build(Config.world, function(player: Player, _crystal: BasePart): boolean
	return tryMine(player)
end)

-- Passive income, once per second.
task.spawn(function()
	while true do
		local elapsed = task.wait(1)
		for player, session in sessions do
			if rules:tick(session.state, elapsed) > 0 then
				push(player, session)
			end
		end
	end
end)

-- Autosave (also refreshes the session lock so it never goes stale while playing).
task.spawn(function()
	while true do
		task.wait(Config.save.autosaveSeconds)
		for player, session in sessions do
			task.spawn(saveSession, player, session, false)
		end
	end
end)
