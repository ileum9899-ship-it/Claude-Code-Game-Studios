// ---------------------------------------------------------------- audio (WebAudio synth, starts on first click)
const AUDIO = (() => {
  let ctx = null, master, engine, engineGain, engineFilter, engine2, skidGain, sirenOsc, sirenGain, ambGain;
  function noiseBuffer() { const b = ctx.createBuffer(1, ctx.sampleRate * 2, ctx.sampleRate), d = b.getChannelData(0); for (let k = 0; k < d.length; k++) d[k] = Math.random() * 2 - 1; return b; }
  function start() {
    if (ctx) { if (ctx.state === "suspended") ctx.resume(); return; }
    try { ctx = new (window.AudioContext || window.webkitAudioContext)(); } catch (e) { return; }
    master = ctx.createGain(); master.gain.value = 0.55; master.connect(ctx.destination);
    engine = ctx.createOscillator(); engine.type = "sawtooth"; engine2 = ctx.createOscillator(); engine2.type = "square";
    engineFilter = ctx.createBiquadFilter(); engineFilter.type = "lowpass"; engineFilter.frequency.value = 700;
    engineGain = ctx.createGain(); engineGain.gain.value = 0;
    const e2g = ctx.createGain(); e2g.gain.value = 0.35;
    engine.connect(engineFilter); engine2.connect(e2g); e2g.connect(engineFilter); engineFilter.connect(engineGain); engineGain.connect(master);
    engine.start(); engine2.start();
    const nb = noiseBuffer();
    const skid = ctx.createBufferSource(); skid.buffer = nb; skid.loop = true;
    const bp = ctx.createBiquadFilter(); bp.type = "bandpass"; bp.frequency.value = 1800; bp.Q.value = 1.5;
    skidGain = ctx.createGain(); skidGain.gain.value = 0; skid.connect(bp); bp.connect(skidGain); skidGain.connect(master); skid.start();
    const amb = ctx.createBufferSource(); amb.buffer = nb; amb.loop = true;
    const lp = ctx.createBiquadFilter(); lp.type = "lowpass"; lp.frequency.value = 400;
    ambGain = ctx.createGain(); ambGain.gain.value = 0.05; amb.connect(lp); lp.connect(ambGain); ambGain.connect(master); amb.start();
    sirenOsc = ctx.createOscillator(); sirenOsc.type = "triangle"; sirenGain = ctx.createGain(); sirenGain.gain.value = 0;
    sirenOsc.connect(sirenGain); sirenGain.connect(master); sirenOsc.start();
  }
  function blip(freqs, dur = 0.12, type = "sine", vol = 0.25) {
    if (!ctx) return;
    freqs.forEach((f, k) => {
      const o = ctx.createOscillator(), g = ctx.createGain(); o.type = type; o.frequency.value = f;
      const t0 = ctx.currentTime + k * dur * 0.8; g.gain.setValueAtTime(0, t0); g.gain.linearRampToValueAtTime(vol, t0 + 0.01); g.gain.exponentialRampToValueAtTime(0.001, t0 + dur * 1.6);
      o.connect(g); g.connect(master); o.start(t0); o.stop(t0 + dur * 1.8);
    });
  }
  function burst(vol, freq = 300, dur = 0.35) {
    if (!ctx) return;
    const s = ctx.createBufferSource(); s.buffer = noiseBuffer();
    const f = ctx.createBiquadFilter(); f.type = "lowpass"; f.frequency.value = freq;
    const g = ctx.createGain(); g.gain.setValueAtTime(vol, ctx.currentTime); g.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + dur);
    s.connect(f); f.connect(g); g.connect(master); s.start(); s.stop(ctx.currentTime + dur);
  }
  return {
    start,
    update(car, sirenLevel, t) {
      if (!ctx) return;
      const now = ctx.currentTime;
      if (car) {
        const sp = Math.abs(car.vel.dot(forwardOf(car))), gear = Math.min(5, Math.floor(sp / 11)), within = (sp - gear * 11) / 11;
        const rpm = 900 + within * 4800 + (car.throttle !== 0 ? 400 : 0);
        engine.frequency.setTargetAtTime(rpm / 60 * 2, now, 0.05); engine2.frequency.setTargetAtTime(rpm / 60, now, 0.05);
        engineFilter.frequency.setTargetAtTime(500 + Math.abs(car.throttle) * 1600, now, 0.1);
        engineGain.gain.setTargetAtTime(0.08 + Math.abs(car.throttle) * 0.1, now, 0.1);
        skidGain.gain.setTargetAtTime(clamp((car.slip - 3.5) / 10, 0, 0.35) * (sp > 4 ? 1 : 0), now, 0.05);
      } else { engineGain.gain.setTargetAtTime(0, now, 0.15); skidGain.gain.setTargetAtTime(0, now, 0.05); }
      sirenGain.gain.setTargetAtTime(sirenLevel * 0.09, now, 0.2);
      sirenOsc.frequency.setTargetAtTime(Math.sin(t * 3.2) > 0 ? 1350 : 950, now, 0.08);
    },
    horn(pos, vol = 0.5) { if (!ctx) return; const d = pos ? Math.hypot(pos.x - CAMERA.cam.position.x, pos.z - CAMERA.cam.position.z) : 0; const v = vol * clamp(1 - d / 60, 0, 1); if (v > 0.01) blip([392, 494], 0.3, "square", v * 0.25); },
    thud(v) { burst(v * 0.8, 220, 0.3); },
    crash(v) { burst(v, 900, 0.5); burst(v * 0.8, 160, 0.6); },
    cash() { blip([880, 1320], 0.08, "triangle", 0.2); },
    win() { blip([523, 659, 784, 1046], 0.13, "triangle", 0.25); },
    fail() { blip([392, 311, 262], 0.2, "sawtooth", 0.15); },
    star() { blip([660, 440], 0.12, "square", 0.12); },
  };
})();

