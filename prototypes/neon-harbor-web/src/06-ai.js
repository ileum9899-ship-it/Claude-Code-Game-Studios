// ---------------------------------------------------------------- traffic lights
const LIGHTS = (() => {
  const offs = new Map();
  const key = (i, j) => i * 100 + j;
  for (let j = 0; j <= CITY.N; j++) for (let i = 0; i <= CITY.N; i++) offs.set(key(i, j), (i * 7 + j * 13) % 9 * 2);
  // axis 0 = traffic moving along x, 1 = along z
  function state(i, j, axis, time) {
    const cyc = CFG.traffic.lightCycle, half = cyc / 2, y = CFG.traffic.yellow;
    const t = (time + offs.get(key(i, j))) % cyc;
    const local = axis === 0 ? t : (t + half) % cyc;
    if (local < half - y) return 0; // green
    if (local < half) return 1;     // yellow
    return 2;                       // red
  }
  // visuals: one signal head per approach (4 per intersection), 3 lamps each
  let lampMesh = null; const heads = [];
  function build() {
    const pole = new Instancer(new THREE.CylinderGeometry(0.1, 0.12, 5, 8), M.metal);
    const arm = new Instancer(new THREE.BoxGeometry(0.12, 0.12, 4), M.metal);
    const box = new Instancer(new THREE.BoxGeometry(0.42, 1.2, 0.36), stdMat({ color: col(0x1d2024), roughness: 0.6 }));
    const lamps = [];
    for (let j = 0; j <= CITY.N; j++) for (let i = 0; i <= CITY.N; i++) {
      const x = i * CITY.PITCH, z = j * CITY.PITCH, h = CITY.HALF_R + 1.2;
      // approach from -x travelling +x: signal on the far-right corner facing -x
      for (const [dx, dz, axis] of [[1, 0, 0], [-1, 0, 0], [0, 1, 1], [0, -1, 1]]) {
        const rx = -dz, rz = dx; // right of travel
        const cx = x + dx * h + rx * h, cz = z + dz * h + rz * h;
        const ex = cx - dx * 0 - rx * 2.6, ez = cz - rz * 2.6; // arm reaches over the lane
        const ry = Math.atan2(-dx, -dz);
        pole.add(cx, 2.68, cz);
        arm.add((cx + ex) / 2, 5.0, (cz + ez) / 2, 1, 1, 1, Math.atan2(rx, rz));
        box.add(ex, 4.4, ez, 1, 1, 1, ry);
        for (let k = 0; k < 3; k++) lamps.push({ x: ex - dx * 0.2, y: 4.78 - k * 0.37, z: ez - dz * 0.2, i, j, axis, k });
      }
    }
    pole.build(); arm.build(); box.build();
    lampMesh = new THREE.InstancedMesh(new THREE.SphereGeometry(0.13, 10, 8), M.signalLamp, lamps.length);
    const o = new THREE.Object3D();
    lamps.forEach((l, n) => { o.position.set(l.x, l.y, l.z); o.updateMatrix(); lampMesh.setMatrixAt(n, o.matrix); lampMesh.setColorAt(n, new THREE.Color()); });
    world.add(lampMesh); heads.push(...lamps);
  }
  const ON = [new THREE.Color(0.1, 3.2, 0.6), new THREE.Color(3.4, 2.0, 0.1), new THREE.Color(4, 0.15, 0.1)], OFF = [new THREE.Color(0.01, 0.04, 0.02), new THREE.Color(0.05, 0.035, 0.01), new THREE.Color(0.05, 0.01, 0.01)];
  let last = -1;
  function update(time) {
    const step = Math.floor(time * 4); if (step === last || !lampMesh) return; last = step;
    heads.forEach((l, n) => { const s = state(l.i, l.j, l.axis, time); const lampIdx = 2 - l.k; lampMesh.setColorAt(n, (s === lampIdx) ? ON[lampIdx] : OFF[lampIdx]); });
    lampMesh.instanceColor.needsUpdate = true;
  }
  return { state, build, update };
})();

