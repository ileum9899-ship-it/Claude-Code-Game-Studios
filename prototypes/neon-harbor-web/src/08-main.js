// ---------------------------------------------------------------- post-processing
const composerTarget = new THREE.WebGLRenderTarget(1, 1, { type: THREE.HalfFloatType, format: THREE.RGBAFormat, minFilter: THREE.LinearFilter, magFilter: THREE.LinearFilter });
const composer = new THREE.EffectComposer(renderer, composerTarget);
composer.addPass(new THREE.RenderPass(scene, camera));
const bloom = new THREE.UnrealBloomPass(new THREE.Vector2(256, 256), 0.55, 0.55, 0.92);
composer.addPass(bloom);
const grade = new THREE.ShaderPass({
  uniforms: { tDiffuse: { value: null }, exposure: { value: 0.6 }, saturation: { value: 1.12 }, vignette: { value: 0.32 }, time: { value: 0 }, tint: { value: new THREE.Color(1, 1, 1) }, flash: { value: new THREE.Color(0, 0, 0) } },
  vertexShader: "varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }",
  fragmentShader: `uniform sampler2D tDiffuse; uniform float exposure, saturation, vignette, time; uniform vec3 tint, flash; varying vec2 vUv;
    vec3 aces(vec3 x){ return clamp((x*(2.51*x+0.03))/(x*(2.43*x+0.59)+0.14), 0.0, 1.0); }
    float hash(vec2 p){ return fract(sin(dot(p, vec2(12.9898,78.233)) + time) * 43758.5453); }
    void main(){
      vec3 c = texture2D(tDiffuse, vUv).rgb * exposure * tint;
      c = aces(c);
      float l = dot(c, vec3(0.2126,0.7152,0.0722)); c = mix(vec3(l), c, saturation);
      c = pow(c, vec3(1.0/2.2));
      vec2 q = vUv - 0.5; c *= 1.0 - vignette * dot(q, q) * 2.2;
      c += (hash(vUv * 800.0) - 0.5) * 0.025;
      c = mix(c, flash, clamp(length(flash), 0.0, 0.6));
      gl_FragColor = vec4(c, 1.0);
    }`,
});
composer.addPass(grade);
const fxaa = new THREE.ShaderPass(THREE.FXAAShader);
composer.addPass(fxaa);