// ---------------------------------------------------------------- input
const INPUT = { keys: new Set(), mdx: 0, mdy: 0, locked: false, dragging: false, wheel: 0, touch: { x: 0, y: 0, active: false }, pressed: new Set() };
const isDown = c => INPUT.keys.has(c);
addEventListener("keydown", e => {
  if (["Space", "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Tab"].includes(e.code)) e.preventDefault();
  if (!INPUT.keys.has(e.code)) INPUT.pressed.add(e.code);
  INPUT.keys.add(e.code); AUDIO.start();
});
addEventListener("keyup", e => INPUT.keys.delete(e.code));
addEventListener("blur", () => INPUT.keys.clear());
const canvasEl = renderer.domElement;
canvasEl.addEventListener("contextmenu", e => e.preventDefault());
canvasEl.addEventListener("mousedown", e => {
  AUDIO.start(); INPUT.dragging = true;
  if (!INPUT.locked && e.button === 0 && canvasEl.requestPointerLock && !matchMedia("(hover: none)").matches) {
    try { const r = canvasEl.requestPointerLock(); if (r && r.catch) r.catch(() => {}); } catch (err) { /* sandbox may refuse */ }
  }
});
addEventListener("mouseup", () => INPUT.dragging = false);
document.addEventListener("pointerlockchange", () => INPUT.locked = document.pointerLockElement === canvasEl);
addEventListener("mousemove", e => { if (INPUT.locked || INPUT.dragging) { INPUT.mdx += e.movementX; INPUT.mdy += e.movementY; } });
canvasEl.addEventListener("wheel", e => { e.preventDefault(); INPUT.wheel += Math.sign(e.deltaY); }, { passive: false });
// touch: left half = joystick, right half = camera drag; buttons are DOM
const joy = document.getElementById("joy"), knob = document.getElementById("knob");
const touches = new Map();
canvasEl.addEventListener("touchstart", e => {
  AUDIO.start();
  for (const t of e.changedTouches) {
    if (t.clientX < innerWidth * 0.45 && !INPUT.touch.active) {
      INPUT.touch = { id: t.identifier, ox: t.clientX, oy: t.clientY, x: 0, y: 0, active: true };
      joy.style.left = t.clientX - 60 + "px"; joy.style.top = t.clientY - 60 + "px"; joy.hidden = false;
    } else touches.set(t.identifier, { x: t.clientX, y: t.clientY });
  }
  e.preventDefault();
}, { passive: false });
canvasEl.addEventListener("touchmove", e => {
  for (const t of e.changedTouches) {
    if (INPUT.touch.active && t.identifier === INPUT.touch.id) {
      let dx = t.clientX - INPUT.touch.ox, dy = t.clientY - INPUT.touch.oy; const l = Math.hypot(dx, dy);
      if (l > 50) { dx *= 50 / l; dy *= 50 / l; }
      INPUT.touch.x = dx / 50; INPUT.touch.y = dy / 50; knob.style.transform = `translate(${dx}px,${dy}px)`;
    } else if (touches.has(t.identifier)) {
      const p = touches.get(t.identifier); INPUT.mdx += (t.clientX - p.x) * 1.6; INPUT.mdy += (t.clientY - p.y) * 1.6; p.x = t.clientX; p.y = t.clientY;
    }
  }
  e.preventDefault();
}, { passive: false });
function endTouch(e) {
  for (const t of e.changedTouches) {
    if (INPUT.touch.active && t.identifier === INPUT.touch.id) { INPUT.touch.active = false; INPUT.touch.x = INPUT.touch.y = 0; joy.hidden = true; knob.style.transform = ""; }
    touches.delete(t.identifier);
  }
}
canvasEl.addEventListener("touchend", endTouch); canvasEl.addEventListener("touchcancel", endTouch);
document.querySelectorAll("[data-key]").forEach(b => {
  const code = b.dataset.key;
  const down = e => { e.preventDefault(); AUDIO.start(); if (!INPUT.keys.has(code)) INPUT.pressed.add(code); INPUT.keys.add(code); b.classList.add("held"); };
  const up = e => { e.preventDefault(); INPUT.keys.delete(code); b.classList.remove("held"); };
  b.addEventListener("pointerdown", down); b.addEventListener("pointerup", up); b.addEventListener("pointerleave", up); b.addEventListener("pointercancel", up);
});