// ---------------------------------------------------------------- vehicles registry
const VEHICLES = []; // all cars (traffic, parked, police, player)
function removeCar(car) { const k = VEHICLES.indexOf(car); if (k >= 0) VEHICLES.splice(k, 1); dynamic.remove(car.root); if (car.driverMesh) car.driverMesh = null; }
function addDriver(car) {
  const d = makeHuman({ top: car.type === "police" ? 0x1d2a44 : undefined });
  poseSit(d); d.root.scale.setScalar(0.95); d.root.position.set(-0.38, -0.48, -0.15);
  car.body.add(d.root); car.driverMesh = d; return d;
}

// ---------------------------------------------------------------- traffic AI (lane-following on the road graph)
const DIRS = [[1, 0], [-1, 0], [0, 1], [0, -1]];
const LANE = 3;
function nodePos(n) { return new THREE.Vector2(n[0] * CITY.PITCH, n[1] * CITY.PITCH); }
function bez(p0, p1, p2, t) { const u = 1 - t; return new THREE.Vector2(u * u * p0.x + 2 * u * t * p1.x + t * t * p2.x, u * u * p0.y + 2 * u * t * p1.y + t * t * p2.y); }
function bezT(p0, p1, p2, t) { return new THREE.Vector2(2 * (1 - t) * (p1.x - p0.x) + 2 * t * (p2.x - p1.x), 2 * (1 - t) * (p1.y - p0.y) + 2 * t * (p2.y - p1.y)); }
function laneRight(f) { return new THREE.Vector2(-f.y, f.x); } // right of travel, in (x,z)
function makeSegPiece(a, b) {
  const A = nodePos(a), B = nodePos(b), f = B.clone().sub(A).normalize(), r = laneRight(f);
  const p0 = A.clone().addScaledVector(f, CITY.HALF_R).addScaledVector(r, LANE), p2 = B.clone().addScaledVector(f, -CITY.HALF_R).addScaledVector(r, LANE);
  return { kind: "seg", a, b, f, p0, p1: p0.clone().lerp(p2, 0.5), p2, len: p0.distanceTo(p2), axis: Math.abs(f.x) > 0.5 ? 0 : 1 };
}
function makeTurnPiece(a, b, c) {
  const A = nodePos(a), B = nodePos(b), C = nodePos(c);
  const f1 = B.clone().sub(A).normalize(), f2 = C.clone().sub(B).normalize(), r1 = laneRight(f1), r2 = laneRight(f2);
  const p0 = B.clone().addScaledVector(f1, -CITY.HALF_R).addScaledVector(r1, LANE), p2 = B.clone().addScaledVector(f2, CITY.HALF_R).addScaledVector(r2, LANE);
  const straight = Math.abs(f1.dot(f2)) > 0.9;
  const p1 = straight ? p0.clone().lerp(p2, 0.5) : B.clone().addScaledVector(r1, LANE).addScaledVector(r2, LANE);
  let len = 0, prev = p0; for (let k = 1; k <= 8; k++) { const q = bez(p0, p1, p2, k / 8); len += q.distanceTo(prev); prev = q; }
  return { kind: "turn", a, b, c, p0, p1, p2, len, straight };
}
function chooseNext(prev, cur) {
  const opts = CITY.neighbours(cur[0], cur[1]).filter(n => !(n[0] === prev[0] && n[1] === prev[1]));
  return opts.length ? pick(opts) : prev;
}
function spawnTrafficCar(nearPlayer) {
  for (let attempt = 0; attempt < 20; attempt++) {
    const a = [randi(0, CITY.N), randi(0, CITY.N)], ns = CITY.neighbours(a[0], a[1]), b = pick(ns);
    const piece = makeSegPiece(a, b), s = rand(0, piece.len), p = bez(piece.p0, piece.p1, piece.p2, s / piece.len);
    const d = Math.hypot(p.x - nearPlayer.x, p.y - nearPlayer.z);
    if (d < 70 || d > 230) continue;
    if (VEHICLES.some(v => Math.hypot(v.pos.x - p.x, v.pos.z - p.y) < 9)) continue;
    const type = pick(["sedan", "sedan", "sedan", "suv", "suv", "taxi", "sports", "van"]);
    const car = makeCar(type);
    car.ai = { mode: "drive", piece, s, speed: CFG.traffic.cruise * rand(0.85, 1.1), next: chooseNext(a, b), want: CFG.traffic.cruise * rand(0.85, 1.15), honk: 0 };
    addDriver(car);
    placeOnPiece(car);
    VEHICLES.push(car);
    return car;
  }
  return null;
}
function placeOnPiece(car) {
  const ai = car.ai, t = clamp(ai.s / ai.piece.len, 0, 1);
  const p = bez(ai.piece.p0, ai.piece.p1, ai.piece.p2, t), tg = bezT(ai.piece.p0, ai.piece.p1, ai.piece.p2, t);
  car.pos.set(p.x, 0, p.y);
  car.yaw = Math.atan2(tg.x, tg.y);
  const f = forwardOf(car); car.vel.set(f.x * ai.speed, f.y * ai.speed);
}
function obstacleAhead(car, maxDist) {
  const f = forwardOf(car), r = new THREE.Vector2(-f.y, f.x);
  let best = maxDist;
  const test = (x, z, rad) => {
    const dx = x - car.pos.x, dz = z - car.pos.z, along = dx * f.x + dz * f.y, lat = Math.abs(dx * r.x + dz * r.y);
    if (along > 0 && along < best + 3 && lat < 1.4 + rad) best = Math.min(best, along - car.spec.len / 2 - rad);
  };
  for (const v of VEHICLES) if (v !== car) test(v.pos.x, v.pos.z, v.spec.len / 2);
  for (const p of PEDS) if (p.state !== "down" && CITY.onRoad(p.pos.x, p.pos.z)) test(p.pos.x, p.pos.z, 0.4);
  if (!PLAYER.car) test(PLAYER.pos.x, PLAYER.pos.z, 0.4);
  return best;
}
function updateTraffic(car, dt, time) {
  const ai = car.ai;
  if (ai.mode !== "drive") { stepCar(car, dt); return; } // wrecked: coast as a physics body
  let want = ai.piece.kind === "turn" && !ai.piece.straight ? CFG.traffic.turnSpeed : ai.want;
  // red light: stop before the stop line of a segment
  if (ai.piece.kind === "seg") {
    const toEnd = ai.piece.len - ai.s, st = LIGHTS.state(ai.piece.b[0], ai.piece.b[1], ai.piece.axis, time);
    const stopAt = toEnd - 4.6 - car.spec.len / 2;
    if (st === 2 || (st === 1 && stopAt > ai.speed * ai.speed / 12)) {
      if (stopAt > -0.5) want = Math.min(want, Math.max(0, Math.sqrt(Math.max(0, stopAt) * 2 * 5.5) - 0.3));
    }
  }
  const gap = obstacleAhead(car, CFG.traffic.followGap * 2.2);
  if (gap < CFG.traffic.followGap * 2.2) want = Math.min(want, Math.max(0, (gap - 2.2) * 1.1));
  if (gap < 3 && !PLAYER.car && Math.hypot(PLAYER.pos.x - car.pos.x, PLAYER.pos.z - car.pos.z) < 8 && ai.honk <= 0) { AUDIO.horn(car.pos, 0.35); ai.honk = 4; }
  ai.honk -= dt;
  const accel = want > ai.speed ? 4 : 9;
  ai.speed = want > ai.speed ? Math.min(want, ai.speed + accel * dt) : Math.max(want, ai.speed - accel * dt);
  ai.s += ai.speed * dt;
  while (ai.s >= ai.piece.len) {
    ai.s -= ai.piece.len;
    if (ai.piece.kind === "seg") { const p = ai.piece; ai.piece = makeTurnPiece(p.a, p.b, ai.next); }
    else { const p = ai.piece; ai.piece = makeSegPiece(p.b, p.c); ai.next = chooseNext(p.b, p.c); }
  }
  const prevYaw = car.yaw;
  placeOnPiece(car);
  car.yawRate = angDiff(prevYaw, car.yaw) / Math.max(dt, 1e-4);
  car.steer = clamp(car.yawRate * 0.35, -0.5, 0.5);
  car.wheelSpin += ai.speed * dt / car.spec.wheel;
  car.braking = want < ai.speed - 0.3 || ai.speed < 0.2;
  car.roll = damp(car.roll, clamp(-ai.speed * car.yawRate * 0.012, -0.06, 0.06), 6, dt);
  syncCar(car);
}