// ---------------------------------------------------------------- time of day
const DAY = { t: 0.728, lastEnv: -1 };
const playerLight = new THREE.SpotLight(0xfff1d6, 0, 70, 0.55, 0.5, 1.2); playerLight.castShadow = false; scene.add(playerLight, playerLight.target);
const C_DAYFOG = col(0xa9bdd0), C_DUSKFOG = col(0xd59a72), C_NIGHTFOG = col(0x0b1222);
function updateDay(dt) {
  DAY.t = (DAY.t + dt / CFG.dayLength) % 1;
  const th = (DAY.t - 0.25) * TAU, dir = new THREE.Vector3(Math.cos(th), Math.sin(th), 0.42).normalize();
  const elev = dir.y, day = clamp(elev * 5 + 0.15, 0, 1), night = clamp((0.06 - elev) / 0.18, 0, 1), dusk = clamp(1 - Math.abs(elev - 0.06) / 0.16, 0, 1);
  sky.material.uniforms.sunPosition.value.copy(dir);
  const focus = PLAYER.pos;
  const lightDir = elev > -0.05 ? dir : new THREE.Vector3(-dir.x, -dir.y, -dir.z * 0.5).normalize();
  sun.position.set(focus.x + lightDir.x * 250, focus.y + lightDir.y * 250, focus.z + lightDir.z * 250);
  sun.target.position.set(focus.x, 0, focus.z); sun.target.updateMatrixWorld();
  sun.intensity = elev > -0.05 ? lerp(0.3, 4.0, day) : 0.45;
  sun.color.copy(col(0xffffff)).lerp(col(0xff9a5c), dusk * 0.85); if (elev <= -0.05) sun.color.copy(col(0x8fa8ff));
  hemi.intensity = lerp(0.14, 0.5, day); hemi.color.copy(col(0xcfe3ff)).lerp(col(0x334466), night);
  const fogC = C_DAYFOG.clone().lerp(C_DUSKFOG, dusk * 0.7).lerp(C_NIGHTFOG, night);
  scene.fog.color.copy(fogC); scene.fog.density = lerp(0.0017, 0.0032, night);
  stars.material.opacity = night * 0.9;
  for (const k of Object.keys(TEX.facade)) M["f_" + k].emissiveIntensity = lerp(0.0, 1.25, clamp(night * 1.2 + dusk * 0.2, 0, 1));
  M.lamp.color.setRGB(1, 0.82, 0.55).multiplyScalar(lerp(0.8, 7, night));
  NEON.forEach(m => m.color.setScalar(lerp(0.9, 3.4, night)));
  POOL_MAT.opacity = night * 0.55; BEAM_MAT.opacity = night * 0.32;
  MAT_HEAD.color.setRGB(2.2, 2.1, 1.8).multiplyScalar(lerp(1, 3, night));
  grade.uniforms.exposure.value = lerp(0.56, 0.72, night);
  bloom.strength = lerp(0.35, 0.95, night);
  bloom.threshold = lerp(0.95, 0.82, night);
  if (M.water) { M.water.uniforms.sunDir.value.copy(dir); M.water.uniforms.skyCol.value.copy(fogC).multiplyScalar(1.1); M.water.uniforms.night.value = night; M.water.uniforms.fogColor.value.copy(fogC); M.water.uniforms.fogDensity.value = scene.fog.density; M.water.uniforms.sunCol.value.copy(sun.color); }
  // headlight for the player's car after dusk
  if (PLAYER.car && night > 0.2) {
    const c = PLAYER.car, f = forwardOf(c);
    playerLight.intensity = 3 * night; playerLight.position.set(c.pos.x + f.x * 2.2, 0.9, c.pos.z + f.y * 2.2);
    playerLight.target.position.set(c.pos.x + f.x * 22, 0, c.pos.z + f.y * 22); playerLight.target.updateMatrixWorld();
  } else playerLight.intensity = 0;
  if (Math.abs(DAY.t - DAY.lastEnv) > 0.006) {
    DAY.lastEnv = DAY.t;
    for (const k of ["turbidity", "rayleigh", "mieCoefficient", "mieDirectionalG"]) envSky.material.uniforms[k].value = sky.material.uniforms[k].value;
    envSky.material.uniforms.sunPosition.value.copy(dir);
    const old = envRT; envRT = pmrem.fromScene(envScene); scene.environment = envRT.texture; if (old) old.dispose();
  }
  return { night, elev };
}

