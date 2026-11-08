--!strict
-- Crystal Cavern Simulator — HUD. Display and intent only: it shows the snapshot
-- the server pushes and sends requests ("buy Pickaxe"); it never decides amounts.

local ContextActionService = game:GetService("ContextActionService")
local Players = game:GetService("Players")
local ReplicatedStorage = game:GetService("ReplicatedStorage")
local TweenService = game:GetService("TweenService")

local Shared = ReplicatedStorage:WaitForChild("Shared")
local Config = require(Shared:WaitForChild("Config"))
local EconomyMath = require(Shared:WaitForChild("EconomyMath"))

local Remotes = ReplicatedStorage:WaitForChild("Remotes")
local MineRemote = Remotes:WaitForChild("Mine") :: RemoteEvent
local BuyUpgradeRemote = Remotes:WaitForChild("BuyUpgrade") :: RemoteEvent
local RebirthRemote = Remotes:WaitForChild("Rebirth") :: RemoteEvent
local OpenEggRemote = Remotes:WaitForChild("OpenEgg") :: RemoteEvent
local StateChanged = Remotes:WaitForChild("StateChanged") :: RemoteEvent
local Notify = Remotes:WaitForChild("Notify") :: RemoteEvent

local player = Players.LocalPlayer
local fmt = EconomyMath.formatNumber

local THEME = {
	panel = Color3.fromRGB(24, 22, 40),
	button = Color3.fromRGB(70, 150, 255),
	buttonAlt = Color3.fromRGB(150, 90, 255),
	good = Color3.fromRGB(80, 220, 140),
	disabled = Color3.fromRGB(70, 70, 90),
	text = Color3.fromRGB(240, 240, 255),
	muted = Color3.fromRGB(170, 170, 200),
}

-- Small builders ------------------------------------------------------------

local function corner(parent: Instance, radius: number?)
	local c = Instance.new("UICorner")
	c.CornerRadius = UDim.new(0, radius or 10)
	c.Parent = parent
end

local function pad(parent: Instance, px: number)
	local p = Instance.new("UIPadding")
	p.PaddingTop, p.PaddingBottom = UDim.new(0, px), UDim.new(0, px)
	p.PaddingLeft, p.PaddingRight = UDim.new(0, px), UDim.new(0, px)
	p.Parent = parent
end

local function list(parent: Instance, gap: number, horizontal: boolean?)
	local l = Instance.new("UIListLayout")
	l.Padding = UDim.new(0, gap)
	l.SortOrder = Enum.SortOrder.LayoutOrder
	l.FillDirection = if horizontal then Enum.FillDirection.Horizontal else Enum.FillDirection.Vertical
	l.Parent = parent
end

local function label(parent: Instance, text: string, size: number, order: number?): TextLabel
	local t = Instance.new("TextLabel")
	t.BackgroundTransparency = 1
	t.Size = UDim2.new(1, 0, 0, size + 6)
	t.Font = Enum.Font.GothamBold
	t.TextSize = size
	t.TextColor3 = THEME.text
	t.TextXAlignment = Enum.TextXAlignment.Left
	t.TextWrapped = true
	t.Text = text
	t.LayoutOrder = order or 0
	t.Parent = parent
	return t
end

local function button(parent: Instance, text: string, size: UDim2, color: Color3, onClick: () -> ()): TextButton
	local b = Instance.new("TextButton")
	b.Size = size
	b.BackgroundColor3 = color
	b.AutoButtonColor = true
	b.Font = Enum.Font.GothamBold
	b.TextSize = 16
	b.TextColor3 = THEME.text
	b.TextWrapped = true
	b.Text = text
	b.Parent = parent
	corner(b, 8)
	b.Activated:Connect(onClick)
	return b
end

-- Layout ----------------------------------------------------------------------

local gui = Instance.new("ScreenGui")
gui.Name = "CrystalHud"
gui.ResetOnSpawn = false
gui.IgnoreGuiInset = false
gui.ZIndexBehavior = Enum.ZIndexBehavior.Sibling

-- Top-left stats.
local stats = Instance.new("Frame")
stats.Name = "Stats"
stats.Position = UDim2.fromOffset(12, 12)
stats.Size = UDim2.fromOffset(260, 0)
stats.AutomaticSize = Enum.AutomaticSize.Y
stats.BackgroundColor3 = THEME.panel
stats.BackgroundTransparency = 0.15
stats.Parent = gui
corner(stats)
pad(stats, 10)
list(stats, 2)
local gemsLabel = label(stats, "💎 0", 26, 1)
local rateLabel = label(stats, "+0 / s   ·   +1 / mine", 15, 2)
rateLabel.TextColor3 = THEME.muted
local multLabel = label(stats, "x1.00 multiplier", 15, 3)
multLabel.TextColor3 = THEME.muted

-- Bottom-centre mine button.
local mineButton = button(gui, "⛏  MINE", UDim2.fromOffset(200, 64), THEME.good, function()
	MineRemote:FireServer()
end)
mineButton.Name = "MineButton"
mineButton.AnchorPoint = Vector2.new(0.5, 1)
mineButton.Position = UDim2.new(0.5, 0, 1, -24)
mineButton.TextSize = 26

