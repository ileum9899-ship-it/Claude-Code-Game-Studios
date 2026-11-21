// ---------------------------------------------------------------- vehicle accidents (= Core/CrashModel.cs):
// impact damage, dented bodywork, broken lights, debris, engine smoke, windscreen ejection,
// reckless traffic that runs red lights, drivers who get out of wrecks.
const CRASH_MODEL = {
  damage(v) { return v < 3 ? 0 : Math.min(120, 1.1 * Math.pow(v - 3, 1.35)); },
  dent(v) { return clamp((v - 4) * 0.012, 0, 0.28); },
  lightsBreak(v) { return v >= 9; },
  frontal(fx, fz, nx, nz) { return Math.max(0, -(fx * nx + fz * nz)); }, // n points out of the obstacle, into the car
  ejects(v, frontal) { return v * frontal >= 24; },
  injury(v) { return clamp(15 + (v - 24) * 2.5, 15, 60); },
  smoke(h) { return h < 25 ? 2 : h < 50 ? 1 : 0; },
  performance(h) { return lerp(0.55, 1, clamp(h / 60, 0, 1)); },
};

const CRASH = (() => {
  const P = PLAYER;
  const MAT_BROKEN = new THREE.MeshStandardMaterial({ color: col(0x1a1a1a), roughness: 0.6 });

  // push body vertices near the contact point inward (car-local space); body geometry is cloned on first dent
  function dent(car, wx, wz, wy, nx, nz, depth) {
    const bodyMesh = car.body.children[0]; if (!bodyMesh || !bodyMesh.geometry) return;
    if (!car.dented) { bodyMesh.geometry = bodyMesh.geometry.clone(); car.dented = true; }
    const s = Math.sin(-car.yaw), c = Math.cos(-car.yaw);
    const lx = (wx - car.pos.x) * c + (wz - car.pos.z) * s, lz = -(wx - car.pos.x) * s + (wz - car.pos.z) * c;
    const lnx = nx * c + nz * s, lnz = -nx * s + nz * c; // into the car, local
    const pos = bodyMesh.geometry.attributes.position, R = 0.95;
    for (let k = 0; k < pos.count; k++) {
      const dx = pos.getX(k) - lx, dy = pos.getY(k) - wy, dz = pos.getZ(k) - lz, d = Math.sqrt(dx * dx + dy * dy * 0.5 + dz * dz);
      if (d >= R) continue;
      const f = depth * (1 - d / R) * (0.8 + 0.4 * Math.random());
      pos.setX(k, pos.getX(k) + lnx * f); pos.setZ(k, pos.getZ(k) + lnz * f); pos.setY(k, pos.getY(k) - f * 0.25);
    }
    pos.needsUpdate = true; bodyMesh.geometry.computeVertexNormals();
  }

  function breakLights(car, front) {
    const list = front ? car.heads : car.tails;
    if (!list) return;
    list.forEach(m => { m.material = MAT_BROKEN; });
    if (front) car.headsBroken = true; else car.tailsBroken = true;
  }

  function debris(x, y, z, v) {
    const n = Math.min(14, 3 + v * 0.5);
    for (let k = 0; k < n; k++) COMBAT.fx("spark", new THREE.Vector3(x, y, z), new THREE.Vector3(rand(-3, 3), rand(1, 4), rand(-3, 3)), rand(0.2, 0.45), rand(0.06, 0.14));
    if (v > 9) for (let k = 0; k < 4; k++) COMBAT.fx("smoke", new THREE.Vector3(x, y, z), new THREE.Vector3(rand(-1, 1), rand(0.5, 1.5), rand(-1, 1)), rand(0.8, 1.5), 0.8, 2);
  }

  function eject(car, speed) {
    // the player goes through the windscreen
    const f = forwardOf(car);
    if (car.driverMesh) { car.body.remove(car.driverMesh.root); car.driverMesh = null; }
    car.ai = { mode: "parked" }; car.throttle = 0; car.brake = 1;
    P.car = null; P.rig.root.visible = true;
    P.pos.set(car.pos.x + f.x * (car.spec.len / 2 + 0.6), 1.2, car.pos.z + f.y * (car.spec.len / 2 + 0.6));
    P.yaw = car.yaw;
    P.downTimer = 0; P.knock(f.x * speed * 0.55, f.y * speed * 0.55, 0);
    P.health -= CRASH_MODEL.injury(speed); HUD.hurt(1);
    HUD.big("앞유리 밖으로 튕겨 나갔습니다!", "", "bad");
  }

  // one impact on one car; (nx, nz) is the contact normal pointing INTO this car
  function impact(car, speed, px, pz, nx, nz, other) {
    if (car.exploded || speed < 3) return;
    car.health -= CRASH_MODEL.damage(speed);
    const depth = CRASH_MODEL.dent(speed);
    if (depth > 0) dent(car, px, pz, 0.75, nx, nz, depth);
    const f = forwardOf(car), front = (px - car.pos.x) * f.x + (pz - car.pos.z) * f.y > 0;
    if (CRASH_MODEL.lightsBreak(speed) && !(front ? car.headsBroken : car.tailsBroken)) breakLights(car, front);
    debris(px, 0.8, pz, speed);
    if (car === P.car) {
      const frontal = CRASH_MODEL.frontal(f.x, f.y, nx, nz);
      if (CRASH_MODEL.ejects(speed, frontal)) { eject(car, speed); return; }
      if (speed > 8) WANTED.heard(P.pos, CFG.gta.hearCrash, "충돌음");
      if (other && other.type !== "police" && speed > 6) WANTED.crime(0.5, "교통사고", false, true); // only counts if police see it
    }
  }

  // drivers of wrecked traffic cars get out once the car stops: shaken, then walk (or run) away
  function bailOut(v, dt) {
    if (v.bailed || !v.ai || v.ai.mode !== "wreck" || !v.driverMesh) return;
    if (speedOf(v) > 0.5) { v.bailT = 0; return; }
    v.bailT = (v.bailT || 0) + dt; if (v.bailT < 1.5) return;
    v.bailed = true; v.body.remove(v.driverMesh.root); v.driverMesh = null;
    const blk = CITY.blockAt(v.pos.x + 8, v.pos.z) || CITY.blockAt(v.pos.x - 8, v.pos.z) || CITY.blockAt(v.pos.x, v.pos.z + 8) || CITY.blockAt(v.pos.x, v.pos.z - 8);
    if (!blk) return;
    const p = spawnPed(blk); const f = forwardOf(v);
    p.pos.set(v.pos.x - f.y * 2, 0.18, v.pos.z + f.x * 2);
    if (v.health < 60 || P.stars > 0) { p.state = "flee"; p.timer = 6; p.threat = v.pos.clone(); }
  }

  let smokeAcc = 0;
  return {
    MAT_BROKEN, impact,
    update(dt) {
      smokeAcc += dt;
      const puff = smokeAcc > 0.12; if (puff) smokeAcc = 0;
      for (const v of VEHICLES) {
        bailOut(v, dt);
        if (!puff || v.exploded || v.burning) continue;
        const lvl = CRASH_MODEL.smoke(v.health); if (!lvl) continue;
        const f = forwardOf(v), x = v.pos.x + f.x * v.spec.len * 0.38, z = v.pos.z + f.y * v.spec.len * 0.38;
        COMBAT.fx("smoke", new THREE.Vector3(x, 1.1, z), new THREE.Vector3(rand(-0.3, 0.3), rand(1.2, 2.2), rand(-0.3, 0.3)), lvl === 2 ? 2.2 : 1.4, lvl === 2 ? 1.1 : 0.7, 2.5, lvl === 2 ? 0.06 : 0.35);
      }
    },
  };
})();