// ---------------------------------------------------------------- HUD
const $ = id => document.getElementById(id);
const HUD = (() => {
  const mm = $("minimap"), g = mm.getContext("2d"), S = 1.15, MW = Math.ceil((CITY.SIZE + 120) * S);
  const base = document.createElement("canvas"); base.width = base.height = MW;
  const b = base.getContext("2d"), O = 50; // world -> map: (x+O)*S
  b.fillStyle = "#1c3a52"; b.fillRect(0, 0, MW, MW);
  b.fillStyle = "#3b3f46"; b.fillRect((-CITY.HALF_R + O) * S, (-CITY.HALF_R + O) * S, (CITY.SIZE + CITY.R + 28) * S, (CITY.SIZE + CITY.R + 28) * S);
  for (const blk of CITY.blocks) {
    b.fillStyle = blk.type === "park" ? "#3f6b3a" : blk.type === "harbor" ? "#5a5550" : "#6a6e75";
    b.fillRect((blk.x0 + O) * S, (blk.z0 + O) * S, (blk.x1 - blk.x0) * S, (blk.z1 - blk.z0) * S);
    b.fillStyle = blk.type === "downtown" ? "#8c929b" : "#7b8088";
    for (const bl of blk.buildings) b.fillRect((bl.x0 + O) * S, (bl.z0 + O) * S, (bl.x1 - bl.x0) * S, (bl.z1 - bl.z0) * S);
  }
  const toasts = $("toasts");
  let bigTimer = 0;
  function blip(x, z, color, r, px, pz, rot, R, edge = false) {
    let dx = (x - px) * S, dz = (z - pz) * S;
    const c = Math.cos(rot), s = Math.sin(rot); let sx = dx * c - dz * s, sy = dx * s + dz * c;
    const d = Math.hypot(sx, sy);
    if (d > R - 6) { if (!edge) return; sx *= (R - 6) / d; sy *= (R - 6) / d; }
    g.fillStyle = color; g.beginPath(); g.arc(R + sx, R + sy, r, 0, TAU); g.fill();
    g.strokeStyle = "rgba(0,0,0,0.6)"; g.lineWidth = 1.5; g.stroke();
  }
  return {
    toast(msg, kind = "") {
      const d = document.createElement("div"); d.className = "toast " + kind; d.textContent = msg; toasts.appendChild(d);
      while (toasts.children.length > 3) toasts.firstChild.remove(); setTimeout(() => d.remove(), 3200);
    },
    big(title, sub, kind) { $("bigT").textContent = title; $("bigS").textContent = sub || ""; $("big").className = "big show " + (kind || ""); bigTimer = 3; },
    mission(a) {
      const el = $("mission");
      if (!a) { el.hidden = true; return; }
      el.hidden = false;
      $("mName").textContent = a.job.name;
      let goal;
      if (a.job.kind === "courier") goal = "표시된 지점으로 소포를 배달하세요";
      else if (a.job.kind === "race") goal = `체크포인트 ${a.idx + 1} / ${a.points.length}`;
      else goal = a.armed ? "경찰을 따돌리세요 (지도의 파란 점에서 멀어지기)" : `수배 ★★ 이상 얻기 (현재 ${"★".repeat(PLAYER.stars) || "없음"})`;
      $("mGoal").textContent = goal;
      const t = Math.max(0, a.time); $("mTime").textContent = `${Math.floor(t / 60)}:${String(Math.floor(t % 60)).padStart(2, "0")}`;
      $("mTime").classList.toggle("low", t < 10);
      $("mReward").textContent = `보상 $${a.reward.toLocaleString()}`;
    },
    update(dt, t, nearestCop) {
      // stats
      $("money").textContent = "$" + Math.floor(PLAYER.money).toLocaleString();
      const hours = Math.floor(DAY.t * 24), mins = Math.floor((DAY.t * 24 * 60) % 60);
      $("clock").textContent = `${String(hours).padStart(2, "0")}:${String(mins).padStart(2, "0")}`;
      const starEls = $("stars").children, flash = PLAYER.evade > 0 && Math.floor(t * 3) % 2 === 0;
      for (let k = 0; k < 5; k++) starEls[k].className = k < PLAYER.stars ? (flash ? "on dim" : "on") : "";
      $("hp").style.width = clamp(PLAYER.health, 0, 100) + "%";
      const car = PLAYER.car;
      $("speedo").hidden = !car;
      if (car) {
        const kmh = Math.round(Math.abs(car.vel.dot(forwardOf(car))) * 3.6);
        $("kmh").textContent = kmh;
        $("gear").textContent = car.vel.dot(forwardOf(car)) < -0.5 ? "R" : kmh < 2 ? "N" : String(1 + Math.min(5, Math.floor(kmh / 40)));
        $("dmg").style.width = clamp(car.health, 0, 100) + "%";
        $("carname").textContent = CAR_NAMES[car.type];
      }
      // context prompt
      let prompt = "";
      if (!car && PLAYER.alive) { const c = nearestEnterable(); if (c) prompt = `<kbd>F</kbd> ${CAR_NAMES[c.type]} ${c.ai && c.ai.mode === "drive" ? "탈취" : "탑승"}`; }
      else if (car && speedOf(car) < 4) prompt = `<kbd>F</kbd> 내리기`;
      if (!MISSIONS.active) for (const j of MISSIONS.jobs) if (Math.hypot(j.x - PLAYER.pos.x, j.z - PLAYER.pos.z) < 12) prompt = `노란 원에 들어가면 <b>${j.name}</b> 시작`;
      $("prompt").innerHTML = prompt; $("prompt").hidden = !prompt;
      if (bigTimer > 0) { bigTimer -= dt; if (bigTimer <= 0) $("big").className = "big"; }
      // minimap (rotates with the camera)
      const R = mm.width / 2, rot = CAMERA.yaw - Math.PI; // rotates world so the camera's forward points up
      g.clearRect(0, 0, mm.width, mm.height);
      g.save(); g.beginPath(); g.arc(R, R, R - 2, 0, TAU); g.clip();
      g.translate(R, R); g.rotate(rot);
      g.drawImage(base, -(PLAYER.pos.x + O) * S, -(PLAYER.pos.z + O) * S);
      g.restore();
      for (const j of MISSIONS.jobs) if (!MISSIONS.active) blip(j.x, j.z, "#ffc23a", 5, PLAYER.pos.x, PLAYER.pos.z, rot, R, true);
      if (MISSIONS.target) blip(MISSIONS.target.position.x, MISSIONS.target.position.z, "#3cd4ff", 6, PLAYER.pos.x, PLAYER.pos.z, rot, R, true);
      for (const v of VEHICLES) if (v.type === "police" && v.ai && v.ai.mode === "police") blip(v.pos.x, v.pos.z, Math.floor(t * 4) % 2 ? "#ff3b3b" : "#3b6bff", 4.5, PLAYER.pos.x, PLAYER.pos.z, rot, R, true);
      for (const p of PICKUPS.list) blip(p.position.x, p.position.z, "#5cff8a", 2.2, PLAYER.pos.x, PLAYER.pos.z, rot, R);
      // player arrow
      const heading = (PLAYER.car ? PLAYER.car.yaw : PLAYER.yaw);
      g.save(); g.translate(R, R); g.rotate(rot - heading + Math.PI);
      g.fillStyle = "#fff"; g.strokeStyle = "#000"; g.lineWidth = 1.5; g.beginPath(); g.moveTo(0, -9); g.lineTo(6.5, 7); g.lineTo(0, 3.5); g.lineTo(-6.5, 7); g.closePath(); g.fill(); g.stroke();
      g.restore();
      g.strokeStyle = "rgba(255,255,255,0.85)"; g.lineWidth = 3; g.beginPath(); g.arc(R, R, R - 2, 0, TAU); g.stroke();
      // north marker
      g.fillStyle = "#fff"; g.font = "bold 12px sans-serif"; g.textAlign = "center"; g.textBaseline = "middle"; g.fillText("N", R + Math.sin(rot) * (R - 11), R - Math.cos(rot) * (R - 11));
      void nearestCop;
    },
  };
})();