// ---------------------------------------------------------------- police AI (physics cars that chase)
function spawnPolice() {
  const [pi, pj] = CITY.nearestNode(PLAYER.pos.x, PLAYER.pos.z);
  for (let a = 0; a < 30; a++) {
    const n = [clamp(pi + randi(-3, 3), 0, CITY.N), clamp(pj + randi(-3, 3), 0, CITY.N)];
    const p = nodePos(n), d = Math.hypot(p.x - PLAYER.pos.x, p.y - PLAYER.pos.z);
    if (d < 90 || d > 200) continue;
    if (VEHICLES.some(v => Math.hypot(v.pos.x - p.x, v.pos.z - p.y) < 10)) continue;
    const car = makeCar("police");
    car.pos.set(p.x, 0, p.y); car.yaw = Math.atan2(PLAYER.pos.x - p.x, PLAYER.pos.z - p.y);
    car.ai = { mode: "police", path: [], repath: 0, stuck: 0, reverse: 0 };
    addDriver(car); syncCar(car); VEHICLES.push(car);
    return car;
  }
  return null;
}
function updatePolice(car, dt, t) {
  const ai = car.ai, tgt = WANTED.targetFor(car);
  const d = Math.hypot(tgt.x - car.pos.x, tgt.z - car.pos.z);
  ai.repath -= dt;
  if (ai.repath <= 0) { ai.repath = 1; ai.path = CITY.path(CITY.nearestNode(car.pos.x, car.pos.z), CITY.nearestNode(tgt.x, tgt.z)); }
  let aim;
  if (d < 32 || ai.path.length <= 1) {
    const pv = PLAYER.car ? PLAYER.car.vel : new THREE.Vector2();
    aim = new THREE.Vector2(tgt.x + pv.x * 0.45, tgt.z + pv.y * 0.45);
  } else {
    let k = 0; while (k < ai.path.length - 1 && nodePos(ai.path[k]).distanceTo(new THREE.Vector2(car.pos.x, car.pos.z)) < 9) k++;
    aim = nodePos(ai.path[k]);
  }
  const want = Math.atan2(aim.x - car.pos.x, aim.y - car.pos.z), diff = angDiff(car.yaw, want), sp = speedOf(car);
  if (ai.reverse > 0) { ai.reverse -= dt; car.throttle = -1; car.steerIn = -Math.sign(diff); car.handbrake = false; }
  else {
    car.steerIn = clamp(diff * 2.2, -1, 1);
    const sharp = Math.abs(diff);
    car.throttle = d < 7 && !PLAYER.car ? 0 : sharp > 1.2 ? 0.45 : 1;
    car.brake = d < 7 && !PLAYER.car ? 1 : 0;
    car.handbrake = sharp > 1.0 && sp > 14;
    ai.stuck = sp < 1.2 && car.throttle > 0 ? ai.stuck + dt : 0;
    if (ai.stuck > 1.4) { ai.reverse = 1.3; ai.stuck = 0; }
  }
  stepCar(car, dt, true);
  // siren
  const on = Math.floor(t * 6) % 2 === 0;
  car.siren.mr.color.setRGB(on ? 8 : 0.3, on ? 0.3 : 0.02, on ? 0.2 : 0.02);
  car.siren.mb.color.setRGB(on ? 0.02 : 0.3, on ? 0.05 : 0.6, on ? 0.3 : 9);
}