// ---------------------------------------------------------------- player
const PLAYER = {
  rig: null, pos: new THREE.Vector3(), vel: new THREE.Vector3(), yaw: 0, car: null, onGround: true, alive: true,
  health: 100, money: 500, heat: 0, stars: 0, evade: 0, bust: 0, downTimer: 0,
  knock(vx, vz, sp) {
    if (this.downTimer > 0) return;
    this.vel.set(vx * 0.8, 4 + sp * 0.15, vz * 0.8); this.onGround = false; this.downTimer = 1.6;
    this.health -= sp * 3.2; AUDIO.thud(1); CAMERA.shake = 0.6;
  },
};
const SPAWN = { x: 3.5 * CITY.PITCH + 30, z: 4.5 * CITY.PITCH - 2 };
function initPlayer() {
  PLAYER.rig = makeHuman({ top: 0x2a2f36, bottom: 0x3a4d6b, skin: 0xe0ac85, hair: 0x1a1410 });
  // a short leather jacket look: collar + lighter tee
  dynamic.add(PLAYER.rig.root);
  PLAYER.pos.set(SPAWN.x, 0.18, SPAWN.z); PLAYER.yaw = Math.PI;
}
function nearestEnterable() {
  let best = null, bd = 4.2;
  for (const v of VEHICLES) { const d = Math.hypot(v.pos.x - PLAYER.pos.x, v.pos.z - PLAYER.pos.z); if (d < bd && speedOf(v) < 6) { bd = d; best = v; } }
  return best;
}
function enterCar(car) {
  if (car.ai && car.ai.mode === "drive") {
    // carjack: the driver flees on foot
    const blk = CITY.blockAt(car.pos.x + 8, car.pos.z) || CITY.blockAt(car.pos.x, car.pos.z + 8) || CITY.blocks[0];
    const p = spawnPed(blk); p.state = "flee"; p.timer = 6; p.threat = PLAYER.pos.clone();
    addHeat(CFG.wanted.carjack, "차량 탈취");
  } else if (car.type === "police") addHeat(2, "경찰차 탈취");
  car.ai = null; car.throttle = car.brake = car.steerIn = 0;
  if (!car.driverMesh) addDriver(car);
  // swap the seated driver for the player's look
  car.body.remove(car.driverMesh.root);
  const me = makeHuman({ top: 0x2a2f36, bottom: 0x3a4d6b, skin: 0xe0ac85, hair: 0x1a1410 }); poseSit(me); me.root.scale.setScalar(0.95); me.root.position.set(-0.38, -0.48, -0.15);
  car.body.add(me.root); car.driverMesh = me;
  PLAYER.car = car; PLAYER.rig.root.visible = false;
  HUD.toast(`${CAR_NAMES[car.type]} 탑승`);
}
function exitCar() {
  const car = PLAYER.car; if (speedOf(car) > 4) return;
  const f = forwardOf(car), side = new THREE.Vector2(-f.y, f.x);
  PLAYER.pos.set(car.pos.x - side.x * 2.0, 0.18, car.pos.z - side.y * 2.0);
  const p = { x: PLAYER.pos.x, z: PLAYER.pos.z }; collideCircle(p, 0.5); PLAYER.pos.x = p.x; PLAYER.pos.z = p.z;
  PLAYER.yaw = car.yaw; PLAYER.rig.root.visible = true;
  if (car.driverMesh) { car.body.remove(car.driverMesh.root); car.driverMesh = null; }
  car.throttle = 0; car.brake = 1; car.steerIn = 0; car.handbrake = true; car.ai = { mode: "parked" };
  PLAYER.car = null;
}
const CAR_NAMES = { sedan: "세단", sports: "스포츠카", suv: "SUV", taxi: "택시", van: "밴", police: "경찰차" };