// ---------------------------------------------------------------- game flow
const GAME = (() => {
  let state = "play", timer = 0;
  function reset(where, title, sub, kind) {
    state = "dead"; timer = 3.2; HUD.big(title, sub, kind);
    MISSIONS.cancel();
  }
  return {
    get state() { return state; },
    busted() { if (state !== "play") return; const fine = Math.round(PLAYER.money * CFG.wanted.bustFine); PLAYER.money -= fine; reset("station", "체포됨", `벌금 -$${fine.toLocaleString()}`, "bad"); AUDIO.fail(); },
    wasted() { if (state !== "play") return; PLAYER.money = Math.max(0, PLAYER.money - 200); reset("hospital", "의식을 잃었습니다", "병원비 -$200", "bad"); AUDIO.fail(); },
    update(dt) {
      if (state === "play") { if (PLAYER.health <= 0) this.wasted(); return; }
      timer -= dt; grade.uniforms.flash.value.setRGB(0.25, 0.02, 0.02).multiplyScalar(clamp(timer / 3, 0, 1));
      if (timer <= 0) {
        if (PLAYER.car) { const c = PLAYER.car; if (c.driverMesh) { c.body.remove(c.driverMesh.root); c.driverMesh = null; } c.ai = { mode: "parked" }; PLAYER.car = null; }
        PLAYER.rig.root.visible = true; PLAYER.rig.root.rotation.set(0, 0, 0); PLAYER.downTimer = 0;
        PLAYER.pos.set(SPAWN.x, 0.18, SPAWN.z); PLAYER.vel.set(0, 0, 0); PLAYER.health = 100; PLAYER.heat = 0; PLAYER.stars = 0; PLAYER.bust = 0;
        for (const v of [...VEHICLES]) if (v.type === "police") removeCar(v);
        grade.uniforms.flash.value.setRGB(0, 0, 0); state = "play"; CAMERA.pos.set(SPAWN.x, 5, SPAWN.z + 8);
      }
    },
  };
})();