// ---------------------------------------------------------------- pedestrians
const PEDS = [];
function pedPathPoint(blk, t) { // perimeter of the sidewalk ring, t in [0,1)
  const inset = CITY.SW * 0.5, x0 = blk.x0 + inset, z0 = blk.z0 + inset, x1 = blk.x1 - inset, z1 = blk.z1 - inset;
  const w = x1 - x0, h = z1 - z0, P = 2 * (w + h); let d = ((t % 1) + 1) % 1 * P;
  if (d < w) return [x0 + d, z0, 1, 0]; d -= w;
  if (d < h) return [x1, z0 + d, 0, 1]; d -= h;
  if (d < w) return [x1 - d, z1, -1, 0]; d -= w;
  return [x0, z1 - d, 0, -1];
}
function spawnPed(blk, t) {
  const h = makeHuman(); dynamic.add(h.root);
  const ped = { h, blk, t: t ?? RNG(), dir: RNG() < 0.5 ? 1 : -1, speed: CFG.peds.speed * rand(0.8, 1.2), state: "walk", pos: new THREE.Vector3(), vel: new THREE.Vector3(), timer: 0, yaw: 0, spin: 0 };
  const p = pedPathPoint(blk, ped.t); ped.pos.set(p[0], 0.18, p[1]);
  PEDS.push(ped); return ped;
}
function removePed(p) { dynamic.remove(p.h.root); PEDS.splice(PEDS.indexOf(p), 1); }
function knockPed(p, vx, vz, byPlayer) {
  if (p.state === "down" || p.state === "air" || p.state === "gone") return;
  p.state = "air"; p.timer = 12;
  p.vel.set(vx * 1.05 + rand(-1, 1), 3 + Math.hypot(vx, vz) * 0.22, vz * 1.05 + rand(-1, 1));
  p.spin = rand(-8, 8);
  AUDIO.thud(0.7);
  if (byPlayer) { WANTED.crime(CFG.wanted.pedHit, "보행자 사고"); }
  PEDS.forEach(o => { if (o !== p && o.state === "walk" && o.pos.distanceTo(p.pos) < 25) { o.state = "flee"; o.timer = 5; } });
}
function updatePed(p, dt) {
  const perim = 2 * ((p.blk.x1 - p.blk.x0) + (p.blk.z1 - p.blk.z0) - 2 * CITY.SW);
  if (p.state === "walk" || p.state === "flee") {
    const sp = p.state === "flee" ? CFG.peds.fleeSpeed : p.speed;
    if (p.state === "flee" && (p.timer -= dt) <= 0) p.state = "walk";
    // flee: run away from the threat along the ring
    if (p.state === "flee" && p.threat) {
      const a = pedPathPoint(p.blk, p.t), toward = (p.threat.x - a[0]) * a[2] + (p.threat.z - a[1]) * a[3];
      p.dir = toward > 0 ? -1 : 1;
    }
    p.t += p.dir * sp * dt / perim;
    const q = pedPathPoint(p.blk, p.t);
    p.pos.x = damp(p.pos.x, q[0], 10, dt); p.pos.z = damp(p.pos.z, q[1], 10, dt); p.pos.y = 0.18;
    p.yaw = Math.atan2(q[2] * p.dir, q[3] * p.dir);
    p.h.root.rotation.set(0, p.yaw, 0);
    animateHuman(p.h, sp, dt);
  } else if (p.state === "air") {
    p.vel.y -= 18 * dt; p.pos.addScaledVector(p.vel, dt);
    const c = { x: p.pos.x, z: p.pos.z }; if (collideCircle(c, 0.4)) { p.vel.x *= -0.3; p.vel.z *= -0.3; p.pos.x = c.x; p.pos.z = c.z; }
    p.h.root.rotation.x += p.spin * dt; p.h.root.rotation.z += p.spin * 0.6 * dt;
    const g = CITY.groundHeight(p.pos.x, p.pos.z);
    if (p.pos.y <= g + 0.2 && p.vel.y < 0) { p.state = "down"; p.pos.y = g + 0.12; p.h.root.rotation.set(-Math.PI / 2, p.yaw + rand(-1, 1), 0, "YXZ"); }
    animateHuman(p.h, 0, dt, true);
  } else if (p.state === "down") {
    p.timer -= dt;
    if (p.timer < 0) { p.state = "gone"; }
  }
  p.h.root.position.copy(p.pos);
}

