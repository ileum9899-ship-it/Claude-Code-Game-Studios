--!strict
-- Loads and saves one record per player with a session lock, so two servers
-- never both own the same save (rejoin races, teleports).
--
-- Dependencies are injected: `store` is anything with UpdateAsync(key, fn)
-- (a real DataStore, or a fake in tests/unit/player_data), `jobId` identifies
-- this server, `now` is the server clock and `wait` yields between retries.

local PlayerData = {}
PlayerData.__index = PlayerData

export type Deps = {
	store: any,
	jobId: string,
	now: () -> number,
	wait: (number) -> (),
	lockStaleSeconds: number,
	loadAttempts: number,
	warn: ((...any) -> ())?,
}

export type Store = typeof(setmetatable({} :: Deps, PlayerData))

--- Creates a save store around a DataStore-like object.
function PlayerData.new(deps: Deps): Store
	return setmetatable(table.clone(deps), PlayerData)
end

local function lockIsLive(self: Store, record: any): boolean
	if type(record) ~= "table" or type(record.lock) ~= "table" then
		return false
	end
	local lock = record.lock
	if lock.jobId == self.jobId then
		return false -- our own lock
	end
	return type(lock.at) == "number" and self.now() - lock.at < self.lockStaleSeconds
end

--- Claims the lock and returns the saved data (nil for a new player).
--- Returns ok=false with a reason when the DataStore failed or another live
--- server still holds the lock after every attempt.
function PlayerData.load(self: Store, key: string): (boolean, any, string?)
	local lastReason = "unknown"
	for attempt = 1, self.loadAttempts do
		local lockedElsewhere = false
		local loaded = nil
		local ok, err = pcall(function()
			self.store:UpdateAsync(key, function(record: any)
				-- Must not yield; may run more than once.
				lockedElsewhere = false
				loaded = nil
				if lockIsLive(self, record) then
					lockedElsewhere = true
					return nil -- cancel the write
				end
				local out = if type(record) == "table" then table.clone(record) else {}
				loaded = if type(record) == "table" then record.data else nil
				out.lock = { jobId = self.jobId, at = self.now() }
				return out
			end)
		end)
		if ok and not lockedElsewhere then
			return true, loaded, nil
		end
		lastReason = if ok then "locked by another server" else tostring(err)
		if attempt < self.loadAttempts then
			self.wait(2 ^ attempt) -- 2s, 4s, 8s ...
		end
	end
	return false, nil, lastReason
end

--- Writes `data` if this server still owns the lock. `release` clears the lock
--- (final save on leave/shutdown). Returns true when the write landed.
function PlayerData.save(self: Store, key: string, data: any, release: boolean): boolean
	local wrote = false
	local ok, err = pcall(function()
		self.store:UpdateAsync(key, function(record: any)
			wrote = false
			if lockIsLive(self, record) then
				return nil -- someone else owns it now; never clobber
			end
			wrote = true
			return {
				data = data,
				lock = if release then nil else { jobId = self.jobId, at = self.now() },
			}
		end)
	end)
	if not ok then
		if self.warn then
			self.warn("Save failed for", key, err)
		end
		return false
	end
	return wrote
end

return PlayerData
