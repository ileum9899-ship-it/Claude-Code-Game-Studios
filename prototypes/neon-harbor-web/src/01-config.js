// ---------------------------------------------------------------- config
// Mirrors Assets/NeonHarbor/Resources/Data/harbor_balance.json (Unity build).
const CFG = {
  city: { blocks: 8, block: 48, road: 12, sidewalk: 3.5, seed: 1337 },
  player: { walk: 2.4, run: 6.2, jump: 5.2, gravity: 18, radius: 0.4, maxHealth: 100 },
  car: { accel: 13, reverseAccel: 7, brake: 26, maxSpeed: 52, maxReverse: 11, drag: 0.0042, rolling: 1.3,
         grip: 9.5, handbrakeGrip: 1.4, steerMax: 0.6, steerSpeed: 3.2, wheelbase: 2.7, radius: 1.15 },
  traffic: { count: 34, parked: 46, cruise: 12, turnSpeed: 6, followGap: 9, lightCycle: 18, yellow: 2.5 },
  peds: { count: 46, radius: 70, speed: 1.35, fleeSpeed: 5.2, hitSpeed: 4 },
  wanted: { pedHit: 1, carjack: 1, copHit: 1, maxStars: 5, evadeRadius: 85, evadeSeconds: 9, bustRadius: 7,
            bustSeconds: 2.5, bustFine: 0.1, copSpeed: 1.25, copsPerStar: 1, maxCops: 5 },
  missions: { courierReward: [250, 900], raceReward: 1500, timePerMeter: 0.085, timeBase: 25 },
  cash: { pickups: 24, value: [40, 160] },
  dayLength: 720,
};
