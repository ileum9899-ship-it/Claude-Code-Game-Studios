// ---------------------------------------------------------------- police AI: tactics maths (= Core/PoliceTactics.cs),
// route-following drivers with corner braking and obstacle whiskers, pursuit roles, roadblocks, helicopter.
const PT = {
  intercept(cx, cz, chaserSpeed, tx, tz, vx, vz, maxLead) {
    const dx = tx - cx, dz = tz - cz, s = Math.max(chaserSpeed, 8);
    const a = vx * vx + vz * vz - s * s, b = 2 * (dx * vx + dz * vz), c = dx * dx + dz * dz;
    let t;
    if (Math.abs(a) < 1e-4) t = b < 0 ? -c / b : maxLead;
    else { const disc = b * b - 4 * a * c; if (disc < 0) t = maxLead; else { const q = Math.sqrt(disc), t1 = (-b - q) / (2 * a), t2 = (-b + q) / (2 * a); t = Math.min(t1, t2) > 0 ? Math.min(t1, t2) : Math.max(t1, t2); if (t <= 0) t = maxLead; } }
    t = clamp(t, 0, maxLead);
    return { x: tx + vx * t, z: tz + vz * t };
  },
  turnAngle(a, b, c) { const ux = b.x - a.x, uz = b.z - a.z, vx = c.x - b.x, vz = c.z - b.z, lu = Math.hypot(ux, uz) || 1, lv = Math.hypot(vx, vz) || 1; return Math.acos(clamp((ux * vx + uz * vz) / (lu * lv), -1, 1)); },
  speedForCorner(angle, dist, top) { const k = clamp(1 - angle / (Math.PI / 2), 0, 1), corner = 8 + (top - 8) * k * k; return Math.min(top, Math.sqrt(corner * corner + 2 * 14 * Math.max(0, dist))); },
  lookAhead(path, px, pz, L) {
    if (path.length === 1) return path[0];
    let best = 0, bestD = 1e9, bestT = 0;
    for (let k = 0; k + 1 < path.length; k++) {
      const a = path[k], bx = path[k + 1].x - a.x, bz = path[k + 1].z - a.z, l2 = Math.max(1e-6, bx * bx + bz * bz);
      const t = clamp(((px - a.x) * bx + (pz - a.z) * bz) / l2, 0, 1), d = Math.hypot(a.x + bx * t - px, a.z + bz * t - pz);
      if (d < bestD) { bestD = d; best = k; bestT = t; }
    }
    let rem = L, cx = path[best].x + (path[best + 1].x - path[best].x) * bestT, cz = path[best].z + (path[best + 1].z - path[best].z) * bestT;
    for (let k = best; k + 1 < path.length; k++) {
      const n = path[k + 1], seg = Math.hypot(n.x - cx, n.z - cz);
      if (seg >= rem) return { x: cx + (n.x - cx) / seg * rem, z: cz + (n.z - cz) / seg * rem };
      rem -= seg; cx = n.x; cz = n.z;
    }
    return path[path.length - 1];
  },
  nodeAhead(tx, tz, hx, hz, n) {
    const [i, j] = CITY.nearestNode(tx, tz); let di = 0, dj = 0;
    if (Math.abs(hx) >= Math.abs(hz)) di = hx >= 0 ? 1 : -1; else dj = hz >= 0 ? 1 : -1;
    return [clamp(i + di * n, 0, CITY.N), clamp(j + dj * n, 0, CITY.N)];
  },
  inView(ex, ez, yaw, tx, tz, fov, range, near) {
    const dx = tx - ex, dz = tz - ez, d = Math.hypot(dx, dz);
    if (d < near) return true; if (d > range) return false;
    return Math.abs(angDiff(yaw, Math.atan2(dx, dz))) <= fov / 2;
  },
  searchWaypoint(lk, radius, unit, units, leg) {
    units = Math.max(1, units); const sector = TAU / units, base = unit * sector;
    if (leg <= 0) { const a0 = base + sector * 0.5; return new THREE.Vector3(lk.x + Math.cos(a0) * 6, 0, lk.z + Math.sin(a0) * 6); }
    const frac = Math.min(0.9, 0.25 + 0.2 * (leg - 1)), ang = base + sector * (0.2 + 0.6 * (((leg - 1) * 0.618) % 1));
    return new THREE.Vector3(lk.x + Math.cos(ang) * radius * frac, 0, lk.z + Math.sin(ang) * radius * frac);
  },
  accuracy(d, targetSpeed, stars) { return clamp(0.5 + 0.035 * stars - d * 0.011 - targetSpeed * 0.013, 0.08, 0.7); },
};

