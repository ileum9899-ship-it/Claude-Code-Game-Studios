// ---------------------------------------------------------------- city layout (deterministic)
// Grid: roads on lines k*PITCH (k = 0..N), blocks between. Y up, metres.
const CITY = (() => {
  const N = CFG.city.blocks, B = CFG.city.block, R = CFG.city.road, SW = CFG.city.sidewalk;
  const PITCH = B + R, SIZE = N * PITCH, HALF_R = R / 2;
  const rnd = mulberry32(CFG.city.seed);
  const blocks = [];
  const colliders = []; // {x0,z0,x1,z1,h}
  const center = SIZE / 2;
  for (let j = 0; j < N; j++) for (let i = 0; i < N; i++) {
    const x0 = i * PITCH + HALF_R, z0 = j * PITCH + HALF_R, x1 = x0 + B, z1 = z0 + B;
    const cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
    const d = Math.hypot(cx - center, cz - center) / (SIZE * 0.5); // 0 centre .. ~1.4 corner
    let type;
    if ((i === 3 && j === 4) || (i === 6 && j === 1) || (i === 1 && j === 6)) type = "park";
    else if (i === N - 1 && j <= 2) type = "harbor";
    else if (d < 0.38) type = "downtown";
    else if (d < 0.75) type = "midtown";
    else type = "residential";
    const blk = { i, j, x0, z0, x1, z1, cx, cz, type, buildings: [] };
    if (type === "downtown" || type === "midtown" || type === "residential") {
      // subdivide the lot (inside the sidewalk) into 2-3 x 2-3 parcels
      const lx0 = x0 + SW + 1, lz0 = z0 + SW + 1, lx1 = x1 - SW - 1, lz1 = z1 - SW - 1;
      const nx = type === "downtown" ? (rnd() < 0.5 ? 1 : 2) : (rnd() < 0.5 ? 2 : 3);
      const nz = type === "downtown" ? (rnd() < 0.5 ? 1 : 2) : (rnd() < 0.5 ? 2 : 3);
      const gap = type === "residential" ? 3 : 2;
      const pw = (lx1 - lx0 - gap * (nx - 1)) / nx, pd = (lz1 - lz0 - gap * (nz - 1)) / nz;
      for (let a = 0; a < nx; a++) for (let b = 0; b < nz; b++) {
        let bx0 = lx0 + a * (pw + gap), bz0 = lz0 + b * (pd + gap), bw = pw, bd = pd;
        const shrink = type === "residential" ? rnd() * 3 : rnd() * 1.5;
        bx0 += shrink / 2; bz0 += shrink / 2; bw -= shrink; bd -= shrink;
        let h, style;
        if (type === "downtown") { h = 55 + rnd() * 110 * (1.2 - d); style = rnd() < 0.7 ? "glass" : "office"; }
        else if (type === "midtown") { h = 18 + rnd() * 42; style = pick3(rnd, ["office", "brick", "concrete", "glass"]); }
        else { h = 7 + rnd() * 12; style = pick3(rnd, ["brick", "concrete", "plaster", "plaster"]); }
        h = Math.round(h / 3.5) * 3.5;
        const bld = { x0: bx0, z0: bz0, x1: bx0 + bw, z1: bz0 + bd, h, style, tiers: [] };
        // setbacks for tall towers
        bld.tiers.push({ x0: bld.x0, z0: bld.z0, x1: bld.x1, z1: bld.z1, y0: 0, y1: h });
        if (h > 60 && rnd() < 0.8) {
          const s = Math.min(bw, bd) * 0.18, h2 = Math.round(h * (0.25 + rnd() * 0.3) / 3.5) * 3.5;
          bld.tiers.push({ x0: bld.x0 + s, z0: bld.z0 + s, x1: bld.x1 - s, z1: bld.z1 - s, y0: h, y1: h + h2 });
          if (rnd() < 0.5) { const s2 = s * 2.2; bld.tiers.push({ x0: bld.x0 + s2, z0: bld.z0 + s2, x1: bld.x1 - s2, z1: bld.z1 - s2, y0: h + h2, y1: h + h2 + 10.5 }); }
        }
        blk.buildings.push(bld);
        colliders.push({ x0: bld.x0, z0: bld.z0, x1: bld.x1, z1: bld.z1, h: bld.tiers[bld.tiers.length - 1].y1 });
      }
    } else if (type === "harbor") {
      const lx0 = x0 + SW + 2, lz0 = z0 + SW + 2;
      const wh = { x0: lx0, z0: lz0, x1: lx0 + 20, z1: z1 - SW - 2, h: 10.5, style: "warehouse", tiers: [] };
      wh.tiers.push({ x0: wh.x0, z0: wh.z0, x1: wh.x1, z1: wh.z1, y0: 0, y1: wh.h });
      blk.buildings.push(wh);
      colliders.push({ x0: wh.x0, z0: wh.z0, x1: wh.x1, z1: wh.z1, h: wh.h });
      blk.containers = [];
      for (let r = 0; r < 3; r++) for (let c = 0; c < 5; c++) {
        const stack = 1 + Math.floor(rnd() * 3);
        const cx0 = lx0 + 24, cz0 = lz0 + 1 + c * 7.4;
        blk.containers.push({ x: cx0 + r * 2.7, z: cz0, stack });
      }
      colliders.push({ x0: lx0 + 22.6, z0: lz0, x1: lx0 + 22.6 + 8.4, z1: lz0 + 37, h: 8 });
    }
    blocks.push(blk);
  }
  function pick3(r, arr) { return arr[Math.floor(r() * arr.length)]; }

  // spatial hash of colliders by block cell
  const cellOf = v => Math.floor(v / PITCH);
  const grid = new Map();
  colliders.forEach(c => {
    for (let a = cellOf(c.x0) - 1; a <= cellOf(c.x1) + 1; a++) for (let b = cellOf(c.z0) - 1; b <= cellOf(c.z1) + 1; b++) {
      const k = a + "," + b; if (!grid.has(k)) grid.set(k, []); grid.get(k).push(c);
    }
  });
  const near = (x, z) => grid.get(cellOf(x) + "," + cellOf(z)) || [];
  function addCollider(c) {
    colliders.push(c);
    for (let a = cellOf(c.x0) - 1; a <= cellOf(c.x1) + 1; a++) for (let b = cellOf(c.z0) - 1; b <= cellOf(c.z1) + 1; b++) {
      const k = a + "," + b; if (!grid.has(k)) grid.set(k, []); grid.get(k).push(c);
    }
  }

  // road graph: intersections at (i*PITCH, j*PITCH)
  const node = (i, j) => ({ i, j, x: i * PITCH, z: j * PITCH });
  const inGraph = (i, j) => i >= 0 && j >= 0 && i <= N && j <= N;
  function neighbours(i, j) {
    const out = [];
    for (const [di, dj] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) if (inGraph(i + di, j + dj)) out.push([i + di, j + dj]);
    return out;
  }
  function nearestNode(x, z) { return [clamp(Math.round(x / PITCH), 0, N), clamp(Math.round(z / PITCH), 0, N)]; }
  // BFS path between nodes (grid is small: 81 nodes)
  function path(from, to) {
    const key = (a) => a[0] + "," + a[1];
    const prev = new Map([[key(from), null]]);
    const q = [from];
    while (q.length) {
      const c = q.shift();
      if (c[0] === to[0] && c[1] === to[1]) break;
      for (const n of neighbours(c[0], c[1])) if (!prev.has(key(n))) { prev.set(key(n), c); q.push(n); }
    }
    const out = []; let c = to;
    while (c) { out.unshift(c); c = prev.get(key(c)); }
    return out;
  }
  // is a point on asphalt (inside a road corridor)?
  function onRoad(x, z) {
    const mx = ((x % PITCH) + PITCH) % PITCH, mz = ((z % PITCH) + PITCH) % PITCH;
    const inX = (mx < HALF_R || mx > PITCH - HALF_R), inZ = (mz < HALF_R || mz > PITCH - HALF_R);
    return (inX || inZ) && x > -HALF_R && z > -HALF_R && x < SIZE + HALF_R && z < SIZE + HALF_R;
  }
  function groundHeight(x, z) {
    if (x < -HALF_R - 0.01 || z < -HALF_R - 0.01 || x > SIZE + HALF_R + 30 || z > SIZE + HALF_R + 30) return -100; // water
    return onRoad(x, z) ? 0 : 0.18;
  }
  function blockAt(x, z) {
    const i = Math.floor((x - HALF_R) / PITCH), j = Math.floor((z - HALF_R) / PITCH);
    return i >= 0 && j >= 0 && i < N && j < N ? blocks[j * N + i] : null;
  }
  return { N, B, R, SW, PITCH, SIZE, HALF_R, blocks, colliders, near, addCollider, neighbours, nearestNode, path, node, onRoad, groundHeight, blockAt };
})();