function updatePlayer(dt) {
  if (INPUT.pressed.has("KeyF") || INPUT.pressed.has("Enter")) {
    if (PLAYER.car) exitCar(); else { const c = nearestEnterable(); if (c) enterCar(c); }
  }
  if (PLAYER.car) {
    const c = PLAYER.car, tj = INPUT.touch.active ? -INPUT.touch.y : 0;
    const up = isDown("KeyW") || isDown("ArrowUp") || tj > 0.3, dn = isDown("KeyS") || isDown("ArrowDown") || tj < -0.3;
    c.throttle = up ? 1 : dn ? -1 : 0;
    c.brake = 0;
    c.steerIn = ((isDown("KeyA") || isDown("ArrowLeft")) ? 1 : 0) - ((isDown("KeyD") || isDown("ArrowRight")) ? 1 : 0) - (INPUT.touch.active ? INPUT.touch.x : 0);
    c.steerIn = clamp(c.steerIn, -1, 1);
    c.handbrake = isDown("Space");
    if (INPUT.pressed.has("KeyH")) AUDIO.horn(null, 1);
    const impact = stepCar(c, dt);
    if (impact > 4) { CAMERA.shake = Math.min(1, impact / 16); AUDIO.crash(Math.min(1, impact / 22)); }
    PLAYER.pos.set(c.pos.x, 0.18, c.pos.z);
    if (c.health <= 0 && !c.burning) { c.burning = true; HUD.toast("차량이 크게 파손됐습니다! F로 내리세요"); }
    if (c.burning) { c.throttle *= 0.3; }
    return;
  }
  // on foot
  const h = PLAYER.rig;
  if (PLAYER.downTimer > 0) {
    PLAYER.downTimer -= dt;
    PLAYER.vel.y -= CFG.player.gravity * dt; PLAYER.pos.addScaledVector(PLAYER.vel, dt);
    const g = CITY.groundHeight(PLAYER.pos.x, PLAYER.pos.z);
    if (PLAYER.pos.y <= g) { PLAYER.pos.y = g; PLAYER.vel.multiplyScalar(0.6); PLAYER.vel.y = 0; }
    h.root.rotation.set(-Math.PI / 2 * clamp(PLAYER.downTimer * 2, 0, 1), PLAYER.yaw, 0, "YXZ");
    h.root.position.copy(PLAYER.pos); animateHuman(h, 0, dt, true);
    if (PLAYER.downTimer <= 0) h.root.rotation.set(0, PLAYER.yaw, 0);
    return;
  }
  let mx = ((isDown("KeyD") || isDown("ArrowRight")) ? 1 : 0) - ((isDown("KeyA") || isDown("ArrowLeft")) ? 1 : 0);
  let mz = ((isDown("KeyW") || isDown("ArrowUp")) ? 1 : 0) - ((isDown("KeyS") || isDown("ArrowDown")) ? 1 : 0);
  if (INPUT.touch.active) { mx = INPUT.touch.x; mz = -INPUT.touch.y; }
  const len = Math.hypot(mx, mz);
  const run = isDown("ShiftLeft") || isDown("ShiftRight") || (INPUT.touch.active && len > 0.9);
  const speed = len > 0.05 ? (run ? CFG.player.run : CFG.player.walk) * Math.min(1, len) : 0;
  // camera-relative movement: camera forward on the ground
  const cy = CAMERA.yaw, fwd = new THREE.Vector2(Math.sin(cy), Math.cos(cy)), right = new THREE.Vector2(-fwd.y, fwd.x);
  const dir = new THREE.Vector2(fwd.x * mz + right.x * mx, fwd.y * mz + right.y * mx);
  if (dir.lengthSq() > 1e-4) { dir.normalize(); PLAYER.yaw = PLAYER.yaw + angDiff(PLAYER.yaw, Math.atan2(dir.x, dir.y)) * (1 - Math.exp(-14 * dt)); }
  const tvx = dir.x * speed, tvz = dir.y * speed;
  PLAYER.vel.x = damp(PLAYER.vel.x, tvx, PLAYER.onGround ? 12 : 2, dt); PLAYER.vel.z = damp(PLAYER.vel.z, tvz, PLAYER.onGround ? 12 : 2, dt);
  if (PLAYER.onGround && (INPUT.pressed.has("Space"))) { PLAYER.vel.y = CFG.player.jump; PLAYER.onGround = false; }
  PLAYER.vel.y -= CFG.player.gravity * dt;
  PLAYER.pos.addScaledVector(PLAYER.vel, dt);
  const p = { x: PLAYER.pos.x, z: PLAYER.pos.z }; collideCircle(p, CFG.player.radius); PLAYER.pos.x = p.x; PLAYER.pos.z = p.z;
  for (const v of VEHICLES) { // cars are solid
    const f = forwardOf(v), dx = PLAYER.pos.x - v.pos.x, dz = PLAYER.pos.z - v.pos.z;
    const along = dx * f.x + dz * f.y, lat = -dx * f.y + dz * f.x, hl = v.spec.len / 2 + 0.35, hw = v.spec.wid / 2 + 0.35;
    if (Math.abs(along) < hl && Math.abs(lat) < hw && PLAYER.pos.y < 1.3) {
      const pa = hl - Math.abs(along), pl = hw - Math.abs(lat);
      if (pa < pl) { PLAYER.pos.x += f.x * pa * Math.sign(along); PLAYER.pos.z += f.y * pa * Math.sign(along); }
      else { PLAYER.pos.x += -f.y * pl * Math.sign(lat); PLAYER.pos.z += f.x * pl * Math.sign(lat); }
    }
  }
  const g = CITY.groundHeight(PLAYER.pos.x, PLAYER.pos.z);
  if (g < -50) { PLAYER.health = 0; } // fell into the harbour
  const step = PLAYER.pos.y - g;
  if (step <= 0.25 && PLAYER.vel.y <= 0) { PLAYER.pos.y = damp(PLAYER.pos.y, g, 30, dt); PLAYER.vel.y = 0; PLAYER.onGround = true; } else if (step > 0.3) PLAYER.onGround = false;
  h.root.position.copy(PLAYER.pos); h.root.rotation.set(0, PLAYER.yaw, 0);
  animateHuman(h, Math.hypot(PLAYER.vel.x, PLAYER.vel.z), dt, !PLAYER.onGround);
}