-- Right-side shop panel.
local shop = Instance.new("ScrollingFrame")
shop.Name = "Shop"
shop.AnchorPoint = Vector2.new(1, 0)
shop.Position = UDim2.new(1, -12, 0, 12)
shop.Size = UDim2.new(0, 300, 1, -110)
shop.CanvasSize = UDim2.new()
shop.AutomaticCanvasSize = Enum.AutomaticSize.Y
shop.ScrollBarThickness = 6
shop.BackgroundColor3 = THEME.panel
shop.BackgroundTransparency = 0.15
shop.BorderSizePixel = 0
shop.Parent = gui
corner(shop)
pad(shop, 10)
list(shop, 8)

local function section(title: string, order: number)
	local t = label(shop, title, 18, order)
	t.TextColor3 = THEME.muted
end

local function card(order: number): Frame
	local f = Instance.new("Frame")
	f.Size = UDim2.new(1, -6, 0, 0)
	f.AutomaticSize = Enum.AutomaticSize.Y
	f.BackgroundColor3 = Color3.fromRGB(40, 36, 64)
	f.LayoutOrder = order
	f.Parent = shop
	corner(f, 8)
	pad(f, 8)
	list(f, 6)
	return f
end

local function buttonRow(parent: Instance, order: number): Frame
	local row = Instance.new("Frame")
	row.BackgroundTransparency = 1
	row.Size = UDim2.new(1, 0, 0, 36)
	row.LayoutOrder = order
	row.Parent = parent
	list(row, 6, true)
	return row
end

-- Upgrades.
section("UPGRADES", 1)
type UpgradeWidgets = { title: TextLabel, buy: TextButton, max: TextButton }
local upgradeWidgets: { [string]: UpgradeWidgets } = {}
for i, id in Config.upgradeOrder do
	local up = Config.upgrades[id]
	local f = card(1 + i)
	local title = label(f, up.displayName, 17, 1)
	local desc = label(f, up.description, 13, 2)
	desc.TextColor3 = THEME.muted
	local row = buttonRow(f, 3)
	local buy = button(row, "Buy", UDim2.new(0.62, -3, 1, 0), THEME.button, function()
		BuyUpgradeRemote:FireServer(id, false)
	end)
	local max = button(row, "Max", UDim2.new(0.38, -3, 1, 0), THEME.buttonAlt, function()
		BuyUpgradeRemote:FireServer(id, true)
	end)
	upgradeWidgets[id] = { title = title, buy = buy, max = max }
end

-- Rebirth.
section("REBIRTH", 10)
local rebirthCard = card(11)
local rebirthInfo = label(rebirthCard, "", 14, 1)
local rebirthButton = button(rebirthCard, "Rebirth", UDim2.new(1, 0, 0, 36), THEME.buttonAlt, function()
	RebirthRemote:FireServer()
end)
rebirthButton.LayoutOrder = 2

-- Eggs, with the odds visible behind a labelled Details toggle.
section("EGGS", 20)
local eggButtons: { [string]: TextButton } = {}
for i, id in Config.eggOrder do
	local egg = Config.eggs[id]
	local f = card(20 + i)
	label(f, egg.displayName, 17, 1)
	local row = buttonRow(f, 2)
	local price = egg.price[Config.currency] or 0
	eggButtons[id] = button(row, `Open · 💎 {fmt(price)}`, UDim2.new(0.62, -3, 1, 0), THEME.button, function()
		OpenEggRemote:FireServer(id)
	end)

	local odds = EconomyMath.oddsPercent(egg.outcomes)
	local lines = {}
	for _, outcome in egg.outcomes do
		local pet = Config.pets[outcome.id]
		table.insert(lines, `{pet.displayName} ({pet.rarity}, +{math.floor(pet.bonus * 100)}%): {string.format("%.1f", odds[outcome.id])}%`)
	end
	local oddsLabel = label(f, table.concat(lines, "\n"), 13, 3)
	oddsLabel.AutomaticSize = Enum.AutomaticSize.Y
	oddsLabel.Size = UDim2.new(1, 0, 0, 0)
	oddsLabel.TextColor3 = THEME.muted
	oddsLabel.Visible = false
	local details: TextButton
	details = button(row, "Details", UDim2.new(0.38, -3, 1, 0), THEME.disabled, function()
		oddsLabel.Visible = not oddsLabel.Visible
		details.Text = if oddsLabel.Visible then "Hide" else "Details"
	end)
end

-- Pets.
section("PETS", 40)
local petCard = card(41)
local petInfo = label(petCard, "No pets yet — open an egg!", 14, 1)
petInfo.AutomaticSize = Enum.AutomaticSize.Y
petInfo.Size = UDim2.new(1, 0, 0, 0)

