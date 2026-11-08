--!strict
-- Per-key token bucket. Pure Luau; the clock is injected so tests are deterministic.

local RateLimiter = {}
RateLimiter.__index = RateLimiter

export type Limiter = typeof(setmetatable(
	{} :: { capacity: number, refill: number, clock: () -> number, buckets: { [any]: { tokens: number, at: number } } },
	RateLimiter
))

--- Creates a limiter that allows bursts of `capacity` and refills `refillPerSecond`.
function RateLimiter.new(capacity: number, refillPerSecond: number, clock: (() -> number)?): Limiter
	return setmetatable({
		capacity = capacity,
		refill = refillPerSecond,
		clock = clock or os.clock,
		buckets = {},
	}, RateLimiter)
end

--- Spends one token for `key`. Returns false when the bucket is empty.
function RateLimiter.allow(self: Limiter, key: any): boolean
	local now = self.clock()
	local b = self.buckets[key]
	if not b then
		b = { tokens = self.capacity, at = now }
		self.buckets[key] = b
	end
	b.tokens = math.min(self.capacity, b.tokens + (now - b.at) * self.refill)
	b.at = now
	if b.tokens < 1 then
		return false
	end
	b.tokens -= 1
	return true
end

--- Forgets `key` (call when a player leaves).
function RateLimiter.forget(self: Limiter, key: any)
	self.buckets[key] = nil
end

return RateLimiter