// ---------------------------------------------------------------- wanted level
function addHeat(n, why) {
  const before = PLAYER.stars;
  PLAYER.heat = Math.min(CFG.wanted.maxStars + 0.99, PLAYER.heat + n);
  PLAYER.stars = Math.min(CFG.wanted.maxStars, Math.floor(PLAYER.heat));
  PLAYER.evade = 0;
  if (PLAYER.stars > before) { AUDIO.star(); HUD.toast(`수배 레벨 ${"★".repeat(PLAYER.stars)} · ${why}`, "warn"); }
}
function updateWanted(dt) {
  const cops = VEHICLES.filter(v => v.type === "police" && v.ai && v.ai.mode === "police");
  const want = Math.min(CFG.wanted.maxCops, PLAYER.stars * CFG.wanted.copsPerStar);
  if (cops.length < want && Math.random() < dt * 0.8) spawnPolice();
  let nearest = 1e9;
  for (const c of cops) nearest = Math.min(nearest, Math.hypot(c.pos.x - PLAYER.pos.x, c.pos.z - PLAYER.pos.z));
  if (PLAYER.stars > 0) {
    if (nearest > CFG.wanted.evadeRadius) {
      PLAYER.evade += dt;
      if (PLAYER.evade > CFG.wanted.evadeSeconds) { PLAYER.heat = 0; PLAYER.stars = 0; PLAYER.evade = 0; HUD.toast("경찰을 따돌렸습니다", "good"); AUDIO.win(); MISSIONS.onEvaded(); }
    } else PLAYER.evade = 0;
    const playerSpeed = PLAYER.car ? speedOf(PLAYER.car) : 0;
    if (nearest < CFG.wanted.bustRadius && playerSpeed < 2) { PLAYER.bust += dt; if (PLAYER.bust > CFG.wanted.bustSeconds) GAME.busted(); }
    else PLAYER.bust = Math.max(0, PLAYER.bust - dt);
  }
  // police with no wanted level drive off and despawn when far
  for (const c of cops) if (PLAYER.stars === 0) { c.ai.mode = "parked"; c.throttle = 0; c.brake = 1; }
  return nearest;
}