// ---------------------------------------------------------------- vehicle-vehicle and vehicle-ped contacts
function resolveContacts(dt) {
  for (let a = 0; a < VEHICLES.length; a++) for (let b = a + 1; b < VEHICLES.length; b++) {
    const A = VEHICLES[a], Bc = VEHICLES[b];
    const fa = forwardOf(A), fb = forwardOf(Bc);
    let hit = null;
    for (const oa of [A.spec.len * 0.28, -A.spec.len * 0.28]) for (const ob of [Bc.spec.len * 0.28, -Bc.spec.len * 0.28]) {
      const ax = A.pos.x + fa.x * oa, az = A.pos.z + fa.y * oa, bx = Bc.pos.x + fb.x * ob, bz = Bc.pos.z + fb.y * ob;
      const dx = bx - ax, dz = bz - az, d = Math.hypot(dx, dz), R = A.radius + Bc.radius - 0.15;
      if (d < R && d > 1e-4 && (!hit || R - d > hit.pen)) hit = { nx: dx / d, nz: dz / d, pen: R - d };
    }
    if (!hit) continue;
    // a driving traffic car becomes a physics body when struck
    for (const v of [A, Bc]) if (v.ai && v.ai.mode === "drive") { v.ai.mode = "wreck"; v.throttle = 0; v.brake = 0.3; v.steerIn = 0; }
    const ma = A.mass, mb = Bc.mass, tot = ma + mb;
    A.pos.x -= hit.nx * hit.pen * mb / tot; A.pos.z -= hit.nz * hit.pen * mb / tot;
    Bc.pos.x += hit.nx * hit.pen * ma / tot; Bc.pos.z += hit.nz * hit.pen * ma / tot;
    const rv = (Bc.vel.x - A.vel.x) * hit.nx + (Bc.vel.y - A.vel.y) * hit.nz;
    if (rv < 0) {
      const j = -(1.3) * rv / (1 / ma + 1 / mb);
      A.vel.x -= j * hit.nx / ma; A.vel.y -= j * hit.nz / ma; Bc.vel.x += j * hit.nx / mb; Bc.vel.y += j * hit.nz / mb;
      A.yawRate += rand(-1, 1) * -rv * 0.08; Bc.yawRate += rand(-1, 1) * -rv * 0.08;
      if (-rv > 4) {
        AUDIO.crash(Math.min(1, -rv / 20));
        const pc = PLAYER.car;
        if (pc === A || pc === Bc) {
          CAMERA.shake = Math.min(1, -rv / 18); pc.health -= -rv * 1.4;
          const other = pc === A ? Bc : A;
          if (other.type === "police" && -rv > 6) WANTED.crime(CFG.wanted.copHit, "경찰차 충돌", true);
        }
      }
    }
    syncCar(A); syncCar(Bc);
  }
  // cars vs pedestrians and the player on foot
  for (const v of VEHICLES) {
    const sp = speedOf(v); if (sp < 1.5) continue;
    const f = forwardOf(v);
    for (const p of PEDS) {
      if (p.state === "down" || p.state === "air" || p.state === "gone") continue;
      const dx = p.pos.x - v.pos.x, dz = p.pos.z - v.pos.z;
      if (Math.abs(dx) > 4 || Math.abs(dz) > 4) continue;
      const along = dx * f.x + dz * f.y, lat = Math.abs(-dx * f.y + dz * f.x);
      if (Math.abs(along) < v.spec.len / 2 + 0.3 && lat < v.spec.wid / 2 + 0.3) {
        if (sp > CFG.peds.hitSpeed) knockPed(p, v.vel.x, v.vel.y, v === PLAYER.car);
        else { p.state = "flee"; p.timer = 3; p.threat = v.pos.clone(); }
      }
    }
    if (!PLAYER.car && PLAYER.alive) {
      const dx = PLAYER.pos.x - v.pos.x, dz = PLAYER.pos.z - v.pos.z, along = dx * f.x + dz * f.y, lat = Math.abs(-dx * f.y + dz * f.x);
      if (Math.abs(along) < v.spec.len / 2 + 0.35 && lat < v.spec.wid / 2 + 0.35 && PLAYER.pos.y < 1.2) {
        if (sp > 5) { PLAYER.knock(v.vel.x, v.vel.y, sp); }
        else { // shove the player aside
          const s = Math.sign(-dx * f.y + dz * f.x) || 1; PLAYER.pos.x += -f.y * s * 0.15; PLAYER.pos.z += f.x * s * 0.15;
        }
      }
    }
  }
}
