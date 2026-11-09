// ---------------------------------------------------------------- open-world systems: witnesses & police search, map + GPS,
// weapon wheel, gun shop, spray garage, hospital / police station, car radio, health regen.
// Mirrors Core/WantedSystem.cs and Core/Shop.cs in the Unity build.
CFG.gta = {
  witnessDelay: 4, witnessRadius: 30, searchBase: 60, searchPerStar: 25, sightCar: 90, sightFoot: 70,
  prices: { pistol: 400, smg: 1200, pistolAmmo: 60, smgAmmo: 120, armor: 300 }, sprayCost: 100, hospitalBill: 200,
  regenCap: 50, regenDelay: 5, regenRate: 3,
};

// ---------- places
const PLACES = (() => {
  const blk = (i, j) => CITY.blocks[j * CITY.N + i];
  const side = (b, t) => { const q = pedPathPoint(b, t); return { x: q[0], z: q[1] }; };
  return {
    hospital: { name: "병원", ...side(blk(3, 4), 0.38), color: "#ff5a6a", letter: "H" },
    police: { name: "경찰서", ...side(blk(5, 5), 0.31), color: "#4b8cff", letter: "P" },
    gunshop: { name: "총포사", ...side(blk(4, 3), 0.56), color: "#ff8a3c", letter: "G" },
    spray: { name: "스프레이 정비소", x: 2 * CITY.PITCH + 30, z: 2 * CITY.PITCH - 3, color: "#3cffc8", letter: "S", road: true },
  };
})();

// ---------- GTA-style wanted level
const WANTED = (() => {
  const P = PLAYER, G = CFG.gta;
  const reports = [];
  const st = { seen: false, searching: false, lastKnown: new THREE.Vector3(), anchor: false, searchPts: new Map() };
  const radius = () => G.searchBase + G.searchPerStar * P.stars;
  function copCanSee() {
    const eye = new THREE.Vector3(P.pos.x, (P.car ? 1.0 : P.pos.y + 1.4), P.pos.z);
    for (const v of VEHICLES) {
      if (v.type !== "police" || v.exploded || !v.ai || (v.ai.mode !== "police" && !(v.ai.mode === "parked" && P.stars > 0))) continue;
      const d = Math.hypot(v.pos.x - eye.x, v.pos.z - eye.z);
      if (d < G.sightCar && raycastBoxes(v.pos.x, 1.4, v.pos.z, eye.x, eye.y, eye.z) >= 0.999) return true;
    }
    for (const o of COMBAT.OFFICERS) {
      if (o.down) continue;
      const d = o.pos.distanceTo(eye);
      if (d < G.sightFoot && raycastBoxes(o.pos.x, o.pos.y + 1.5, o.pos.z, eye.x, eye.y, eye.z) >= 0.999) return true;
    }
    return false;
  }
  function witnessed() {
    for (const p of PEDS) if ((p.state === "walk" || p.state === "flee") && p.pos.distanceTo(P.pos) < G.witnessRadius) return true;
    return false;
  }
  function raise(n, why) {
    const before = P.stars;
    P.heat = clamp(P.heat + n, 0, CFG.wanted.maxStars + 0.99);
    P.stars = Math.min(CFG.wanted.maxStars, Math.floor(P.heat));
    P.evade = 0;
    if (before === 0 && P.stars > 0) st.anchor = true;
    if (P.stars > before) { AUDIO.star(); HUD.toast(`수배 레벨 ${"★".repeat(P.stars)} · ${why}`, "warn"); }
  }
  return {
    st, radius,
    raise,
    // a crime: police witnesses count now, bystanders phone it in, nobody watching → nothing
    crime(n, why, againstPolice = false) {
      if (againstPolice || copCanSee()) { raise(n, why); return; }
      if (witnessed()) { reports.push({ t: G.witnessDelay, n, why }); if (reports.length === 1) HUD.toast("누군가 휴대폰을 꺼냈습니다…", "warn"); }
    },
    get pending() { return reports.length; },
    clear() { P.heat = 0; P.stars = 0; P.evade = 0; P.bust = 0; reports.length = 0; st.searching = false; },
    update(dt) {
      for (let k = reports.length - 1; k >= 0; k--) {
        const r = reports[k]; r.t -= dt; if (r.t > 0) continue;
        reports.splice(k, 1); raise(r.n, "목격자 신고: " + r.why);
      }
      st.seen = P.stars > 0 && copCanSee();
      if (P.stars === 0) { st.searching = false; P.bust = 0; return; }
      if (st.anchor) { st.lastKnown.copy(P.pos); st.anchor = false; }
      if (st.seen) { st.lastKnown.copy(P.pos); st.searching = false; P.evade = 0; }
      else {
        st.searching = true;
        if (Math.hypot(P.pos.x - st.lastKnown.x, P.pos.z - st.lastKnown.z) > radius()) {
          P.evade += dt;
          if (P.evade > CFG.wanted.evadeSeconds) { this.clear(); HUD.toast("경찰을 따돌렸습니다", "good"); AUDIO.win(); MISSIONS.onEvaded(); }
        }
      }
    },
    // where a police unit should drive to: the player when seen, else a point inside the search area
    targetFor(car) {
      if (!st.searching) return PLAYER.car ? PLAYER.car.pos : PLAYER.pos;
      let p = st.searchPts.get(car);
      if (!p || Math.hypot(car.pos.x - p.x, car.pos.z - p.z) < 12 || p.lk !== st.lastKnown.x) {
        const a = rand(0, TAU), r = rand(0, radius() * 0.8);
        p = new THREE.Vector3(st.lastKnown.x + Math.cos(a) * r, 0, st.lastKnown.z + Math.sin(a) * r); p.lk = st.lastKnown.x;
        st.searchPts.set(car, p);
      }
      return p;
    },
  };
})();