// ---------------------------------------------------------------- missions + pickups
const MARKERS = new THREE.Group(); scene.add(MARKERS);
function beam(color) {
  const g = new THREE.Group();
  const cyl = new THREE.Mesh(new THREE.CylinderGeometry(1.6, 1.6, 40, 32, 1, true), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.22, depthWrite: false, side: THREE.DoubleSide, blending: THREE.AdditiveBlending }));
  cyl.position.y = 20; g.add(cyl);
  const ring = new THREE.Mesh(new THREE.TorusGeometry(1.8, 0.08, 8, 48), new THREE.MeshBasicMaterial({ color: color.clone().multiplyScalar(4) }));
  ring.rotation.x = Math.PI / 2; ring.position.y = 0.3; g.add(ring);
  g.userData.ring = ring; MARKERS.add(g); return g;
}
const MISSIONS = (() => {
  const JOBS = [
    { name: "택배 배달", kind: "courier", x: 3 * CITY.PITCH + 14, z: 4 * CITY.PITCH + 9.5 },
    { name: "스트리트 레이스", kind: "race", x: 5 * CITY.PITCH - 9.5, z: 2 * CITY.PITCH + 20 },
    { name: "추격 따돌리기", kind: "evade", x: 2 * CITY.PITCH + 30, z: 6 * CITY.PITCH - 9.5 },
  ];
  const jobBeams = JOBS.map(j => { const b = beam(new THREE.Color(1, 0.75, 0.15)); b.position.set(j.x, 0.18, j.z); return b; });
  let active = null, target = null, cooldown = 0;
  function sidewalkPoint(minD, maxD) {
    for (let a = 0; a < 60; a++) {
      const blk = pick(CITY.blocks); const q = pedPathPoint(blk, RNG()); const d = Math.hypot(q[0] - PLAYER.pos.x, q[1] - PLAYER.pos.z);
      if (d > minD && d < maxD) return new THREE.Vector3(q[0], 0.18, q[1]);
    }
    return new THREE.Vector3(SPAWN.x, 0.18, SPAWN.z);
  }
  function setTarget(p, color) { if (target) MARKERS.remove(target); target = beam(color); target.position.copy(p); }
  function start(job) {
    if (job.kind === "race" && !PLAYER.car) { HUD.toast("레이스는 차량이 필요합니다", "warn"); cooldown = 3; return; }
    AUDIO.win();
    if (job.kind === "courier") {
      const p = sidewalkPoint(160, 330), d = p.distanceTo(PLAYER.pos);
      active = { job, points: [p], idx: 0, time: CFG.missions.timeBase + d * CFG.missions.timePerMeter * 1.9, reward: Math.round(lerp(CFG.missions.courierReward[0], CFG.missions.courierReward[1], clamp((d - 160) / 170, 0, 1)) / 10) * 10 };
    } else if (job.kind === "race") {
      const pts = []; let cur = CITY.nearestNode(PLAYER.pos.x, PLAYER.pos.z), prev = cur;
      for (let k = 0; k < 7; k++) { let nx = cur; for (let s = 0; s < 2; s++) { const n = chooseNext(prev, nx); prev = nx; nx = n; } cur = nx; const np = nodePos(cur); pts.push(new THREE.Vector3(np.x, 0, np.y)); }
      let total = 0, last = PLAYER.pos; pts.forEach(p => { total += p.distanceTo(last); last = p; });
      active = { job, points: pts, idx: 0, time: 12 + total * 0.062, reward: CFG.missions.raceReward };
    } else {
      active = { job, points: [], idx: 0, time: 120, reward: 1200, needStars: 2 };
      HUD.toast("수배 ★★ 를 얻은 뒤 경찰을 따돌리세요", "warn");
    }
    if (active.points.length) setTarget(active.points[0], new THREE.Color(0.2, 0.8, 1));
    HUD.mission(active);
  }
  function finish(ok) {
    if (ok) { PLAYER.money += active.reward; AUDIO.win(); HUD.big("미션 성공", `+$${active.reward.toLocaleString()}`, "good"); }
    else { AUDIO.fail(); HUD.big("미션 실패", active.job.name, "bad"); }
    if (target) { MARKERS.remove(target); target = null; }
    active = null; cooldown = 4; HUD.mission(null);
  }
  function update(dt, t) {
    jobBeams.forEach((b, k) => { b.visible = !active; b.userData.ring.position.y = 0.3 + Math.sin(t * 2 + k) * 0.15; });
    if (target) target.userData.ring.position.y = 0.3 + Math.sin(t * 3) * 0.2;
    cooldown -= dt;
    if (!active) {
      if (cooldown <= 0) JOBS.forEach(j => { if (Math.hypot(j.x - PLAYER.pos.x, j.z - PLAYER.pos.z) < 3) start(j); });
      return;
    }
    active.time -= dt;
    if (active.time <= 0) return finish(false);
    if (active.job.kind === "evade") {
      if (PLAYER.stars >= active.needStars) active.armed = true;
      HUD.mission(active); return;
    }
    const p = active.points[active.idx];
    if (Math.hypot(p.x - PLAYER.pos.x, p.z - PLAYER.pos.z) < (active.job.kind === "race" ? 7 : 3.2)) {
      active.idx++; AUDIO.cash();
      if (active.idx >= active.points.length) return finish(true);
      setTarget(active.points[active.idx], new THREE.Color(0.2, 0.8, 1));
    }
    HUD.mission(active);
  }
  return {
    update, get active() { return active; }, get target() { return target; }, jobs: JOBS,
    onEvaded() { if (active && active.job.kind === "evade" && active.armed) finish(true); },
    cancel() { if (active) finish(false); },
  };
})();
const PICKUPS = (() => {
  const geo = new THREE.BoxGeometry(0.5, 0.26, 0.26), mat = new THREE.MeshBasicMaterial({ color: new THREE.Color(0.4, 3, 0.8) });
  const list = [];
  function place(m) { for (let a = 0; a < 30; a++) { const blk = pick(CITY.blocks); const q = pedPathPoint(blk, RNG()); m.position.set(q[0], 0.9, q[1]); if (blk.type !== "harbor") return; } }
  for (let k = 0; k < CFG.cash.pickups; k++) { const m = new THREE.Mesh(geo, mat); place(m); MARKERS.add(m); list.push(m); }
  return {
    update(dt, t) {
      for (const m of list) {
        m.rotation.y += dt * 2; m.position.y = 0.9 + Math.sin(t * 3 + m.id) * 0.12;
        if (Math.hypot(m.position.x - PLAYER.pos.x, m.position.z - PLAYER.pos.z) < 1.6) {
          const v = randi(CFG.cash.value[0], CFG.cash.value[1]); PLAYER.money += v; AUDIO.cash(); HUD.toast(`+$${v}`, "good"); place(m);
        }
      }
    },
    list,
  };
})();

