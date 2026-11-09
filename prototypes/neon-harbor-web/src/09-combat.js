// ---------------------------------------------------------------- combat: weapons, officers, fire & explosions, pickups
// Mirrors Assets/NeonHarbor/Scripts/Core/Combat.cs (weapon specs, damage, armour, hit tests).
const WEAPONS = {
  fist:   { name: "주먹", melee: true, damage: 18, rate: 0.42, range: 1.7 },
  pistol: { name: "권총", damage: 34, rate: 0.26, mag: 12, reload: 1.3, spread: 0.010, range: 140, start: 48 },
  smg:    { name: "기관단총", damage: 19, rate: 0.085, mag: 30, reload: 1.8, spread: 0.028, range: 100, start: 120, auto: true },
};
const WEAPON_ORDER = ["fist", "pistol", "smg"];

const COMBAT = (() => {
  const P = PLAYER;
  P.armor = 0; P.weapon = "fist"; P.cooldown = 0; P.reload = 0; P.aimHold = 0; P.punch = 0;
  P.arsenal = { fist: { owned: true }, pistol: { owned: false, clip: 0, reserve: 0 }, smg: { owned: false, clip: 0, reserve: 0 } };
  const OFFICERS = [];
  let hitMarker = 0, gunfireHeatCd = 0, gunMesh = null;

  // ---------- damage model (same rules as Core/Combat.cs)
  function applyDamage(target, amount) { // target has health (+ optional armor); returns true when it drops to 0
    if (target.armor > 0) { const absorbed = Math.min(target.armor, amount * 0.66); target.armor -= absorbed; amount -= absorbed; }
    target.health -= amount;
    return target.health <= 0;
  }

  // ---------- effects (pooled sprites)
  function spriteTex(kind) {
    const c = document.createElement("canvas"); c.width = c.height = 64; const g = c.getContext("2d");
    const grd = g.createRadialGradient(32, 32, 0, 32, 32, 32);
    if (kind === "smoke") { grd.addColorStop(0, "rgba(255,255,255,0.75)"); grd.addColorStop(0.6, "rgba(255,255,255,0.35)"); grd.addColorStop(1, "rgba(255,255,255,0)"); }
    else { grd.addColorStop(0, "rgba(255,255,255,1)"); grd.addColorStop(0.3, "rgba(255,255,255,0.7)"); grd.addColorStop(1, "rgba(255,255,255,0)"); }
    g.fillStyle = grd; g.fillRect(0, 0, 64, 64); return new THREE.CanvasTexture(c);
  }
  const TEX_FIRE = spriteTex("fire"), TEX_SMOKE = spriteTex("smoke");
  const FX = [];
  function emit(kind, pos, vel, life, size, grow = 1) {
    let s = FX.find(f => !f.alive && f.kind === kind);
    if (!s) {
      if (FX.length > 420) return;
      const additive = kind !== "smoke" && kind !== "hit";
      const mat = new THREE.SpriteMaterial({ map: kind === "smoke" ? TEX_SMOKE : TEX_FIRE, transparent: true, depthWrite: false, blending: additive ? THREE.AdditiveBlending : THREE.NormalBlending, fog: true });
      s = { kind, sprite: new THREE.Sprite(mat), alive: false }; dynamic.add(s.sprite); FX.push(s);
    }
    const m = s.sprite.material;
    if (kind === "fire") m.color.setRGB(4, 1.6, 0.4); else if (kind === "spark") m.color.setRGB(5, 3.5, 1.2);
    else if (kind === "flash") m.color.setRGB(8, 6, 3); else if (kind === "hit") m.color.setRGB(0.35, 0.03, 0.03); else m.color.setRGB(0.18, 0.18, 0.2);
    Object.assign(s, { alive: true, pos: pos.clone(), vel: vel.clone(), life, age: 0, size, grow });
    s.sprite.visible = true;
  }
  function updateFX(dt) {
    for (const s of FX) {
      if (!s.alive) continue;
      s.age += dt; if (s.age >= s.life) { s.alive = false; s.sprite.visible = false; continue; }
      const k = s.age / s.life;
      s.pos.addScaledVector(s.vel, dt);
      if (s.kind === "spark" || s.kind === "hit") s.vel.y -= 9 * dt; else if (s.kind === "smoke") s.vel.multiplyScalar(1 - dt * 0.6);
      s.sprite.position.copy(s.pos);
      s.sprite.scale.setScalar(s.size * (1 + (s.grow - 1) * k));
      s.sprite.material.opacity = s.kind === "smoke" ? 0.55 * (1 - k) : 1 - k;
    }
  }
  const flashLight = new THREE.PointLight(0xffc070, 0, 14, 2); scene.add(flashLight);
  const boomLight = new THREE.PointLight(0xff8a3c, 0, 60, 2); scene.add(boomLight);
  const tracers = [];
  function tracer(a, b) {
    let t = tracers.find(x => !x.alive);
    if (!t) {
      const g = new THREE.BufferGeometry().setFromPoints([a, b]);
      t = { line: new THREE.Line(g, new THREE.LineBasicMaterial({ color: new THREE.Color(5, 4, 2.5), transparent: true, blending: THREE.AdditiveBlending, depthWrite: false })), alive: false };
      scene.add(t.line); tracers.push(t);
    }
    const p = t.line.geometry.attributes.position; p.setXYZ(0, a.x, a.y, a.z); p.setXYZ(1, b.x, b.y, b.z); p.needsUpdate = true; t.line.geometry.computeBoundingSphere();
    t.alive = true; t.age = 0; t.line.visible = true;
  }

  // ---------- weapon model in the player's hand
  function buildGun() {
    const g = new THREE.Group();
    const dark = new THREE.MeshStandardMaterial({ color: col(0x1b1d20), roughness: 0.4, metalness: 0.8 });
    const body = new THREE.Mesh(new THREE.BoxGeometry(0.05, 0.08, 0.2), dark); body.position.set(0, 0, 0.06); g.add(body);
    const grip = new THREE.Mesh(new THREE.BoxGeometry(0.045, 0.11, 0.05), dark); grip.position.set(0, -0.07, 0); grip.rotation.x = 0.25; g.add(grip);
    const smgPart = new THREE.Mesh(new THREE.BoxGeometry(0.055, 0.07, 0.24), dark); smgPart.position.set(0, 0.0, 0.26); g.add(smgPart);
    const mag = new THREE.Mesh(new THREE.BoxGeometry(0.04, 0.16, 0.05), dark); mag.position.set(0, -0.1, 0.13); g.add(mag);
    g.userData = { smgPart, mag };
    g.rotation.x = Math.PI / 2; g.position.set(0, -0.46, 0.02);
    P.rig.foreR.add(g);
    return g;
  }

  // ---------- hit testing
  const _ray = new THREE.Ray();
  function rayVsCylinder(o, d, cx, cz, y0, y1, r) { // vertical capsule-ish cylinder
    const ox = o.x - cx, oz = o.z - cz, a = d.x * d.x + d.z * d.z, b = 2 * (ox * d.x + oz * d.z), c = ox * ox + oz * oz - r * r;
    if (a < 1e-8) return -1;
    const disc = b * b - 4 * a * c; if (disc < 0) return -1;
    const t = (-b - Math.sqrt(disc)) / (2 * a); if (t < 0) return -1;
    const y = o.y + d.y * t; return y >= y0 && y <= y1 ? t : -1;
  }
  function rayVsCar(o, d, v) {
    const s = Math.sin(-v.yaw), cc = Math.cos(-v.yaw); // into car-local space (local +z = forward)
    const lx = o.x - v.pos.x, lz = o.z - v.pos.z, ox = lx * cc + lz * s, oz = -lx * s + lz * cc;
    const dx = d.x * cc + d.z * s, dz = -d.x * s + d.z * cc;
    const mn = [-v.spec.wid / 2, v.pos.y + 0.25, -v.spec.len / 2], mx = [v.spec.wid / 2, v.pos.y + 1.5, v.spec.len / 2];
    const oo = [ox, o.y, oz], dd = [dx, d.y, dz]; let t0 = 0, t1 = 1e9;
    for (let k = 0; k < 3; k++) {
      if (Math.abs(dd[k]) < 1e-9) { if (oo[k] < mn[k] || oo[k] > mx[k]) return -1; continue; }
      let ta = (mn[k] - oo[k]) / dd[k], tb = (mx[k] - oo[k]) / dd[k]; if (ta > tb) [ta, tb] = [tb, ta];
      t0 = Math.max(t0, ta); t1 = Math.min(t1, tb); if (t0 > t1) return -1;
    }
    return t0;
  }
  // first thing hit along o + d·t, t ∈ (tMin, range)
  function castShot(o, d, range, tMin, ignoreCar) {
    let best = { t: range, kind: "none" };
    const wall = raycastBoxes(o.x, o.y, o.z, o.x + d.x * range, o.y + d.y * range, o.z + d.z * range) * range;
    if (wall < best.t) best = { t: wall, kind: "wall" };
    if (d.y < -1e-4) { const tg = -o.y / d.y; if (tg > tMin && tg < best.t) best = { t: tg, kind: "ground" }; }
    for (const p of PEDS) {
      if (p.state === "gone" || p.state === "down" || p.state === "air") continue;
      const t = rayVsCylinder(o, d, p.pos.x, p.pos.z, p.pos.y, p.pos.y + 1.8, 0.38);
      if (t > tMin && t < best.t) best = { t, kind: "ped", ped: p };
    }
    for (const of of OFFICERS) {
      if (of.down) continue;
      const t = rayVsCylinder(o, d, of.pos.x, of.pos.z, of.pos.y, of.pos.y + 1.8, 0.38);
      if (t > tMin && t < best.t) best = { t, kind: "officer", officer: of };
    }
    for (const v of VEHICLES) {
      if (v === ignoreCar || v.exploded) continue;
      if (Math.abs(v.pos.x - o.x) > range + 5 || Math.abs(v.pos.z - o.z) > range + 5) continue;
      const t = rayVsCar(o, d, v);
      if (t > tMin && t < best.t) best = { t, kind: "car", car: v };
    }
    return best;
  }

  // ---------- reactions
  function scarePeds(at, radius, seconds = 6) {
    for (const p of PEDS) if ((p.state === "walk" || p.state === "flee") && p.pos.distanceTo(at) < radius) { p.state = "flee"; p.timer = seconds; p.threat = at.clone(); }
  }
  function copNearby(r) {
    for (const v of VEHICLES) if (v.type === "police" && !v.exploded && Math.hypot(v.pos.x - P.pos.x, v.pos.z - P.pos.z) < r) return true;
    return OFFICERS.some(o => !o.down && o.pos.distanceTo(P.pos) < r);
  }
  function damagePed(p, amount, dir, byPlayer) {
    p.health = p.health ?? 40;
    p.health -= amount;
    for (let k = 0; k < 3; k++) emit("hit", new THREE.Vector3(p.pos.x, p.pos.y + 1.2, p.pos.z), new THREE.Vector3(dir.x * 2 + rand(-1, 1), rand(0.5, 2), dir.z * 2 + rand(-1, 1)), 0.35, 0.18);
    if (p.health <= 0) {
      knockPed(p, dir.x * 3, dir.z * 3, false);
      if (byPlayer) addHeat(1, "보행자 공격");
    } else { p.state = "flee"; p.timer = 7; p.threat = P.pos.clone(); }
  }
  function damageCar(v, amount, byPlayer) {
    v.health -= amount;
    if (v.ai && v.ai.mode === "drive" && amount > 0) { v.ai.mode = "wreck"; } // drivers bail out of the lane when shot at
    if (v.health <= 0 && !v.burning && !v.exploded) { v.burning = 4.5; v.burnByPlayer = byPlayer; }
  }
  function explode(v) {
    v.exploded = true; v.burning = 0; v.health = 0;
    const c = v.pos.clone(); c.y += 1;
    AUDIO.explosion(); CAMERA.shake = Math.max(CAMERA.shake, clamp(1.2 - c.distanceTo(P.pos) / 40, 0.2, 1));
    boomLight.position.copy(c); boomLight.intensity = 12;
    for (let k = 0; k < 26; k++) emit("fire", c, new THREE.Vector3(rand(-6, 6), rand(1, 9), rand(-6, 6)), rand(0.5, 1.1), rand(1.2, 2.6), 2.2);
    for (let k = 0; k < 20; k++) emit("smoke", c, new THREE.Vector3(rand(-2, 2), rand(2, 6), rand(-2, 2)), rand(2.5, 4.5), rand(2, 3.5), 3);
    emit("flash", c, new THREE.Vector3(), 0.25, 9, 2);
    // burnt shell
    v.root.traverse(m => { if (m.isMesh && m.material && m.material.isMeshPhysicalMaterial) m.material = BURNT; });
    v.vel.x += rand(-2, 2); v.vel.y += rand(-2, 2); v.yawRate += rand(-2, 2); v.body.position.y = 0.25; v.body.rotation.z = rand(-0.15, 0.15);
    if (v.ai) v.ai.mode = "parked";
    if (v.driverMesh && v !== P.car) { v.body.remove(v.driverMesh.root); v.driverMesh = null; }
    // blast
    const R = 8;
    for (const p of PEDS) { const d = p.pos.distanceTo(c); if (d < R && p.state !== "down") knockPed(p, (p.pos.x - c.x) / d * 9, (p.pos.z - c.z) / d * 9, false); }
    for (const o of OFFICERS) { const d = o.pos.distanceTo(c); if (d < R) hurtOfficer(o, 120 * (1 - d / R), false); }
    for (const w of VEHICLES) { if (w === v || w.exploded) continue; const d = w.pos.distanceTo(v.pos); if (d < R) { damageCar(w, 70 * (1 - d / R), v.burnByPlayer); w.vel.x += (w.pos.x - v.pos.x) / d * 6; w.vel.y += (w.pos.z - v.pos.z) / d * 6; } }
    const pd = c.distanceTo(P.pos);
    if (pd < R) {
      if (P.car === v) { P.health = 0; }
      else if (P.car) { damageCar(P.car, 60 * (1 - pd / R), true); }
      else { applyDamage(P, 110 * (1 - pd / R)); P.knock((P.pos.x - c.x) / pd * 8, (P.pos.z - c.z) / pd * 8, 6); }
      HUD.hurt(1);
    }
    scarePeds(c, 60, 8);
    if (v.burnByPlayer) addHeat(1, "차량 폭발");
  }
  const BURNT = new THREE.MeshStandardMaterial({ color: col(0x161412), roughness: 0.95, metalness: 0.2 });

  // ---------- officers on foot
  function spawnOfficersFrom(car) {
    car.unloaded = true;
    const f = forwardOf(car), side = new THREE.Vector2(-f.y, f.x);
    for (const s of [1, -1]) {
      const rig = makeHuman({ top: 0x1d2a44, bottom: 0x1d2a44, skin: pick(SKIN), hair: 0x111111 });
      const cap = new THREE.Mesh(new THREE.CylinderGeometry(0.15, 0.15, 0.08, 14), new THREE.MeshStandardMaterial({ color: col(0x141a26), roughness: 0.6 }));
      cap.position.set(0, 0.11, 0); rig.head.add(cap);
      const gun = new THREE.Mesh(new THREE.BoxGeometry(0.05, 0.08, 0.2), new THREE.MeshStandardMaterial({ color: col(0x1b1d20), roughness: 0.4, metalness: 0.8 }));
      gun.rotation.x = Math.PI / 2; gun.position.set(0, -0.46, 0.06); rig.foreR.add(gun);
      dynamic.add(rig.root);
      const pos = new THREE.Vector3(car.pos.x + side.x * s * 1.9, 0.1, car.pos.z + side.y * s * 1.9);
      OFFICERS.push({ rig, pos, yaw: car.yaw, health: 60, armor: 0, down: false, downT: 0, cd: rand(0.4, 1.2), vel: new THREE.Vector3() });
    }
    if (car.driverMesh) { car.body.remove(car.driverMesh.root); car.driverMesh = null; }
    car.ai.mode = "parked"; car.throttle = 0; car.brake = 1;
  }
  function hurtOfficer(o, amount, byPlayer) {
    if (o.down) return;
    for (let k = 0; k < 3; k++) emit("hit", new THREE.Vector3(o.pos.x, o.pos.y + 1.2, o.pos.z), new THREE.Vector3(rand(-1, 1), rand(0.5, 2), rand(-1, 1)), 0.35, 0.18);
    if (applyDamage(o, amount)) {
      o.down = true; o.downT = 14; o.rig.root.rotation.set(-Math.PI / 2, o.yaw, 0, "YXZ"); o.pos.y = CITY.groundHeight(o.pos.x, o.pos.z) + 0.12;
      if (byPlayer) addHeat(1, "경찰관 공격");
      // drop ammo
      dropPickup("ammo", o.pos.clone());
    } else if (byPlayer && P.stars < 2) addHeat(2 - P.heat + 0.01, "경찰관 공격");
  }
  function lineOfSight(a, b) { return raycastBoxes(a.x, a.y, a.z, b.x, b.y, b.z) >= 0.999; }
  function updateOfficers(dt) {
    for (let k = OFFICERS.length - 1; k >= 0; k--) {
      const o = OFFICERS[k];
      if (o.down) { o.downT -= dt; if (o.downT < 0 || o.pos.distanceTo(P.pos) > 150) { dynamic.remove(o.rig.root); OFFICERS.splice(k, 1); } else o.rig.root.position.copy(o.pos); continue; }
      if (P.stars === 0 && o.pos.distanceTo(P.pos) > 60) { dynamic.remove(o.rig.root); OFFICERS.splice(k, 1); continue; }
      const tgt = P.car ? P.car.pos : P.pos, d = Math.hypot(tgt.x - o.pos.x, tgt.z - o.pos.z);
      const eye = new THREE.Vector3(o.pos.x, o.pos.y + 1.5, o.pos.z), aimAt = new THREE.Vector3(tgt.x, (P.car ? 0.9 : P.pos.y + 1.2), tgt.z);
      const los = d < 70 && lineOfSight(eye, aimAt);
      const want = Math.atan2(tgt.x - o.pos.x, tgt.z - o.pos.z);
      o.yaw += angDiff(o.yaw, want) * (1 - Math.exp(-10 * dt));
      const shooting = P.stars >= 2 && los && d < 32 && P.alive && GAME.state === "play";
      let speed = 0;
      if (P.stars === 0) speed = 0;
      else if (!shooting || d > 16) speed = d > 3 ? CFG.player.run * 0.85 : 0;
      o.vel.x = damp(o.vel.x, Math.sin(o.yaw) * speed, 10, dt); o.vel.z = damp(o.vel.z, Math.cos(o.yaw) * speed, 10, dt);
      o.pos.x += o.vel.x * dt; o.pos.z += o.vel.z * dt;
      const c = { x: o.pos.x, z: o.pos.z }; collideCircle(c, 0.4); o.pos.x = c.x; o.pos.z = c.z;
      o.pos.y = CITY.groundHeight(o.pos.x, o.pos.z);
      o.rig.root.position.copy(o.pos); o.rig.root.rotation.set(0, o.yaw, 0);
      animateHuman(o.rig, Math.hypot(o.vel.x, o.vel.z), dt);
      if (shooting) {
        o.rig.armR.rotation.x = -Math.PI / 2 + 0.1; o.rig.foreR.rotation.x = 0; o.rig.armL.rotation.x = -1.2;
        o.cd -= dt;
        if (o.cd <= 0) {
          o.cd = rand(0.55, 1.0);
          const muzzle = new THREE.Vector3(o.pos.x + Math.sin(o.yaw) * 0.6, o.pos.y + 1.45, o.pos.z + Math.cos(o.yaw) * 0.6);
          const moving = P.car ? speedOf(P.car) : Math.hypot(P.vel.x, P.vel.z);
          const chance = clamp(0.55 - d * 0.012 - moving * 0.012, 0.08, 0.55);
          const hit = Math.random() < chance;
          const end = hit ? aimAt.clone() : aimAt.clone().add(new THREE.Vector3(rand(-2, 2), rand(-0.5, 1.5), rand(-2, 2)));
          tracer(muzzle, end); emit("flash", muzzle, new THREE.Vector3(), 0.06, 0.5);
          flashLight.position.copy(muzzle); flashLight.intensity = 3;
          AUDIO.shot(clamp(1 - d / 60, 0.15, 0.8), false);
          if (hit) {
            if (P.car) damageCar(P.car, 6, false);
            else { applyDamage(P, 7); HUD.hurt(0.5); }
          } else emit("spark", end, new THREE.Vector3(rand(-1, 1), 2, rand(-1, 1)), 0.25, 0.12);
        }
      }
    }
  }

  // ---------- pickups (weapons, ammo, health, armour)
  const PICKS = [];
  const PICK_STYLE = { pistol: [3, 2.4, 0.4], smg: [3, 1.3, 0.2], ammo: [2.6, 2.2, 0.6], health: [0.4, 3.2, 0.6], armor: [0.4, 1.4, 4] };
  function pickupMesh(kind) {
    const g = new THREE.Group(), m = new THREE.MeshBasicMaterial({ color: new THREE.Color(...PICK_STYLE[kind]) });
    if (kind === "health") { g.add(new THREE.Mesh(new THREE.BoxGeometry(0.5, 0.16, 0.16), m)); g.add(new THREE.Mesh(new THREE.BoxGeometry(0.16, 0.5, 0.16), m)); }
    else if (kind === "armor") { const s = new THREE.Mesh(new THREE.CylinderGeometry(0.3, 0.22, 0.42, 6), m); g.add(s); }
    else if (kind === "ammo") { for (let k = 0; k < 3; k++) { const b = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.05, 0.3, 8), m); b.position.x = (k - 1) * 0.13; g.add(b); } }
    else { const b = new THREE.Mesh(new THREE.BoxGeometry(0.1, 0.14, kind === "smg" ? 0.62 : 0.34), m); g.add(b); const gr = new THREE.Mesh(new THREE.BoxGeometry(0.09, 0.2, 0.1), m); gr.position.set(0, -0.14, kind === "smg" ? -0.12 : -0.08); g.add(gr); }
    const ring = new THREE.Mesh(new THREE.TorusGeometry(0.6, 0.03, 6, 32), m); ring.rotation.x = Math.PI / 2; ring.position.y = -0.75; g.add(ring);
    MARKERS.add(g); return g;
  }
  function dropPickup(kind, pos, respawn = 0) {
    const mesh = pickupMesh(kind); mesh.position.set(pos.x, CITY.groundHeight(pos.x, pos.z) + 0.85, pos.z);
    PICKS.push({ kind, mesh, home: respawn ? pos.clone() : null, respawn, wait: 0 });
  }
  function placeWorldPickups() {
    const S = SPAWN;
    dropPickup("pistol", new THREE.Vector3(S.x - 6, 0.18, S.z + 1.2), 30);
    dropPickup("health", new THREE.Vector3(S.x + 6, 0.18, S.z + 1.2), 30);
    const spots = [["smg", 4.5, 2.5], ["armor", 2.5, 3.5], ["ammo", 5.5, 4.5], ["pistol", 1.5, 1.5], ["health", 6.5, 6.5], ["ammo", 2.5, 6.5], ["armor", 6.5, 2.5], ["smg", 1.5, 5.5]];
    for (const [kind, bi, bj] of spots) {
      const blk = CITY.blockAt(bi * CITY.PITCH, bj * CITY.PITCH) || CITY.blocks[0];
      const q = pedPathPoint(blk, 0.13); dropPickup(kind, new THREE.Vector3(q[0], 0.18, q[1]), 45);
    }
  }
  function takePickup(p) {
    const A = P.arsenal;
    if (p.kind === "pistol" || p.kind === "smg") {
      const w = A[p.kind], spec = WEAPONS[p.kind];
      if (!w.owned) { w.owned = true; w.clip = spec.mag; w.reserve = spec.start - spec.mag; P.weapon = p.kind; HUD.toast(`${spec.name} 획득 — 우클릭 조준, 좌클릭 사격`, "good"); }
      else { w.reserve += spec.mag * 2; HUD.toast(`${spec.name} 탄약 +${spec.mag * 2}`, "good"); }
    } else if (p.kind === "ammo") {
      let any = false; for (const k of ["pistol", "smg"]) if (A[k].owned) { A[k].reserve += WEAPONS[k].mag * 2; any = true; }
      if (!any) return false; HUD.toast("탄약 보충", "good");
    } else if (p.kind === "health") { if (P.health >= 100) return false; P.health = 100; HUD.toast("체력 회복", "good"); }
    else if (p.kind === "armor") { if (P.armor >= 100) return false; P.armor = 100; HUD.toast("방탄복 착용", "good"); }
    AUDIO.cash(); return true;
  }
  function updatePickups(dt, t) {
    for (let k = PICKS.length - 1; k >= 0; k--) {
      const p = PICKS[k];
      if (p.wait > 0) { p.wait -= dt; if (p.wait <= 0) p.mesh.visible = true; continue; }
      p.mesh.rotation.y += dt * 2; p.mesh.children.forEach(c => { if (c.geometry.type === "TorusGeometry") c.rotation.z = t; });
      p.mesh.position.y = CITY.groundHeight(p.mesh.position.x, p.mesh.position.z) + 0.85 + Math.sin(t * 3 + k) * 0.08;
      if (!P.car && Math.hypot(p.mesh.position.x - P.pos.x, p.mesh.position.z - P.pos.z) < 1.3 && takePickup(p)) {
        if (p.respawn) { p.mesh.visible = false; p.wait = p.respawn; } else { MARKERS.remove(p.mesh); PICKS.splice(k, 1); }
      }
    }
  }

  // ---------- player weapons
  function cycle(dir) {
    let i = WEAPON_ORDER.indexOf(P.weapon);
    for (let n = 0; n < 3; n++) { i = (i + dir + 3) % 3; if (P.arsenal[WEAPON_ORDER[i]].owned) break; }
    P.weapon = WEAPON_ORDER[i]; P.reload = 0;
  }
  function startReload() {
    const w = P.arsenal[P.weapon], spec = WEAPONS[P.weapon];
    if (spec.melee || P.reload > 0 || w.clip >= spec.mag || w.reserve <= 0) return;
    P.reload = spec.reload; AUDIO.click(true);
  }
  function aimRay() {
    const o = camera.position.clone(), d = new THREE.Vector3(); camera.getWorldDirection(d); return { o, d };
  }
  function fire() {
    const spec = WEAPONS[P.weapon], w = P.arsenal[P.weapon];
    if (spec.melee) return punch();
    if (P.reload > 0) return;
    if (w.clip <= 0) { if (w.reserve > 0) startReload(); else { AUDIO.click(false); P.cooldown = 0.3; } return; }
    w.clip--; P.cooldown = spec.rate; P.aimHold = 1.2;
    const { o, d } = aimRay();
    const aiming = INPUT.aim;
    const spread = spec.spread * (aiming ? 1 : 2.2) * (Math.hypot(P.vel.x, P.vel.z) > 3 ? 1.6 : 1);
    d.x += rand(-spread, spread); d.y += rand(-spread, spread); d.z += rand(-spread, spread); d.normalize();
    // touch: soft auto-aim at the nearest target in a 12° cone
    if (matchMedia("(hover: none)").matches) {
      let best = null, bestDot = Math.cos(0.21);
      const cand = [...PEDS.filter(p => p.state === "walk" || p.state === "flee").map(p => p.pos), ...OFFICERS.filter(x => !x.down).map(x => x.pos)];
      for (const p of cand) { const v = new THREE.Vector3(p.x, p.y + 1.2, p.z).sub(o); const dist = v.length(); v.normalize(); const dt2 = v.dot(d); if (dist < 45 && dt2 > bestDot) { bestDot = dt2; best = v; } }
      if (best) d.copy(best);
    }
    const startT = o.distanceTo(P.car ? P.car.pos : P.pos) - 0.2; // ignore everything between camera and player
    const hit = castShot(o, d, spec.range + startT, Math.max(0.5, startT), P.car);
    const end = o.clone().addScaledVector(d, hit.t);
    const muzzle = P.car ? new THREE.Vector3(P.car.pos.x, 1.3, P.car.pos.z) : new THREE.Vector3(); if (!P.car) P.rig.foreR.localToWorld(muzzle.set(0, -0.5, 0.42));
    tracer(muzzle, end); emit("flash", muzzle, new THREE.Vector3(), 0.05, P.weapon === "smg" ? 0.55 : 0.45);
    flashLight.position.copy(muzzle); flashLight.intensity = 4;
    AUDIO.shot(1, P.weapon === "smg");
    CAMERA.shake = Math.max(CAMERA.shake, P.weapon === "smg" ? 0.08 : 0.14);
    const dir = d.clone(); dir.y = 0; dir.normalize();
    if (hit.kind === "ped") { damagePed(hit.ped, spec.damage, dir, true); hitMarker = 0.18; }
    else if (hit.kind === "officer") { hurtOfficer(hit.officer, spec.damage, true); hitMarker = 0.18; }
    else if (hit.kind === "car") { damageCar(hit.car, spec.damage * 0.7, true); hitMarker = 0.12; for (let k = 0; k < 4; k++) emit("spark", end, new THREE.Vector3(rand(-2, 2), rand(1, 3), rand(-2, 2)), 0.3, 0.1); if (hit.car.type === "police") addHeat(1, "경찰차 사격"); }
    else if (hit.kind !== "none") for (let k = 0; k < 4; k++) emit("spark", end, new THREE.Vector3(rand(-2, 2), rand(1, 3), rand(-2, 2)), 0.3, 0.1);
    // gunfire is noticed
    scarePeds(P.pos, 45);
    if (gunfireHeatCd <= 0 && copNearby(55)) { addHeat(1, "총기 사용"); gunfireHeatCd = 6; }
    if (w.clip === 0 && w.reserve > 0) startReload();
  }
  function punch() {
    P.cooldown = WEAPONS.fist.rate; P.punch = 0.25;
    const f = new THREE.Vector2(Math.sin(P.yaw), Math.cos(P.yaw));
    let target = null, best = WEAPONS.fist.range;
    for (const p of PEDS) {
      if (p.state !== "walk" && p.state !== "flee") continue;
      const dx = p.pos.x - P.pos.x, dz = p.pos.z - P.pos.z, d = Math.hypot(dx, dz);
      if (d < best && (dx * f.x + dz * f.y) / Math.max(d, 1e-3) > 0.5) { best = d; target = p; }
    }
    let officer = null;
    for (const o of OFFICERS) { if (o.down) continue; const dx = o.pos.x - P.pos.x, dz = o.pos.z - P.pos.z, d = Math.hypot(dx, dz); if (d < best && (dx * f.x + dz * f.y) / Math.max(d, 1e-3) > 0.5) { best = d; officer = o; target = null; } }
    AUDIO.swing();
    if (target) {
      AUDIO.thud(0.5); hitMarker = 0.15;
      target.health = (target.health ?? 40) - WEAPONS.fist.damage;
      if (target.health <= 0) { knockPed(target, f.x * 3, f.y * 3, false); addHeat(0.5, "폭행"); }
      else { target.state = "flee"; target.timer = 6; target.threat = P.pos.clone(); target.pos.x += f.x * 0.4; target.pos.z += f.y * 0.4; addHeat(0.25, "폭행"); }
      if (copNearby(30)) addHeat(1, "경찰 앞에서 폭행");
    } else if (officer) { AUDIO.thud(0.5); hitMarker = 0.15; hurtOfficer(officer, WEAPONS.fist.damage, true); }
  }

  function updatePlayerWeapons(dt) {
    if (!gunMesh) gunMesh = buildGun();
    if (INPUT.pressed.has("Digit1")) { P.weapon = "fist"; P.reload = 0; }
    if (INPUT.pressed.has("Digit2") && P.arsenal.pistol.owned) { P.weapon = "pistol"; P.reload = 0; }
    if (INPUT.pressed.has("Digit3") && P.arsenal.smg.owned) { P.weapon = "smg"; P.reload = 0; }
    if (INPUT.pressed.has("KeyQ") || INPUT.pressed.has("WeaponNext")) cycle(1);
    if (INPUT.pressed.has("KeyR")) startReload();
    P.cooldown -= dt; P.aimHold -= dt; P.punch -= dt; gunfireHeatCd -= dt; hitMarker -= dt;
    if (P.reload > 0) {
      P.reload -= dt;
      if (P.reload <= 0) { const w = P.arsenal[P.weapon], spec = WEAPONS[P.weapon]; const n = Math.min(spec.mag - w.clip, w.reserve); w.clip += n; w.reserve -= n; AUDIO.click(true); }
    }
    const spec = WEAPONS[P.weapon];
    const canAct = GAME.state === "play" && P.downTimer <= 0 && $("intro").hidden;
    const held = INPUT.fire || isDown("Fire"), pressedNow = INPUT.firePressed || INPUT.pressed.has("Fire");
    const trigger = spec.auto ? held : pressedNow;
    if (canAct && trigger && P.cooldown <= 0 && !(P.car && spec.melee)) fire();
    INPUT.firePressed = false;
    // visuals: gun model + aim pose (on foot)
    gunMesh.visible = !spec.melee;
    gunMesh.userData.smgPart.visible = P.weapon === "smg"; gunMesh.userData.mag.visible = P.weapon === "smg";
    if (!P.car && P.downTimer <= 0) {
      const aiming = !spec.melee && (INPUT.aim || P.aimHold > 0);
      if (aiming) {
        P.yaw += angDiff(P.yaw, CAMERA.yaw) * (1 - Math.exp(-18 * dt));
        const r = P.rig; const pitch = -CAMERA.pitch * 0.8;
        r.armR.rotation.set(-Math.PI / 2 + pitch, 0, -0.12); r.foreR.rotation.set(0, 0, 0);
        r.armL.rotation.set(-Math.PI / 2 + pitch + 0.1, 0, 0.5); r.foreL.rotation.set(-0.25, 0, 0);
        if (P.reload > 0) { r.armL.rotation.x = -0.6; r.foreL.rotation.x = -1.4; }
      } else if (P.punch > 0) {
        const k = P.punch / 0.25; P.rig.armR.rotation.set(-Math.PI / 2 * Math.sin(k * Math.PI), 0, 0.2); P.rig.foreR.rotation.set(-0.3 * k, 0, 0);
      }
    }
  }

  function updateBurning(dt) {
    for (const v of VEHICLES) {
      if (!v.burning || v.exploded) continue;
      v.burning -= dt;
      const eng = new THREE.Vector3(v.pos.x + Math.sin(v.yaw) * v.spec.len * 0.35, 1.1, v.pos.z + Math.cos(v.yaw) * v.spec.len * 0.35);
      if (Math.random() < dt * 30) emit("fire", eng, new THREE.Vector3(rand(-0.5, 0.5), rand(1.5, 3), rand(-0.5, 0.5)), 0.5, 0.8, 1.6);
      if (Math.random() < dt * 14) emit("smoke", eng, new THREE.Vector3(rand(-0.5, 0.5), rand(2, 4), rand(-0.5, 0.5)), 2.5, 1.2, 3);
      if (v === P.car && Math.floor(v.burning * 2) !== Math.floor((v.burning + dt) * 2)) HUD.toast("차에 불이 붙었습니다! F로 내리세요", "warn");
      if (v.burning <= 0) explode(v);
    }
    // smoking wrecks after explosion
    for (const v of VEHICLES) if (v.exploded && Math.random() < dt * 4) emit("smoke", new THREE.Vector3(v.pos.x, 1, v.pos.z), new THREE.Vector3(rand(-0.3, 0.3), rand(1.5, 3), rand(-0.3, 0.3)), 3, 1.2, 3);
  }

  return {
    OFFICERS,
    init() { placeWorldPickups(); },
    get hitMarker() { return hitMarker; },
    nearestOfficer() { let n = 1e9; for (const o of OFFICERS) if (!o.down) n = Math.min(n, o.pos.distanceTo(P.pos)); return n; },
    update(dt, t) {
      updatePlayerWeapons(dt);
      // police leave their cars to fight on foot once they're close
      if (P.stars >= 2) for (const v of VEHICLES) {
        if (v.type !== "police" || v.unloaded || !v.ai || v.ai.mode !== "police" || v.exploded) continue;
        const d = Math.hypot(v.pos.x - P.pos.x, v.pos.z - P.pos.z);
        if (d < (P.car ? 14 : 26) && speedOf(v) < (P.car ? 3 : 9)) spawnOfficersFrom(v);
      }
      updateOfficers(dt); updateBurning(dt); updatePickups(dt, t); updateFX(dt);
      for (const tr of tracers) if (tr.alive) { tr.age += dt; tr.line.material.opacity = 1 - tr.age / 0.07; if (tr.age > 0.07) { tr.alive = false; tr.line.visible = false; } }
      flashLight.intensity = Math.max(0, flashLight.intensity - dt * 60);
      boomLight.intensity = Math.max(0, boomLight.intensity - dt * 14);
    },
    reset() {
      for (const o of OFFICERS) dynamic.remove(o.rig.root); OFFICERS.length = 0;
      P.armor = 0; P.reload = 0;
    },
    damageCar, explode,
    debugCast() { const { o, d } = aimRay(); const startT = o.distanceTo(P.pos) - 0.2; const h = castShot(o, d, 200, Math.max(0.5, startT), P.car); return { kind: h.kind, t: h.t, o: o.toArray(), d: d.toArray(), startT }; },
  };
})();