-- Toast.
local toast = Instance.new("TextLabel")
toast.Name = "Toast"
toast.AnchorPoint = Vector2.new(0.5, 0)
toast.Position = UDim2.new(0.5, 0, 0, 20)
toast.Size = UDim2.fromOffset(360, 48)
toast.BackgroundColor3 = THEME.panel
toast.Font = Enum.Font.GothamBold
toast.TextSize = 20
toast.TextColor3 = THEME.text
toast.TextWrapped = true
toast.Visible = false
toast.Parent = gui
corner(toast)

local toastSerial = 0
local function showToast(text: string, color: Color3?)
	toastSerial += 1
	local mine = toastSerial
	toast.Text = text
	toast.BackgroundColor3 = color or THEME.panel
	toast.Visible = true
	task.delay(2.5, function()
		if toastSerial == mine then
			toast.Visible = false
		end
	end)
end

-- Phones: a narrower shop that leaves room for the thumbstick and jump button.
local camera = workspace.CurrentCamera
local function adapt()
	local w = camera.ViewportSize.X
	shop.Size = if w < 700 then UDim2.new(0, 220, 1, -110) else UDim2.new(0, 300, 1, -110)
end
camera:GetPropertyChangedSignal("ViewportSize"):Connect(adapt)
adapt()

gui.Parent = player:WaitForChild("PlayerGui")

-- State -> UI --------------------------------------------------------------

local lastGems = 0
local function setEnabled(b: TextButton, on: boolean, color: Color3)
	b.BackgroundColor3 = if on then color else THEME.disabled
end

StateChanged.OnClientEvent:Connect(function(snap: any)
	if type(snap) ~= "table" then
		return
	end
	local gems = snap.gems or 0
	gemsLabel.Text = `💎 {fmt(gems)}`
	rateLabel.Text = `+{fmt(math.floor(snap.perSecond or 0))} / s   ·   +{fmt(snap.perMine or 1)} / mine`
	multLabel.Text = string.format("x%.2f multiplier · %d rebirths", snap.multiplier or 1, snap.rebirths or 0)
	if gems > lastGems then
		gemsLabel.TextSize = 30
		TweenService:Create(gemsLabel, TweenInfo.new(0.12), { TextSize = 26 }):Play()
	end
	lastGems = gems

	for id, w in upgradeWidgets do
		local level = (snap.levels and snap.levels[id]) or 0
		local price = snap.prices and snap.prices[id]
		w.title.Text = `{Config.upgrades[id].displayName}  ·  Lv {level}`
		if price then
			w.buy.Text = `Buy · 💎 {fmt(price)}`
			setEnabled(w.buy, gems >= price, THEME.button)
			setEnabled(w.max, gems >= price, THEME.buttonAlt)
		else
			w.buy.Text = "MAXED"
			setEnabled(w.buy, false, THEME.button)
			setEnabled(w.max, false, THEME.buttonAlt)
		end
	end

	local req = snap.rebirthRequirement or 0
	local nextMult = EconomyMath.rebirthMultiplier(Config.rebirth.multiplierPerRebirth, (snap.rebirths or 0) + 1)
	rebirthInfo.Text = `Need 💎 {fmt(req)}. Resets Gems and upgrades, keeps pets.\nRebirth multiplier becomes x{string.format("%.1f", nextMult)}.`
	setEnabled(rebirthButton, gems >= req, THEME.buttonAlt)

	for id, b in eggButtons do
		local price = Config.eggs[id].price[Config.currency] or 0
		setEnabled(b, gems >= price and (snap.petCount or 0) < Config.maxPetInventory, THEME.button)
	end

	local lines = { `Owned {snap.petCount or 0}/{Config.maxPetInventory} · best {Config.maxEquippedPets} are equipped` }
	for _, petId in snap.equipped or {} do
		local pet = Config.pets[petId]
		if pet then
			table.insert(lines, `★ {pet.displayName} +{math.floor(pet.bonus * 100)}%`)
		end
	end
	petInfo.Text = table.concat(lines, "\n")
end)

Notify.OnClientEvent:Connect(function(kind: unknown, value: unknown)
	if kind == "pet" and type(value) == "string" and Config.pets[value] then
		local pet = Config.pets[value]
		showToast(`You hatched a {pet.rarity} {pet.displayName}!`, THEME.buttonAlt)
	elseif kind == "offline" and type(value) == "number" then
		showToast(`Your drills mined 💎 {fmt(value)} while you were away`, THEME.good)
	elseif kind == "rebirth" and type(value) == "number" then
		showToast(`Rebirth {value}! Everything earns more now.`, THEME.buttonAlt)
	elseif kind == "error" and type(value) == "string" then
		showToast(value, Color3.fromRGB(200, 70, 80))
	end
end)

-- Keyboard / gamepad shortcut for mining (E or gamepad X).
ContextActionService:BindAction("CrystalMine", function(_, inputState)
	if inputState == Enum.UserInputState.Begin then
		MineRemote:FireServer()
	end
	return Enum.ContextActionResult.Sink
end, false, Enum.KeyCode.E, Enum.KeyCode.ButtonX)