// ---------- GPS route (shortest road path) to the waypoint or mission target
const GPS = (() => {
  let route = [], timer = 0, target = null, color = "#c86bff";
  return {
    waypoint: null,
    get route() { return route; }, get color() { return color; },
    update(dt) {
      timer -= dt; if (timer > 0) return; timer = 0.5;
      const mt = MISSIONS.target ? MISSIONS.target.position : null;
      target = mt || this.waypoint; color = mt ? "#ffd23c" : "#c86bff";
      if (!target) { route = []; return; }
      if (this.waypoint && !mt && Math.hypot(PLAYER.pos.x - this.waypoint.x, PLAYER.pos.z - this.waypoint.z) < 8) { this.waypoint = null; HUD.toast("목적지 도착"); route = []; return; }
      const nodes = CITY.path(CITY.nearestNode(PLAYER.pos.x, PLAYER.pos.z), CITY.nearestNode(target.x, target.z));
      route = [{ x: PLAYER.pos.x, z: PLAYER.pos.z }, ...nodes.map(n => ({ x: n[0] * CITY.PITCH, z: n[1] * CITY.PITCH })), { x: target.x, z: target.z }];
    },
  };
})();

// ---------- radio (procedural stations)
const RADIO = (() => {
  const STATIONS = [
    { name: "NEON FM", tag: "신스웨이브", bpm: 108 },
    { name: "HARBOR BEATS", tag: "힙합", bpm: 88 },
    { name: "LOW TIDE", tag: "앰비언트", bpm: 70 },
  ];
  let ctx = null, out = null, station = -1, nextT = 0, step = 0, timer = null;
  const ROOTS = [57, 53, 48, 55]; // A3 F3 C3 G3  (i–VI–III–VII in A minor)
  const mtof = m => 440 * Math.pow(2, (m - 69) / 12);
  function note(f, t, dur, type, vol, filt = 0) {
    const o = ctx.createOscillator(), g = ctx.createGain(); o.type = type; o.frequency.value = f;
    g.gain.setValueAtTime(0, t); g.gain.linearRampToValueAtTime(vol, t + 0.01); g.gain.exponentialRampToValueAtTime(0.001, t + dur);
    if (filt) { const fl = ctx.createBiquadFilter(); fl.type = "lowpass"; fl.frequency.value = filt; o.connect(fl); fl.connect(g); } else o.connect(g);
    g.connect(out); o.start(t); o.stop(t + dur + 0.05);
  }
  let noise = null;
  function hit(t, kind) {
    if (!noise) { noise = ctx.createBuffer(1, ctx.sampleRate * 0.5, ctx.sampleRate); const d = noise.getChannelData(0); for (let k = 0; k < d.length; k++) d[k] = Math.random() * 2 - 1; }
    if (kind === "kick") { const o = ctx.createOscillator(), g = ctx.createGain(); o.frequency.setValueAtTime(140, t); o.frequency.exponentialRampToValueAtTime(40, t + 0.15); g.gain.setValueAtTime(0.9, t); g.gain.exponentialRampToValueAtTime(0.001, t + 0.3); o.connect(g); g.connect(out); o.start(t); o.stop(t + 0.35); return; }
    const s = ctx.createBufferSource(); s.buffer = noise; const f = ctx.createBiquadFilter(); const g = ctx.createGain();
    f.type = kind === "hat" ? "highpass" : "bandpass"; f.frequency.value = kind === "hat" ? 7000 : 1800;
    const dur = kind === "hat" ? 0.05 : 0.18; g.gain.setValueAtTime(kind === "hat" ? 0.25 : 0.55, t); g.gain.exponentialRampToValueAtTime(0.001, t + dur);
    s.connect(f); f.connect(g); g.connect(out); s.start(t); s.stop(t + dur + 0.02);
  }
  function schedule() {
    if (station < 0 || !ctx) return;
    const S = STATIONS[station], sixteenth = 60 / S.bpm / 4;
    while (nextT < ctx.currentTime + 0.15) {
      const t = nextT, bar = Math.floor(step / 16) % 4, s16 = step % 16, root = ROOTS[bar];
      if (station === 0) { // synthwave: driving octave bass, arpeggio, four-on-the-floor
        note(mtof(root - 12 + (s16 % 2 ? 12 : 0)), t, sixteenth * 0.9, "sawtooth", 0.16, 900);
        if (s16 % 2 === 0) note(mtof(root + 12 + [0, 3, 7, 12, 7, 3, 0, 7][(s16 / 2) % 8]), t, sixteenth * 1.6, "square", 0.05, 2400);
        if (s16 % 4 === 0) hit(t, "kick"); if (s16 === 4 || s16 === 12) hit(t, "snare"); if (s16 % 2 === 1) hit(t, "hat");
        if (s16 === 0) [0, 3, 7].forEach(i => note(mtof(root + 12 + i), t, sixteenth * 15, "triangle", 0.035));
      } else if (station === 1) { // boom-bap: swung hats, sub bass, sparse keys
        if (s16 === 0 || s16 === 7 || s16 === 10) hit(t, "kick"); if (s16 === 4 || s16 === 12) hit(t, "snare"); if (s16 % 2 === 0) hit(t + (s16 % 4 === 2 ? sixteenth * 0.3 : 0), "hat");
        if (s16 === 0 || s16 === 10) note(mtof(root - 24), t, sixteenth * 5, "sine", 0.4);
        if (s16 === 2 || s16 === 6 || s16 === 14) note(mtof(root + [3, 7, 10][((s16 / 4) | 0) % 3]), t, sixteenth * 3, "triangle", 0.07, 1600);
      } else { // ambient pads + slow bell
        if (s16 === 0) [0, 7, 12, 15].forEach(i => note(mtof(root + i), t, sixteenth * 18, "sine", 0.06));
        if (s16 === 8 && Math.random() < 0.6) note(mtof(root + 24 + [0, 3, 7, 10][(Math.random() * 4) | 0]), t, sixteenth * 10, "sine", 0.05);
      }
      nextT += sixteenth; step++;
    }
  }
  return {
    STATIONS,
    get station() { return station; },
    set(i, quiet = false) {
      station = i;
      if (i >= 0) {
        try { if (!ctx) { ctx = new (window.AudioContext || window.webkitAudioContext)(); out = ctx.createGain(); out.gain.value = 0.32; out.connect(ctx.destination); } } catch (e) { return; }
        if (ctx.state === "suspended") ctx.resume();
        nextT = ctx.currentTime + 0.05; step = 0;
        if (!timer) timer = setInterval(schedule, 40);
        HUD.toast(`라디오: ${STATIONS[i].name} · ${STATIONS[i].tag}`);
      } else if (!quiet) HUD.toast("라디오 끔");
    },
    next(dir) { const n = STATIONS.length + 1; this.set(((station + 1 + dir) % n + n) % n - 1); },
  };
})();