// push a circle (x,z,r) out of building boxes; returns collision normal or null
function collideCircle(p, r) {
  let hit = null;
  for (const c of CITY.near(p.x, p.z)) {
    const qx = clamp(p.x, c.x0, c.x1), qz = clamp(p.z, c.z0, c.z1);
    let dx = p.x - qx, dz = p.z - qz; const d2 = dx * dx + dz * dz;
    if (d2 < r * r) {
      if (d2 < 1e-8) { // centre inside: push out along the shallowest axis
        const l = p.x - c.x0, rr = c.x1 - p.x, t = p.z - c.z0, b = c.z1 - p.z, m = Math.min(l, rr, t, b);
        if (m === l) { dx = -1; dz = 0; p.x = c.x0 - r; } else if (m === rr) { dx = 1; dz = 0; p.x = c.x1 + r; }
        else if (m === t) { dx = 0; dz = -1; p.z = c.z0 - r; } else { dx = 0; dz = 1; p.z = c.z1 + r; }
        hit = { x: dx, z: dz };
      } else {
        const d = Math.sqrt(d2), push = r - d; dx /= d; dz /= d;
        p.x += dx * push; p.z += dz * push; hit = { x: dx, z: dz };
      }
    }
  }
  // world edge: the harbour water
  const lim0 = -CITY.HALF_R - 1, lim1 = CITY.SIZE + CITY.HALF_R + 28;
  if (p.x < lim0 + r) { p.x = lim0 + r; hit = { x: 1, z: 0 }; }
  if (p.z < lim0 + r) { p.z = lim0 + r; hit = { x: 0, z: 1 }; }
  if (p.x > lim1 - r) { p.x = lim1 - r; hit = { x: -1, z: 0 }; }
  if (p.z > lim1 - r) { p.z = lim1 - r; hit = { x: 0, z: -1 }; }
  return hit;
}
// segment vs boxes (camera collision): returns t in [0,1]
function raycastBoxes(ax, ay, az, bx, by, bz) {
  let best = 1;
  const cells = new Set();
  for (let s = 0; s <= 4; s++) { const t = s / 4; CITY.near(lerp(ax, bx, t), lerp(az, bz, t)).forEach(c => cells.add(c)); }
  for (const c of cells) {
    let t0 = 0, t1 = 1; const d = [bx - ax, by - ay, bz - az], o = [ax, ay, az], mn = [c.x0, 0, c.z0], mx = [c.x1, c.h, c.z1];
    let ok = true;
    for (let k = 0; k < 3 && ok; k++) {
      if (Math.abs(d[k]) < 1e-9) { if (o[k] < mn[k] || o[k] > mx[k]) ok = false; }
      else { let ta = (mn[k] - o[k]) / d[k], tb = (mx[k] - o[k]) / d[k]; if (ta > tb) [ta, tb] = [tb, ta]; t0 = Math.max(t0, ta); t1 = Math.min(t1, tb); if (t0 > t1) ok = false; }
    }
    if (ok && t0 < best) best = t0;
  }
  return best;
}