const POLICE = (() => {
  const P = PLAYER;
  const roles = new Map(), units = new Map();
  let roleTimer = 0, roadblockCd = 8, roadblock = null, heli = null, clock = 0;
  const playerVel = () => P.car ? { x: P.car.vel.x, z: P.car.vel.y } : { x: P.vel.x, z: P.vel.z };
  const playerPos = () => P.car ? P.car.pos : P.pos;

  function unitIndex(u) { if (!units.has(u)) units.set(u, units.size); return units.get(u); }
  const legs = new Map();
  function searchTarget(u) {
    const lk = WANTED.st.lastKnown; let L = legs.get(u);
    if (!L) legs.set(u, L = { leg: 0, t: 0, ax: lk.x, az: lk.z });
    if (Math.hypot(L.ax - lk.x, L.az - lk.z) > 5) { L.ax = lk.x; L.az = lk.z; L.leg = 0; L.t = 0; } // new sighting: start over at it
    const p = PT.searchWaypoint(lk, WANTED.radius(), unitIndex(u) % 4, Math.min(4, Math.max(1, units.size)), L.leg);
    const at = u.pos || (u.g && u.pos) || null; L.t += 1 / 60;
    if ((at && Math.hypot(at.x - p.x, at.z - p.z) < 14) || L.t > 15) { L.leg++; L.t = 0; }
    return p;
  }

  // route over the road graph, refreshed when the goal moves or every 0.8 s
  function route(car, goal) {
    const ai = car.ai;
    ai.rt = (ai.rt || 0) - 1;
    if (!ai.route || ai.rt <= 0 || Math.hypot(goal.x - ai.goalX, goal.z - ai.goalZ) > 12) {
      ai.rt = 48; ai.goalX = goal.x; ai.goalZ = goal.z;
      const nodes = CITY.path(CITY.nearestNode(car.pos.x, car.pos.z), CITY.nearestNode(goal.x, goal.z));
      const f = forwardOf(car), pts = [{ x: car.pos.x, z: car.pos.z }];
      nodes.forEach((n, k) => {
        const x = n[0] * CITY.PITCH, z = n[1] * CITY.PITCH;
        if (k === 0 && nodes.length > 1 && ((x - car.pos.x) * f.x + (z - car.pos.z) * f.y) < 0 && Math.hypot(x - car.pos.x, z - car.pos.z) < 14) return; // don't double back
        pts.push({ x, z });
      });
      pts.push({ x: goal.x, z: goal.z });
      ai.route = pts;
    }
    return ai.route;
  }

  // drive along a polyline: pure pursuit + corner speed planning + obstacle whiskers
  function drive(car, pts, opts) {
    const ai = car.ai, sp = speedOf(car), top = (opts.maxSpeed || CFG.car.maxSpeed * CFG.wanted.copSpeed * 0.95);
    pts[0] = { x: car.pos.x, z: car.pos.z };
    const L = 7 + sp * 0.45, aim = opts.direct ? opts.direct : PT.lookAhead(pts, car.pos.x, car.pos.z, L);
    let want = Math.atan2(aim.x - car.pos.x, aim.z - car.pos.z), diff = angDiff(car.yaw, want);
    // speed planning for the next corner on the route
    let vmax = top, along = 0;
    for (let k = 1; k + 1 < pts.length && along < 120; k++) {
      along += Math.hypot(pts[k].x - pts[k - 1].x, pts[k].z - pts[k - 1].z);
      const ang = PT.turnAngle(pts[k - 1], pts[k], pts[k + 1]);
      if (ang > 0.3) { vmax = Math.min(vmax, PT.speedForCorner(ang, along - 6, top)); break; }
    }
    if (opts.stopAt != null) vmax = Math.min(vmax, Math.sqrt(2 * 12 * Math.max(0, opts.stopAt)));
    // whiskers: buildings / props and cars ahead
    const f = forwardOf(car), reach = 6 + sp * 0.6;
    const probe = a => { const yaw = car.yaw + a, dx = Math.sin(yaw), dz = Math.cos(yaw); return raycastBoxes(car.pos.x + dx * 2, 1, car.pos.z + dz * 2, car.pos.x + dx * reach, 1, car.pos.z + dz * reach); };
    const cL = probe(0.38), cC = probe(0), cR = probe(-0.38);
    let carAhead = 1;
    for (const v of VEHICLES) {
      if (v === car || v === P.car) continue;
      const dx = v.pos.x - car.pos.x, dz = v.pos.z - car.pos.z, al = dx * f.x + dz * f.y, lat = Math.abs(-dx * f.y + dz * f.x);
      if (al > 0 && al < reach && lat < 2.2) carAhead = Math.min(carAhead, al / reach);
    }
    const block = Math.min(cC, carAhead);
    if (block < 0.6) { diff += (cL > cR ? 1 : -1) * (0.6 - block) * 2.2; vmax = Math.min(vmax, 6 + block * 20); }
    else if (Math.min(cL, cR) < 0.45) diff += (cL > cR ? 0.35 : -0.35);
    car.steerIn = clamp(diff * 2.4, -1, 1);
    car.throttle = sp < vmax - 1 ? 1 : sp > vmax + 2 ? -1 : 0.25;
    car.brake = sp > vmax + 6 ? 0.6 : 0;
    car.handbrake = Math.abs(diff) > 1.1 && sp > 13;
    if (opts.ram) { car.throttle = 1; car.brake = 0; }
  }

  function relocate(car) { // out-of-sight respawn of a hopelessly stuck unit (as the genre does)
    const [pi, pj] = CITY.nearestNode(P.pos.x, P.pos.z);
    for (let a = 0; a < 20; a++) {
      const n = [clamp(pi + randi(-3, 3), 0, CITY.N), clamp(pj + randi(-3, 3), 0, CITY.N)], x = n[0] * CITY.PITCH, z = n[1] * CITY.PITCH;
      const d = Math.hypot(x - P.pos.x, z - P.pos.z);
      if (d < 80 || d > 190 || raycastBoxes(x, 1.5, z, P.pos.x, 1.5, P.pos.z) >= 0.999) continue;
      car.pos.set(x, 0, z); car.vel.set(0, 0); car.yaw = Math.atan2(P.pos.x - x, P.pos.z - z); car.ai.route = null; car.ai.stuckN = 0; syncCar(car); return;
    }
  }

  function updateCar(car, dt, t) {
    if (car.ai.roadblock) { car.throttle = 0; car.brake = 1; car.steerIn = 0; stepCar(car, dt, true); return; }
    const ai = car.ai, sp = speedOf(car), real = playerPos(), pv = playerVel(), seen = !WANTED.st.searching;
    const d = Math.hypot(real.x - car.pos.x, real.z - car.pos.z);
    ai.role = roles.get(car) || "pursuer";
    if (ai.reverse > 0) {
      ai.reverse -= dt; car.throttle = -1; car.brake = 0; car.handbrake = false; car.steerIn = ai.revSteer;
    } else if (!seen) {
      const goal = searchTarget(car);
      drive(car, route(car, goal), { maxSpeed: 16 });
    } else {
      const onFoot = !P.car;
      let goal, direct = null, ram = false, stopAt = null;
      if (ai.role === "pursuer" || d < 40 || ai.cutTimer > 0) {
        const ic = PT.intercept(car.pos.x, car.pos.z, Math.max(sp, 20), real.x, real.z, pv.x, pv.z, 2.5);
        goal = ic;
        if (d < 30 && raycastBoxes(car.pos.x, 1, car.pos.z, ic.x, 1, ic.z) >= 0.999) {
          direct = ic;
          if (!onFoot && d < 14) { // PIT: aim at the target's rear quarter when alongside
            const tf = forwardOf(P.car), lat = -(car.pos.x - real.x) * tf.y + (car.pos.z - real.z) * tf.x;
            direct = { x: real.x - tf.x * 1.6 + (-tf.y) * Math.sign(lat || 1) * -0.5, z: real.z - tf.y * 1.6 + tf.x * Math.sign(lat || 1) * -0.5 };
            ram = true;
          }
        }
        if (onFoot) { stopAt = d - 9; ram = false; }
        ai.cutTimer = Math.max(0, (ai.cutTimer || 0) - dt);
      } else {
        // flank / intercept: race to an intersection ahead of the target's travel direction
        const hx = Math.hypot(pv.x, pv.z) > 2 ? pv.x : Math.sin(P.car ? P.car.yaw : P.yaw), hz = Math.hypot(pv.x, pv.z) > 2 ? pv.z : Math.cos(P.car ? P.car.yaw : P.yaw);
        const node = PT.nodeAhead(real.x, real.z, hx, hz, ai.role === "interceptor" ? 2 : 1);
        goal = { x: node[0] * CITY.PITCH, z: node[1] * CITY.PITCH };
        const gd = Math.hypot(goal.x - car.pos.x, goal.z - car.pos.z), passed = (goal.x - real.x) * hx + (goal.z - real.z) * hz < 0;
        if (gd < 12 || passed) ai.cutTimer = 4; // reached the cut-off point (or the target passed it): engage
      }
      drive(car, route(car, goal), { direct, ram, stopAt });
    }
    // stuck handling
    if (ai.reverse <= 0) {
      ai.stuck = sp < 1.5 && car.throttle > 0.3 ? (ai.stuck || 0) + dt : 0;
      if (ai.stuck > 1.0) {
        ai.stuck = 0; ai.reverse = 1.2; ai.stuckN = (ai.stuckN || 0) + 1;
        const l = raycastBoxes(car.pos.x, 1, car.pos.z, car.pos.x + Math.sin(car.yaw + 0.6) * 8, 1, car.pos.z + Math.cos(car.yaw + 0.6) * 8);
        const r = raycastBoxes(car.pos.x, 1, car.pos.z, car.pos.x + Math.sin(car.yaw - 0.6) * 8, 1, car.pos.z + Math.cos(car.yaw - 0.6) * 8);
        ai.revSteer = l > r ? -1 : 1; // reverse away from the open side so the nose swings toward it
        if (ai.stuckN >= 4 && d > 70) relocate(car);
      } else if (sp > 6) ai.stuckN = 0;
    }
    stepCar(car, dt, true);
    const on = Math.floor(t * 6) % 2 === 0;
    car.siren.mr.color.setRGB(on ? 8 : 0.3, on ? 0.3 : 0.02, on ? 0.2 : 0.02);
    car.siren.mb.color.setRGB(on ? 0.02 : 0.3, on ? 0.05 : 0.6, on ? 0.3 : 9);
  }

  // ---------- roadblocks (3+ stars, player driving): two cruisers across the road ahead, officers out
  function updateRoadblock(dt) {
    roadblockCd -= dt;
    if (roadblock) {
      const far = Math.hypot(roadblock.x - P.pos.x, roadblock.z - P.pos.z) > 230;
      if (far || P.stars === 0) { roadblock.cars.forEach(c => { if (!c.exploded) removeCar(c); }); roadblock = null; }
      else roadblock.cars.forEach(c => { const on = Math.floor(clock * 6) % 2 === 0; if (c.siren) { c.siren.mr.color.setRGB(on ? 8 : 0.3, 0.1, 0.1); c.siren.mb.color.setRGB(0.05, 0.1, on ? 0.3 : 9); } });
      return;
    }
    if (P.stars < 3 || !P.car || speedOf(P.car) < 8 || roadblockCd > 0) return;
    const v = P.car.vel, node = PT.nodeAhead(P.car.pos.x, P.car.pos.z, v.x, v.y, 2);
    const x = node[0] * CITY.PITCH, z = node[1] * CITY.PITCH, dist = Math.hypot(x - P.pos.x, z - P.pos.z);
    if (dist < 70 || dist > 180) return;
    const alongX = Math.abs(v.x) >= Math.abs(v.y), dirSign = alongX ? Math.sign(v.x) : Math.sign(v.y);
    // block the approach side of the intersection, cars broadside to the player's lane
    const bx = alongX ? x - dirSign * (CITY.HALF_R + 6) : x, bz = alongX ? z : z - dirSign * (CITY.HALF_R + 6);
    const cars = [];
    for (const s of [-1, 1]) {
      const c = makeCar("police");
      c.pos.set(bx + (alongX ? 0 : s * 3.1), 0, bz + (alongX ? s * 3.1 : 0));
      c.yaw = alongX ? (s > 0 ? Math.PI : 0) + 0.25 * s : (s > 0 ? Math.PI / 2 : -Math.PI / 2) + 0.25 * s;
      c.ai = { mode: "police", roadblock: true }; c.unloaded = false; syncCar(c); VEHICLES.push(c); cars.push(c);
      COMBAT.spawnOfficersFrom(c);
    }
    roadblock = { x: bx, z: bz, cars }; roadblockCd = 30;
    HUD.toast("전방에 경찰 바리케이드!", "warn");
  }

  // ---------- helicopter (4+ stars): follows, sees from above, searchlight, gunner at 5 stars
  function buildHeli() {
    const g = new THREE.Group();
    const dark = new THREE.MeshStandardMaterial({ color: col(0x1a2030), roughness: 0.4, metalness: 0.5 }), white = new THREE.MeshStandardMaterial({ color: col(0xe8e8e8), roughness: 0.4 });
    const body = new THREE.Mesh(new THREE.SphereGeometry(1.6, 16, 12), dark); body.scale.set(1, 0.9, 1.7); g.add(body);
    const stripe = new THREE.Mesh(new THREE.BoxGeometry(3.1, 0.35, 2), white); stripe.position.set(0, -0.2, 0.3); g.add(stripe);
    const tail = new THREE.Mesh(new THREE.CylinderGeometry(0.25, 0.45, 6, 8), dark); tail.rotation.x = Math.PI / 2; tail.position.set(0, 0.3, -4.6); g.add(tail);
    const fin = new THREE.Mesh(new THREE.BoxGeometry(0.1, 1.4, 0.9), dark); fin.position.set(0, 0.9, -7.4); g.add(fin);
    const skid = new THREE.Mesh(new THREE.BoxGeometry(0.1, 0.1, 3.4), dark); skid.position.set(0.9, -1.6, 0); g.add(skid); const s2 = skid.clone(); s2.position.x = -0.9; g.add(s2);
    const rotor = new THREE.Mesh(new THREE.BoxGeometry(11, 0.06, 0.35), dark); rotor.position.y = 1.7; g.add(rotor);
    const rotor2 = rotor.clone(); rotor2.rotation.y = Math.PI / 2; rotor.add(rotor2); rotor2.position.set(0, 0, 0);
    const blink = new THREE.Mesh(new THREE.SphereGeometry(0.15, 8, 6), M.redBlink); blink.position.set(0, -1.4, 1.5); g.add(blink);
    const coneMat = new THREE.MeshBasicMaterial({ color: new THREE.Color(1, 0.95, 0.8), transparent: true, opacity: 0.12, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide });
    const cone = new THREE.Mesh(new THREE.ConeGeometry(6, 44, 24, 1, true), coneMat); cone.position.y = -22; g.add(cone);
    const spot = new THREE.SpotLight(0xfff2d0, 0, 120, 0.16, 0.5, 1); spot.position.set(0, -1.5, 0); g.add(spot); g.add(spot.target);
    scene.add(g);
    return { g, rotor, cone, coneMat, spot, pos: new THREE.Vector3(P.pos.x + 120, 45, P.pos.z + 120), vel: new THREE.Vector3(), cd: 2, sees: false };
  }
  function updateHeli(dt, night) {
    if (P.stars < 4) { if (heli && (heli.pos.y > 140 || Math.hypot(heli.pos.x - P.pos.x, heli.pos.z - P.pos.z) > 160)) { scene.remove(heli.g); heli = null; } else if (heli) { heli.vel.y += 3 * dt; heli.pos.addScaledVector(heli.vel, dt); heli.g.position.copy(heli.pos); heli.sees = false; } return; }
    if (!heli) { heli = buildHeli(); HUD.toast("경찰 헬기가 떴습니다", "warn"); }
    const real = playerPos(), seen = !WANTED.st.searching;
    const goal = seen ? new THREE.Vector3(real.x, 0, real.z) : searchTarget(heli);
    const to = new THREE.Vector3(goal.x - heli.pos.x - 10, 45 - heli.pos.y, goal.z - heli.pos.z - 10);
    const desired = to.clone().setLength(Math.min(to.length() * 0.8, 30));
    heli.vel.lerp(desired, 1 - Math.exp(-1.2 * dt)); heli.pos.addScaledVector(heli.vel, dt);
    heli.g.position.copy(heli.pos); heli.g.rotation.set(clamp(heli.vel.length() * 0.012, 0, 0.3), Math.atan2(heli.vel.x, heli.vel.z), 0, "YXZ");
    heli.rotor.rotation.y += dt * 40;
    // searchlight points at the target / search point
    const aim = seen ? new THREE.Vector3(real.x, 0, real.z) : goal; const dir = aim.clone().sub(heli.pos);
    heli.cone.parent.updateMatrixWorld(); const local = heli.g.worldToLocal(aim.clone());
    heli.cone.quaternion.setFromUnitVectors(new THREE.Vector3(0, -1, 0), local.clone().normalize()); heli.cone.position.copy(local.clone().normalize().multiplyScalar(22));
    heli.cone.scale.set(1, clamp(dir.length() / 44, 0.6, 2), 1);
    heli.coneMat.opacity = 0.03 + night * 0.05; heli.spot.intensity = 0.6 + night * 2.4; heli.spot.target.position.copy(local);
    const horiz = Math.hypot(real.x - heli.pos.x, real.z - heli.pos.z);
    heli.sees = horiz < 75 && raycastBoxes(heli.pos.x, heli.pos.y - 2, heli.pos.z, real.x, 1.2, real.z) >= 0.999;
    if (heli.sees && P.stars >= 5 && GAME.state === "play") {
      heli.cd -= dt;
      if (heli.cd <= 0) {
        heli.cd = rand(1.2, 2); const hit = Math.random() < 0.3, end = new THREE.Vector3(real.x + (hit ? 0 : rand(-3, 3)), 1, real.z + (hit ? 0 : rand(-3, 3)));
        COMBAT.fxTracer(new THREE.Vector3(heli.pos.x, heli.pos.y - 1.5, heli.pos.z), end); AUDIO.shot(0.5, true);
        if (hit) { if (P.car) COMBAT.damageCar(P.car, 8, false); else { P.health -= 9; HUD.hurt(0.6); } }
      }
    }
  }

  return {
    update: updateCar,
    tick(dt, t, night) {
      clock = t;
      roleTimer -= dt;
      if (roleTimer <= 0) {
        roleTimer = 0.5; roles.clear();
        const chasers = VEHICLES.filter(v => v.type === "police" && v.ai && v.ai.mode === "police" && !v.ai.roadblock && !v.exploded);
        const real = playerPos(), ds = chasers.map(c => Math.hypot(c.pos.x - real.x, c.pos.z - real.z));
        const order = ds.map((d, i) => i).sort((a, b) => ds[a] - ds[b]);
        order.forEach((ci, rank) => roles.set(chasers[ci], rank === 1 ? "flanker" : rank === 2 ? "interceptor" : "pursuer"));
        for (const u of [...units.keys()]) if (u !== heli && !VEHICLES.includes(u) && !COMBAT.OFFICERS.includes(u)) { units.delete(u); legs.delete(u); }
      }
      updateRoadblock(dt);
      updateHeli(dt, night);
    },
    searchTarget,
    heliSees() { return !!(heli && heli.sees); },
    debugRoadblockNow() { roadblockCd = 0; }, // test hook
    get heli() { return heli; },
  };
})();
