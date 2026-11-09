"use strict";
// ---------------------------------------------------------------- util
const TAU = Math.PI * 2;
const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
const lerp = (a, b, t) => a + (b - a) * t;
const smooth = t => t * t * (3 - 2 * t);
const damp = (a, b, k, dt) => lerp(a, b, 1 - Math.exp(-k * dt));
const angDiff = (a, b) => { let d = (b - a) % TAU; if (d > Math.PI) d -= TAU; if (d < -Math.PI) d += TAU; return d; };
function mulberry32(seed) {
  return function () {
    seed |= 0; seed = seed + 0x6D2B79F5 | 0;
    let t = Math.imul(seed ^ seed >>> 15, 1 | seed);
    t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  };
}
const RNG = mulberry32(1337);
const rand = (a = 0, b = 1) => a + (b - a) * RNG();
const randi = (a, b) => Math.floor(rand(a, b + 1));
const pick = arr => arr[Math.floor(RNG() * arr.length)];
// sRGB hex -> linear THREE.Color (we tone-map + gamma in the final pass)
const col = hex => new THREE.Color(hex).convertSRGBToLinear();