// ---------------------------------------------------------------- camera
const CAMERA = { cam: camera, yaw: Math.PI, pitch: 0.28, dist: 1, idle: 0, shake: 0, fov: 62, pos: new THREE.Vector3(), look: new THREE.Vector3() };
function updateCamera(dt) {
  const inCar = !!PLAYER.car;
  const sens = INPUT.locked ? 0.0022 : 0.005;
  if (Math.abs(INPUT.mdx) + Math.abs(INPUT.mdy) > 0) { CAMERA.idle = 0; }
  CAMERA.yaw -= INPUT.mdx * sens; CAMERA.pitch = clamp(CAMERA.pitch + INPUT.mdy * sens, -0.25, 1.2);
  INPUT.mdx = INPUT.mdy = 0;
  CAMERA.idle += dt;
  let target, baseDist, height;
  if (inCar) {
    const c = PLAYER.car, sp = speedOf(c);
    if (CAMERA.idle > 1.2 && sp > 2) { // swing behind the car (or toward travel when reversing)
      const vF = c.vel.dot(forwardOf(c)), behind = vF < -1 ? c.yaw + Math.PI : c.yaw;
      CAMERA.yaw += angDiff(CAMERA.yaw, behind) * (1 - Math.exp(-3 * dt));
      CAMERA.pitch = damp(CAMERA.pitch, 0.2, 2, dt);
    }
    baseDist = 7.8 + c.spec.len * 0.3 + sp * 0.04; height = 1.6;
    target = new THREE.Vector3(c.pos.x, c.pos.y + 1.4, c.pos.z);
    CAMERA.fov = damp(CAMERA.fov, 62 + clamp(sp / 45, 0, 1) * 16, 3, dt);
  } else {
    baseDist = 4.4; height = 1.55;
    target = new THREE.Vector3(PLAYER.pos.x, PLAYER.pos.y + height, PLAYER.pos.z);
    CAMERA.fov = damp(CAMERA.fov, 60, 3, dt);
  }
  CAMERA.dist = clamp(CAMERA.dist + INPUT.wheel * 0.6, 0.6, 1.8); INPUT.wheel = 0;
  const dist = baseDist * CAMERA.dist / 1.0;
  const cp = Math.cos(CAMERA.pitch), sp2 = Math.sin(CAMERA.pitch);
  const want = new THREE.Vector3(target.x - Math.sin(CAMERA.yaw) * cp * dist, target.y + sp2 * dist + (inCar ? 0.6 : 0.25), target.z - Math.cos(CAMERA.yaw) * cp * dist);
  if (!inCar) { const sideX = Math.cos(CAMERA.yaw) * -0.55, sideZ = -Math.sin(CAMERA.yaw) * -0.55; want.x += sideX; want.z += sideZ; target.x += sideX; target.z += sideZ; }
  const tHit = raycastBoxes(target.x, target.y, target.z, want.x, want.y, want.z);
  if (tHit < 1) want.lerpVectors(target, want, Math.max(0.12, tHit - 0.05));
  want.y = Math.max(want.y, CITY.groundHeight(want.x, want.z) + 0.4);
  CAMERA.pos.lerp(want, 1 - Math.exp(-(inCar ? 14 : 20) * dt));
  if (CAMERA.pos.distanceTo(want) > 30) CAMERA.pos.copy(want);
  camera.position.copy(CAMERA.pos);
  if (CAMERA.shake > 0) { camera.position.x += rand(-1, 1) * CAMERA.shake * 0.3; camera.position.y += rand(-1, 1) * CAMERA.shake * 0.3; CAMERA.shake = Math.max(0, CAMERA.shake - dt * 2.5); }
  camera.lookAt(target);
  if (Math.abs(camera.fov - CAMERA.fov) > 0.01) { camera.fov = CAMERA.fov; camera.updateProjectionMatrix(); }
}
