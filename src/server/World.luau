--!strict
-- Builds the ring of mineable crystals from Config.world. The cavern floor,
-- spawn and lighting are authored in default.project.json; only the repeated
-- crystals are placed by code. Crystals are tagged "Crystal" for discovery.

local CollectionService = game:GetService("CollectionService")
local TweenService = game:GetService("TweenService")
local Workspace = game:GetService("Workspace")

local World = {}

local CRYSTAL_COLORS = {
	Color3.fromRGB(120, 200, 255),
	Color3.fromRGB(190, 120, 255),
	Color3.fromRGB(110, 255, 200),
	Color3.fromRGB(255, 130, 200),
}

--- Places the crystals and calls `onMine(player, crystal)` when one is clicked.
function World.build(worldConfig: any, onMine: (Player, BasePart) -> boolean)
	local folder = Instance.new("Folder")
	folder.Name = "Crystals"

	local rng = Random.new(worldConfig.seed)
	for i = 1, worldConfig.crystalCount do
		local angle = (i / worldConfig.crystalCount) * math.pi * 2 + rng:NextNumber(-0.1, 0.1)
		local radius = rng:NextNumber(worldConfig.ringInner, worldConfig.ringOuter)
		local height = rng:NextNumber(5, 9)

		local crystal = Instance.new("Part")
		crystal.Name = "Crystal" .. i
		crystal.Anchored = true
		crystal.Material = Enum.Material.Neon
		crystal.Color = CRYSTAL_COLORS[(i - 1) % #CRYSTAL_COLORS + 1]
		crystal.Size = Vector3.new(3, height, 3)
		crystal.CFrame = CFrame.new(math.cos(angle) * radius, height / 2, math.sin(angle) * radius)
			* CFrame.Angles(0, rng:NextNumber(0, math.pi), math.rad(rng:NextNumber(-12, 12)))
		crystal:SetAttribute("BaseSize", crystal.Size)

		local detector = Instance.new("ClickDetector")
		detector.MaxActivationDistance = worldConfig.clickDistance
		detector.Parent = crystal

		local light = Instance.new("PointLight")
		light.Color = crystal.Color
		light.Range = 10
		light.Brightness = 1.5
		light.Parent = crystal

		detector.MouseClick:Connect(function(player: Player)
			if onMine(player, crystal) then
				World.pulse(crystal)
			end
		end)

		CollectionService:AddTag(crystal, "Crystal")
		crystal.Parent = folder
	end

	folder.Parent = Workspace
	return folder
end

--- Short squash so a successful mine is visible to everyone nearby.
function World.pulse(crystal: BasePart)
	local base = crystal:GetAttribute("BaseSize")
	if typeof(base) ~= "Vector3" then
		return
	end
	crystal.Size = base * Vector3.new(1.15, 0.9, 1.15)
	TweenService:Create(crystal, TweenInfo.new(0.15, Enum.EasingStyle.Back), { Size = base }):Play()
end

return World