function manageSpawns(dt) {
  const P = PLAYER.pos;
  // traffic
  const traffic = VEHICLES.filter(v => v.ai && (v.ai.mode === "drive" || v.ai.mode === "wreck"));
  if (traffic.length < CFG.traffic.count) spawnTrafficCar(P);
  for (const v of [...VEHICLES]) {
    if (v === PLAYER.car) continue;
    const d = Math.hypot(v.pos.x - P.x, v.pos.z - P.z);
    if (d > 260 && v.ai && (v.ai.mode === "drive" || v.ai.mode === "wreck" || (v.type === "police" && PLAYER.stars === 0))) removeCar(v);
  }
  // pedestrians around the player
  for (const p of [...PEDS]) if (p.state === "gone" || p.pos.distanceTo(P) > CFG.peds.radius * 1.6) removePed(p);
  let guard = 0;
  while (PEDS.length < CFG.peds.count && guard++ < 8) {
    const blk = CITY.blockAt(P.x + rand(-CFG.peds.radius, CFG.peds.radius), P.z + rand(-CFG.peds.radius, CFG.peds.radius));
    if (!blk || blk.type === "harbor") continue;
    const p = spawnPed(blk); if (p.pos.distanceTo(P) < 18 && PEDS.length > 20) removePed(p);
  }
}
function spawnParked() {
  let n = 0;
  for (let a = 0; a < 400 && n < CFG.traffic.parked; a++) {
    const A = [randi(0, CITY.N), randi(0, CITY.N)], B = pick(CITY.neighbours(A[0], A[1])), pc = makeSegPiece(A, B);
    const s = rand(14, pc.len - 14), p = bez(pc.p0, pc.p1, pc.p2, s / pc.len), r = laneRight(pc.f);
    const x = p.x + r.x * 1.8, z = p.y + r.y * 1.8;
    if (VEHICLES.some(v => Math.hypot(v.pos.x - x, v.pos.z - z) < 7)) continue;
    const car = makeCar(pick(["sedan", "sedan", "suv", "sports", "van", "taxi"]));
    car.pos.set(x, 0, z); car.yaw = Math.atan2(pc.f.x, pc.f.y); car.ai = { mode: "parked" }; syncCar(car); VEHICLES.push(car); n++;
  }
  // a sports car waiting next to the spawn point
  const hero = makeCar("sports", 0xd61f1f); hero.pos.set(SPAWN.x - 25, 0, 4 * CITY.PITCH + 5.0); hero.yaw = -Math.PI / 2; hero.ai = { mode: "parked" }; syncCar(hero); VEHICLES.push(hero);
}

