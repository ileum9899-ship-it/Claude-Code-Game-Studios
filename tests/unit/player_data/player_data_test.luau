-- Unit tests for PlayerData session locking and RateLimiter, against a fake
-- DataStore. Run from the repo root:
--   lune run tests/unit/player_data/player_data_test.luau

local process = require("@lune/process")

local PlayerData = require("../../../src/server/PlayerData")
local RateLimiter = require("../../../src/server/RateLimiter")

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

-- Fake DataStore: UpdateAsync semantics (nil return cancels the write).
local function fakeStore(failTimes: number?)
	local data: { [string]: any } = {}
	local fails = failTimes or 0
	return {
		data = data,
		UpdateAsync = function(_self, key: string, fn)
			if fails > 0 then
				fails -= 1
				error("simulated DataStore outage")
			end
			local new = fn(data[key])
			if new ~= nil then
				data[key] = new
			end
			return new
		end,
	}
end

local clock = 1000
local function make(store, jobId: string)
	return PlayerData.new({
		store = store,
		jobId = jobId,
		now = function()
			return clock
		end,
		wait = function() end,
		lockStaleSeconds = 300,
		loadAttempts = 3,
	})
end

test("new player loads nil and takes the lock", function()
	local store = fakeStore()
	local ok, data = make(store, "A"):load("k")
	eq(ok, true, "ok")
	eq(data, nil, "no data")
	eq(store.data.k.lock.jobId, "A", "locked")
end)

test("save then release, then another server can load", function()
	local store = fakeStore()
	local a, b = make(store, "A"), make(store, "B")
	a:load("k")
	eq(a:save("k", { gems = 5 }, false), true, "autosave")
	eq(b:load("k"), false, "B blocked while A holds a live lock")
	eq(a:save("k", { gems = 6 }, true), true, "final save")
	local ok, data = b:load("k")
	eq(ok, true, "B loads after release")
	eq(data.gems, 6, "latest data")
end)

test("a stale lock can be taken over", function()
	local store = fakeStore()
	make(store, "A"):load("k")
	clock += 301
	eq((make(store, "B"):load("k")), true, "stale lock taken")
	clock -= 301
end)

test("a server that lost its lock never clobbers", function()
	local store = fakeStore()
	local a = make(store, "A")
	a:load("k")
	clock += 301
	make(store, "B"):load("k")
	eq(a:save("k", { gems = 999 }, true), false, "A refused")
	eq(store.data.k.lock.jobId, "B", "B still owns")
	clock -= 301
end)

test("transient failures are retried", function()
	local store = fakeStore(2)
	eq((make(store, "A"):load("k")), true, "succeeds on third attempt")
end)

test("persistent failures report not ok", function()
	local store = fakeStore(10)
	local ok, _, reason = make(store, "A"):load("k")
	eq(ok, false, "not ok")
	eq(type(reason), "string", "reason given")
end)

test("rate limiter allows a burst then refills", function()
	local t = 0
	local lim = RateLimiter.new(3, 2, function()
		return t
	end)
	eq(lim:allow("p"), true)
	eq(lim:allow("p"), true)
	eq(lim:allow("p"), true)
	eq(lim:allow("p"), false, "burst spent")
	t = 0.5
	eq(lim:allow("p"), true, "one token after 0.5s at 2/s")
	eq(lim:allow("p"), false)
	eq(lim:allow("q"), true, "keys are independent")
end)

print(`{passed} passed, {failed} failed`)
process.exit(if failed > 0 then 1 else 0)