// ---------- UI: full map, weapon wheel, shop
const UI = (() => {
  const P = PLAYER;
  let mode = null; // "map" | "wheel" | "shop" | null
  let wheelSel = 0, wheelX = 0, wheelY = 0, sprayLock = 0, healthPrev = 100, lastHurt = 0;
  const mapEl = $("bigmap"), mapCv = $("mapcv"), mg = mapCv.getContext("2d");
  const SHOP_ITEMS = [
    { id: "pistol", name: "권총", desc: "반자동 · 12발 탄창" }, { id: "smg", name: "기관단총", desc: "자동 · 30발 탄창" },
    { id: "pistolAmmo", name: "권총 탄약 ×24", desc: "탄창 2개" }, { id: "smgAmmo", name: "기관단총 탄약 ×60", desc: "탄창 2개" },
    { id: "armor", name: "방탄복", desc: "피해의 2/3 흡수" },
  ];
  function buy(id) {
    const price = CFG.gta.prices[id], A = P.arsenal;
    if ((id === "pistol" || id === "smg") && A[id].owned) return HUD.toast("이미 가지고 있습니다");
    if (id === "pistolAmmo" && !A.pistol.owned) return HUD.toast("권총이 필요합니다");
    if (id === "smgAmmo" && !A.smg.owned) return HUD.toast("기관단총이 필요합니다");
    if (id === "armor" && P.armor >= 100) return HUD.toast("방탄복이 이미 가득합니다");
    if (P.money < price) return HUD.toast("돈이 부족합니다", "warn");
    P.money -= price; AUDIO.cash();
    if (id === "pistol" || id === "smg") { const s = WEAPONS[id]; A[id].owned = true; A[id].clip = s.mag; A[id].reserve = s.start - s.mag; P.weapon = id; }
    else if (id === "pistolAmmo") A.pistol.reserve += 24; else if (id === "smgAmmo") A.smg.reserve += 60; else P.armor = 100;
    HUD.toast(`구매: ${SHOP_ITEMS.find(x => x.id === id).name}`, "good"); renderShop();
  }
  function renderShop() {
    $("shoplist").innerHTML = SHOP_ITEMS.map((it, k) => `<button data-buy="${it.id}"><kbd>${k + 1}</kbd><span><b>${it.name}</b><small>${it.desc}</small></span><em>$${CFG.gta.prices[it.id].toLocaleString()}</em></button>`).join("");
    $("shopmoney").textContent = "$" + Math.floor(P.money).toLocaleString();
    document.querySelectorAll("[data-buy]").forEach(b => b.onclick = () => buy(b.dataset.buy));
  }
  function open(m) {
    mode = m;
    mapEl.hidden = m !== "map"; $("shop").hidden = m !== "shop"; $("wheel").hidden = m !== "wheel";
    if (m === "shop") renderShop();
    if (m === "map") drawMap();
    if (m && m !== "wheel" && document.pointerLockElement) document.exitPointerLock();
  }
  // map: north = -z at the top, scaled to fit
  const S = 1.15, O = 50;
  function mapScale() { const W = mapCv.width, base = HUD.baseMap; return Math.min(W / base.width, mapCv.height / base.height); }
  function drawMap() {
    const base = HUD.baseMap, k = mapScale();
    mapCv.width = mapEl.clientWidth * devicePixelRatio; mapCv.height = mapEl.clientHeight * devicePixelRatio;
    const kk = Math.min(mapCv.width / base.width, mapCv.height / base.height), ox = (mapCv.width - base.width * kk) / 2, oy = (mapCv.height - base.height * kk) / 2;
    mg.fillStyle = "#0b1622"; mg.fillRect(0, 0, mapCv.width, mapCv.height);
    mg.drawImage(base, ox, oy, base.width * kk, base.height * kk);
    const W2S = (x, z) => [ox + (x + O) * S * kk, oy + (z + O) * S * kk];
    mapEl._w2s = W2S; mapEl._s2w = (sx, sy) => [(sx - ox) / (S * kk) - O, (sy - oy) / (S * kk) - O];
    if (GPS.route.length) { mg.strokeStyle = GPS.color; mg.lineWidth = 4 * devicePixelRatio; mg.beginPath(); GPS.route.forEach((p, i) => { const [x, y] = W2S(p.x, p.z); i ? mg.lineTo(x, y) : mg.moveTo(x, y); }); mg.stroke(); }
    if (P.stars > 0 && WANTED.st.searching) { const [x, y] = W2S(WANTED.st.lastKnown.x, WANTED.st.lastKnown.z); mg.fillStyle = "rgba(255,60,80,0.18)"; mg.beginPath(); mg.arc(x, y, WANTED.radius() * S * kk, 0, TAU); mg.fill(); }
    const icon = (x, z, color, letter) => { const [sx, sy] = W2S(x, z), r = 11 * devicePixelRatio; mg.fillStyle = color; mg.beginPath(); mg.arc(sx, sy, r, 0, TAU); mg.fill(); mg.fillStyle = "#0b0f16"; mg.font = `bold ${13 * devicePixelRatio}px sans-serif`; mg.textAlign = "center"; mg.textBaseline = "middle"; mg.fillText(letter, sx, sy + 1); };
    for (const pl of Object.values(PLACES)) icon(pl.x, pl.z, pl.color, pl.letter);
    MISSIONS.jobs.forEach(j => icon(j.x, j.z, "#ffc23a", "!"));
    if (GPS.waypoint) icon(GPS.waypoint.x, GPS.waypoint.z, "#c86bff", "W");
    const [px, py] = W2S(P.pos.x, P.pos.z), h = P.car ? P.car.yaw : P.yaw;
    mg.save(); mg.translate(px, py); mg.rotate(Math.PI - h); mg.fillStyle = "#fff"; mg.strokeStyle = "#000"; mg.lineWidth = 2;
    mg.beginPath(); mg.moveTo(0, -14 * devicePixelRatio); mg.lineTo(9 * devicePixelRatio, 10 * devicePixelRatio); mg.lineTo(0, 5 * devicePixelRatio); mg.lineTo(-9 * devicePixelRatio, 10 * devicePixelRatio); mg.closePath(); mg.fill(); mg.stroke(); mg.restore();
  }
  mapCv.addEventListener("click", e => {
    const r = mapCv.getBoundingClientRect(); const [x, z] = mapEl._s2w((e.clientX - r.left) * devicePixelRatio, (e.clientY - r.top) * devicePixelRatio);
    if (GPS.waypoint && Math.hypot(GPS.waypoint.x - x, GPS.waypoint.z - z) < 25) { GPS.waypoint = null; HUD.toast("웨이포인트 해제"); }
    else { GPS.waypoint = new THREE.Vector3(clamp(x, 0, CITY.SIZE), 0, clamp(z, 0, CITY.SIZE)); HUD.toast("웨이포인트 설정 — 미니맵의 보라색 경로를 따라가세요"); }
    GPS.update(1); drawMap();
  });
  $("mapclose").onclick = () => open(null); $("shopclose").onclick = () => open(null);
  $("mapBtn").onclick = () => open(mode === "map" ? null : "map");
  // weapon wheel
  const SLOTS = ["fist", "pistol", "smg"];
  function renderWheel() {
    document.querySelectorAll("#wheel [data-slot]").forEach((el, k) => {
      const id = SLOTS[k], w = P.arsenal[id], s = WEAPONS[id];
      el.classList.toggle("sel", k === wheelSel); el.classList.toggle("off", !w.owned);
      el.querySelector("small").textContent = s.melee ? "" : w.owned ? `${w.clip} / ${w.reserve}` : "없음";
    });
  }
  function near(pl, r) { return Math.hypot(P.pos.x - pl.x, P.pos.z - pl.z) < r; }
  return {
    get mode() { return mode; },
    get paused() { return mode === "map" || mode === "shop"; },
    get timeScale() { return mode === "wheel" ? 0.25 : 1; },
    respawnPoint(kind) { const p = PLACES[kind]; return new THREE.Vector3(p.x, 0.18, p.z); },
    update(dt) {
      const k = INPUT.pressed;
      if ((k.has("KeyM") || k.has("Escape") && mode === "map") && mode !== "shop") open(mode === "map" ? null : "map");
      if (mode === "shop") {
        SHOP_ITEMS.forEach((it, i) => { if (k.has("Digit" + (i + 1))) buy(it.id); });
        if (k.has("Escape") || k.has("KeyF") || !near(PLACES.gunshop, 4)) open(null);
        k.delete("KeyF"); k.delete("Digit1"); k.delete("Digit2"); k.delete("Digit3");
        return;
      }
      if (mode === "map") return;
      // weapon wheel while Tab is held
      if (isDown("Tab") && !P.car && GAME.state === "play") {
        if (mode !== "wheel") { open("wheel"); wheelX = wheelY = 0; wheelSel = SLOTS.indexOf(P.weapon); }
        wheelX += INPUT.mdx; wheelY += INPUT.mdy; INPUT.mdx = INPUT.mdy = 0;
        if (Math.hypot(wheelX, wheelY) > 30) { const a = Math.atan2(wheelX, -wheelY); wheelSel = a < -Math.PI / 3 ? 2 : a > Math.PI / 3 ? 1 : 0; }
        renderWheel();
      } else if (mode === "wheel") { const id = SLOTS[wheelSel]; if (P.arsenal[id].owned) { P.weapon = id; P.reload = 0; } open(null); }
      // shops & services
      if (!P.car && near(PLACES.gunshop, 2.5) && GAME.state === "play" && mode === null && !UI._shopCooldown) { open("shop"); UI._shopCooldown = true; }
      if (!near(PLACES.gunshop, 4)) UI._shopCooldown = false;
      sprayLock -= dt;
      if (P.car && near(PLACES.spray, 5) && sprayLock <= 0) {
        sprayLock = 4;
        if (WANTED.st.seen) HUD.toast("경찰이 보고 있어 정비소에 들어갈 수 없습니다", "warn");
        else if (P.money < CFG.gta.sprayCost) HUD.toast(`$${CFG.gta.sprayCost}가 필요합니다`, "warn");
        else {
          P.money -= CFG.gta.sprayCost; const c = P.car;
          const pm = paintMat(pick([0x1d3b6e, 0xb8b8bc, 0x111114, 0x8a1d1d, 0xe6e6e6, 0x2e4d3a, 0xf2c31b, 0x1f8fd6]));
          c.root.traverse(m => { if (m.isMesh && m.material && m.material.isMeshPhysicalMaterial && m.material !== paintMat(0xf2f2f2)) m.material = pm; });
          c.health = 100; c.burning = 0; GAME.fade(1.2);
          const had = P.stars > 0; WANTED.clear(); AUDIO.cash();
          HUD.big("새 차 같네요", had ? "도색 완료 · 수배 해제" : `도색·수리 완료 -$${CFG.gta.sprayCost}`, "good");
        }
      }
      // radio
      if (P.car) {
        if (k.has("Period") || k.has("RadioNext")) RADIO.next(1);
        if (k.has("Comma")) RADIO.next(-1);
        if (!UI._inCar) { UI._inCar = true; RADIO.set(UI._lastStation ?? ((Math.random() * RADIO.STATIONS.length) | 0)); }
      } else if (UI._inCar) { UI._inCar = false; UI._lastStation = RADIO.station; RADIO.set(-1, true); }
      // health regen (to half) after a quiet spell
      if (P.health < healthPrev - 0.01) lastHurt = 0; else lastHurt += dt;
      if (lastHurt > CFG.gta.regenDelay && P.health > 0 && P.health < CFG.gta.regenCap) P.health = Math.min(CFG.gta.regenCap, P.health + CFG.gta.regenRate * dt);
      healthPrev = P.health;
    },
    redrawMap() { if (mode === "map") drawMap(); },
  };
})();

// world markers for places
(() => {
  for (const pl of Object.values(PLACES)) {
    const c = new THREE.Color(pl.color);
    const b = beam(c); b.position.set(pl.x, pl.road ? 0 : 0.18, pl.z);
    b.children[0].scale.set(pl.road ? 2.2 : 1, 0.35, pl.road ? 2.2 : 1); b.children[0].position.y = 7; b.userData.ring.scale.setScalar(pl.road ? 2.2 : 1);
  }
})();