// ---------------------------------------------------------------- boot + loop
function resize() {
  const w = innerWidth, h = innerHeight; renderer.setSize(w, h); composer.setSize(w, h);
  camera.aspect = w / h; camera.updateProjectionMatrix();
  const pr = renderer.getPixelRatio(); fxaa.uniforms.resolution.value.set(1 / (w * pr), 1 / (h * pr));
  bloom.setSize(w / 2, h / 2);
}
addEventListener("resize", resize);
let clockT = 0, lastFrame = performance.now(), started = false;
function frame(now) {
  const dt = Math.min(0.05, (now - lastFrame) / 1000); lastFrame = now; clockT += dt;
  if (GAME.state === "play") updatePlayer(dt);
  GAME.update(dt);
  for (const v of VEHICLES) {
    if (v === PLAYER.car) continue;
    if (!v.ai) continue;
    if (v.ai.mode === "drive" || v.ai.mode === "wreck") updateTraffic(v, dt, clockT);
    else if (v.ai.mode === "police") updatePolice(v, dt, clockT);
    else if (v.ai.mode === "parked" && (speedOf(v) > 0.05 || Math.abs(v.yawRate) > 0.01)) { v.throttle = 0; v.brake = 0.6; v.steerIn = 0; stepCar(v, dt); }
  }
  resolveContacts(dt);
  for (const p of PEDS) updatePed(p, dt);
  const nearestCop = updateWanted(dt);
  MISSIONS.update(dt, clockT); PICKUPS.update(dt, clockT);
  LIGHTS.update(clockT);
  manageSpawns(dt);
  const env = updateDay(dt);
  if (CAMERA.override) { camera.position.copy(CAMERA.override.pos); camera.lookAt(CAMERA.override.look); } else updateCamera(dt);
  if (M.water) M.water.uniforms.time.value = clockT;
  M.redBlink.color.setRGB(Math.sin(clockT * 3) > 0.6 ? 6 : 0.2, 0.1, 0.05);
  grade.uniforms.time.value = clockT % 10;
  AUDIO.update(PLAYER.car, PLAYER.stars > 0 ? clamp(1 - nearestCop / 140, 0, 1) : 0, clockT);
  HUD.update(dt, clockT, nearestCop);
  INPUT.pressed.clear();
  composer.render(dt);
  void env;
  requestAnimationFrame(frame);
}
function boot() {
  buildWorld(); buildOcean(); buildNeon(); LIGHTS.build(); buildLightPools();
  initPlayer(); spawnParked();
  for (let k = 0; k < CFG.traffic.count; k++) spawnTrafficCar(PLAYER.pos);
  for (let k = 0; k < CFG.peds.count; k++) { const blk = CITY.blockAt(PLAYER.pos.x + rand(-60, 60), PLAYER.pos.z + rand(-60, 60)); if (blk && blk.type !== "harbor") spawnPed(blk); }
  resize();
  CAMERA.pos.set(PLAYER.pos.x, 4, PLAYER.pos.z + 6);
  $("loading").hidden = true;
  requestAnimationFrame(t => { lastFrame = t; frame(t); });
  // test / tuning hooks
  window.__nh = { PLAYER, VEHICLES, PEDS, CAMERA, DAY, MISSIONS, enterCar, nearestEnterable, addHeat, spawnPolice, CITY, setTime: t => { DAY.t = t; DAY.lastEnv = -1; },
    lineup() { // showroom for screenshots
      const x0 = SPAWN.x - 14, z = SPAWN.z + 1.5; let k = 0;
      for (const type of Object.keys(CAR_TYPES)) { const c = makeCar(type); c.pos.set(x0 + k * 5.4, 0.18, z); c.yaw = 0.6; c.ai = { mode: "parked" }; syncCar(c); VEHICLES.push(c); k++; }
      CAMERA.override = { pos: new THREE.Vector3(x0 + 13, 2.6, z + 9), look: new THREE.Vector3(x0 + 13, 0.6, z) };
    } };
}
$("play").addEventListener("click", () => { $("intro").hidden = true; started = true; AUDIO.start(); });
$("helpBtn").addEventListener("click", () => { $("intro").hidden = false; });
document.addEventListener("keydown", e => { if (e.code === "KeyT" && started) { DAY.t = (DAY.t + 0.08) % 1; } if (e.code === "Escape" && !$("intro").hidden && started) $("intro").hidden = true; });
setTimeout(boot, 30);
