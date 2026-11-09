// ---------------------------------------------------------------- humans (procedural rig)
const HUMAN_GEO = {
  torso: (() => { const g = new THREE.CylinderGeometry(0.2, 0.17, 0.62, 10); g.translate(0, -0.31, 0); return g; })(),
  hips: (() => { const g = new THREE.CylinderGeometry(0.18, 0.19, 0.2, 10); g.translate(0, -0.1, 0); return g; })(),
  limb: (() => { const g = new THREE.CylinderGeometry(0.075, 0.06, 0.42, 8); g.translate(0, -0.21, 0); return g; })(),
  leg: (() => { const g = new THREE.CylinderGeometry(0.095, 0.075, 0.46, 8); g.translate(0, -0.23, 0); return g; })(),
  head: new THREE.SphereGeometry(0.13, 16, 12),
  hair: (() => { const g = new THREE.SphereGeometry(0.137, 16, 12, 0, TAU, 0, Math.PI * 0.55); return g; })(),
  shoe: (() => { const g = new THREE.BoxGeometry(0.12, 0.08, 0.26); g.translate(0, -0.04, 0.05); return g; })(),
  hand: new THREE.SphereGeometry(0.055, 8, 6),
};
const humanMatCache = new Map();
function hmat(hex, rough = 0.8) {
  const k = hex + ":" + rough;
  if (!humanMatCache.has(k)) humanMatCache.set(k, new THREE.MeshStandardMaterial({ color: col(hex), roughness: rough }));
  return humanMatCache.get(k);
}
const SKIN = [0xf1c9a5, 0xe0ac85, 0xc68863, 0x8d5a3b, 0xf5d7bf];
const TOPS = [0x2f4f7f, 0xb33a3a, 0x3d3d3d, 0xe8e2d4, 0x6b8e3a, 0xd9a52b, 0x7a4a9c, 0x1f7a7a, 0xc75b2a];
const BOTTOMS = [0x2b2f3a, 0x3c4a63, 0x5a4a3a, 0x1d1d22, 0x6f6a5f];
const HAIR = [0x1a1410, 0x3b2a1e, 0x6b4a2b, 0xb08a52, 0x2b2b2b];
function makeHuman(o = {}) {
  const root = new THREE.Group();
  const top = hmat(o.top ?? pick(TOPS)), bottom = hmat(o.bottom ?? pick(BOTTOMS)), skin = hmat(o.skin ?? pick(SKIN), 0.6);
  const add = (geo, mat, parent, x, y, z) => { const m = new THREE.Mesh(geo, mat); m.position.set(x, y, z); m.castShadow = true; parent.add(m); return m; };
  const pelvis = new THREE.Group(); pelvis.position.y = 0.98; root.add(pelvis);
  add(HUMAN_GEO.hips, bottom, pelvis, 0, 0.1, 0);
  const chest = new THREE.Group(); chest.position.y = 0.6; pelvis.add(chest);
  add(HUMAN_GEO.torso, top, chest, 0, 0, 0).scale.set(1.1, 1, 0.75);
  const head = add(HUMAN_GEO.head, skin, chest, 0, 0.17, 0); head.scale.set(1, 1.12, 1.02);
  add(HUMAN_GEO.hair, hmat(o.hair ?? pick(HAIR), 0.9), head, 0, 0.012, -0.004).scale.set(1, 1, 1.05);
  const limb = (geo, mat, parent, x, y) => { const j = new THREE.Group(); j.position.set(x, y, 0); parent.add(j); add(geo, mat, j, 0, 0, 0); return j; };
  const armL = limb(HUMAN_GEO.limb, top, chest, -0.27, -0.04), armR = limb(HUMAN_GEO.limb, top, chest, 0.27, -0.04);
  const foreL = limb(HUMAN_GEO.limb, skin, armL, 0, -0.42), foreR = limb(HUMAN_GEO.limb, skin, armR, 0, -0.42);
  add(HUMAN_GEO.hand, skin, foreL, 0, -0.44, 0); add(HUMAN_GEO.hand, skin, foreR, 0, -0.44, 0);
  const legL = limb(HUMAN_GEO.leg, bottom, pelvis, -0.1, -0.02), legR = limb(HUMAN_GEO.leg, bottom, pelvis, 0.1, -0.02);
  const shinL = limb(HUMAN_GEO.leg, bottom, legL, 0, -0.46), shinR = limb(HUMAN_GEO.leg, bottom, legR, 0, -0.46);
  add(HUMAN_GEO.shoe, hmat(0x1b1b1b, 0.6), shinL, 0, -0.46, 0); add(HUMAN_GEO.shoe, hmat(0x1b1b1b, 0.6), shinR, 0, -0.46, 0);
  return { root, pelvis, chest, head, armL, armR, foreL, foreR, legL, legR, shinL, shinR, phase: rand(0, TAU) };
}
// speed in m/s, airborne flag; called every frame
function animateHuman(h, speed, dt, air = false) {
  const run = clamp((speed - 2.5) / 3.5, 0, 1);
  h.phase += dt * (speed < 0.1 ? 0 : 2.2 + speed * 1.15);
  const s = Math.sin(h.phase), c = Math.cos(h.phase);
  const amp = clamp(speed / 2, 0, 1) * lerp(0.45, 0.85, run);
  if (air) {
    h.legL.rotation.x = -0.6; h.legR.rotation.x = 0.3; h.shinL.rotation.x = 0.9; h.shinR.rotation.x = 0.4;
    h.armL.rotation.x = -0.4; h.armR.rotation.x = -0.4; h.armL.rotation.z = 0.5; h.armR.rotation.z = -0.5; return;
  }
  h.legL.rotation.x = s * amp; h.legR.rotation.x = -s * amp;
  h.shinL.rotation.x = Math.max(0, -c) * amp * 1.3 + 0.05; h.shinR.rotation.x = Math.max(0, c) * amp * 1.3 + 0.05;
  h.armL.rotation.x = -s * amp * 0.9; h.armR.rotation.x = s * amp * 0.9;
  h.armL.rotation.z = 0.08; h.armR.rotation.z = -0.08;
  h.foreL.rotation.x = -0.25 - run * 1.0; h.foreR.rotation.x = -0.25 - run * 1.0;
  h.chest.rotation.x = run * 0.22; h.chest.rotation.y = s * amp * 0.15;
  h.pelvis.position.y = 0.98 + Math.abs(c) * amp * 0.06 - run * 0.04 + (speed < 0.1 ? Math.sin(performance.now() / 900 + h.phase) * 0.006 : 0);
}
function poseSit(h) {
  h.legL.rotation.x = h.legR.rotation.x = -1.4; h.shinL.rotation.x = h.shinR.rotation.x = 1.4;
  h.armL.rotation.x = h.armR.rotation.x = -0.9; h.foreL.rotation.x = h.foreR.rotation.x = -0.4;
}

// ---------------------------------------------------------------- vehicles
// Body made from an extruded side profile (x = length axis, y = up) so cars read as cars.
const CAR_TYPES = {
  sedan:  { len: 4.6, wid: 1.85, profile: [[-2.3, 0.35], [2.3, 0.35], [2.32, 0.78], [1.6, 0.92], [0.85, 1.42], [-0.9, 1.45], [-1.85, 0.98], [-2.3, 0.92]], wheel: 0.36, mass: 1, paint: [0x1d3b6e, 0xb8b8bc, 0x111114, 0x8a1d1d, 0xe6e6e6, 0x2e4d3a, 0x6b5b3e] },
  sports: { len: 4.4, wid: 1.92, profile: [[-2.2, 0.3], [2.2, 0.3], [2.25, 0.62], [1.3, 0.78], [0.4, 1.18], [-0.7, 1.2], [-1.75, 0.9], [-2.22, 0.82]], wheel: 0.36, mass: 0.9, paint: [0xd61f1f, 0xf2c31b, 0x1f8fd6, 0x111114, 0xf06a14] },
  suv:    { len: 4.9, wid: 2.0, profile: [[-2.45, 0.45], [2.45, 0.45], [2.48, 1.0], [1.85, 1.12], [1.25, 1.8], [-2.25, 1.85], [-2.45, 1.2]], wheel: 0.44, mass: 1.35, paint: [0x2b2b2e, 0xdcdcdc, 0x3b4a2e, 0x5c1e1e, 0x23395c] },
  taxi:   { len: 4.6, wid: 1.85, profile: [[-2.3, 0.35], [2.3, 0.35], [2.32, 0.78], [1.6, 0.92], [0.85, 1.42], [-0.9, 1.45], [-1.85, 0.98], [-2.3, 0.92]], wheel: 0.36, mass: 1, paint: [0xf2b01e] },
  van:    { len: 5.2, wid: 2.05, profile: [[-2.6, 0.45], [2.6, 0.45], [2.62, 1.05], [2.1, 1.3], [1.6, 2.15], [-2.6, 2.2]], wheel: 0.4, mass: 1.6, paint: [0xe8e8e8, 0x2f5f8f, 0x8f2f2f] },
  police: { len: 4.7, wid: 1.9, profile: [[-2.35, 0.35], [2.35, 0.35], [2.37, 0.8], [1.65, 0.94], [0.85, 1.44], [-0.95, 1.47], [-1.9, 1.0], [-2.35, 0.94]], wheel: 0.37, mass: 1.1, paint: [0x101318] },
};
const carGeoCache = {};
const paintCache = new Map();
const MAT_GLASS = new THREE.MeshStandardMaterial({ color: col(0x05080c), roughness: 0.04, metalness: 0.3, envMapIntensity: 0.9 });
const MAT_UNDER = new THREE.MeshStandardMaterial({ color: col(0x0c0c0d), roughness: 0.9 });
const MAT_TIRE = new THREE.MeshStandardMaterial({ color: col(0x161616), roughness: 0.92 });
const MAT_RIM = new THREE.MeshStandardMaterial({ color: col(0xb9bec4), roughness: 0.25, metalness: 1 });
const MAT_CHROME = new THREE.MeshStandardMaterial({ color: col(0x202326), roughness: 0.4, metalness: 0.6 });
const MAT_HEAD = new THREE.MeshBasicMaterial({ color: new THREE.Color(2.2, 2.1, 1.8) });
const MAT_TAIL = new THREE.MeshBasicMaterial({ color: new THREE.Color(1.2, 0.05, 0.03) });
const MAT_BRAKE = new THREE.MeshBasicMaterial({ color: new THREE.Color(6, 0.2, 0.1) });
const MAT_SIREN_R = new THREE.MeshBasicMaterial({ color: new THREE.Color(0.3, 0.02, 0.02) });
const MAT_SIREN_B = new THREE.MeshBasicMaterial({ color: new THREE.Color(0.02, 0.05, 0.3) });
function paintMat(hex) {
  if (!paintCache.has(hex)) paintCache.set(hex, new THREE.MeshPhysicalMaterial({ color: col(hex), roughness: 0.38, metalness: 0.35, clearcoat: 1, clearcoatRoughness: 0.06, envMapIntensity: 1.2 }));
  return paintCache.get(hex);
}
function carGeometry(type) {
  if (carGeoCache[type]) return carGeoCache[type];
  const t = CAR_TYPES[type];
  // silhouette with wheel arches cut into the sill so the tyres show
  const L = t.len / 2, yb = t.profile[0][1], ra = t.wheel + 0.1, xf = L - 0.82, xr = -(L - 0.82) + 0.05;
  const shape = new THREE.Shape();
  shape.moveTo(t.profile[0][0], yb);
  shape.lineTo(xr - ra, yb); shape.absarc(xr, t.wheel, ra, Math.PI, 0, true); shape.lineTo(xr + ra, yb);
  shape.lineTo(xf - ra, yb); shape.absarc(xf, t.wheel, ra, Math.PI, 0, true); shape.lineTo(xf + ra, yb);
  for (let k = 1; k < t.profile.length; k++) shape.lineTo(t.profile[k][0], t.profile[k][1]);
  shape.closePath();
  const body = new THREE.ExtrudeGeometry(shape, { depth: t.wid - 0.16, bevelEnabled: true, bevelThickness: 0.08, bevelSize: 0.07, bevelSegments: 3, steps: 1 });
  body.translate(0, 0, -(t.wid - 0.16) / 2); body.rotateY(-Math.PI / 2); // length along +z
  body.computeVertexNormals();
  // greenhouse (glass) = upper part of profile, slightly inset
  const ys = t.profile.map(p => p[1]); const beltY = lerp(Math.min(...ys.filter(y => y > 0.6)), Math.max(...ys), 0.42);
  // glass = silhouette offset outward (so windscreen/rear glass sit on the body surface), clipped to the band
  // between the belt line and just under the roof, so the roof stays painted
  const P = t.profile, n = P.length, OFF = 0.085, top = Math.max(...ys) - 0.07;
  const grown = P.map((p, k) => {
    const a = P[(k - 1 + n) % n], c = P[(k + 1) % n];
    const e1 = [p[0] - a[0], p[1] - a[1]], e2 = [c[0] - p[0], c[1] - p[1]];
    const n1 = [e1[1], -e1[0]], n2 = [e2[1], -e2[0]];
    const l1 = Math.hypot(...n1) || 1, l2 = Math.hypot(...n2) || 1;
    let nx = n1[0] / l1 + n2[0] / l2, ny = n1[1] / l1 + n2[1] / l2; const l = Math.hypot(nx, ny) || 1;
    return [p[0] + nx / l * OFF, p[1] + ny / l * OFF];
  });
  const clipY = (poly, y0, keepAbove) => {
    const out = [];
    for (let k = 0; k < poly.length; k++) {
      const A = poly[k], Bp = poly[(k + 1) % poly.length];
      const ain = keepAbove ? A[1] >= y0 : A[1] <= y0, bin = keepAbove ? Bp[1] >= y0 : Bp[1] <= y0;
      if (ain) out.push(A);
      if (ain !== bin) { const u = (y0 - A[1]) / (Bp[1] - A[1]); out.push([A[0] + (Bp[0] - A[0]) * u, y0]); }
    }
    return out;
  };
  const glassPts = clipY(clipY(grown, beltY, true), top, false).map(p => new THREE.Vector2(p[0], p[1]));
  const glass = new THREE.ExtrudeGeometry(new THREE.Shape(glassPts), { depth: t.wid - 0.06, bevelEnabled: false });
  glass.translate(0, 0, -(t.wid - 0.06) / 2); glass.rotateY(-Math.PI / 2);
  const wheel = new THREE.CylinderGeometry(t.wheel, t.wheel, 0.26, 20); wheel.rotateZ(Math.PI / 2);
  const rim = new THREE.CylinderGeometry(t.wheel * 0.62, t.wheel * 0.62, 0.28, 10); rim.rotateZ(Math.PI / 2);
  carGeoCache[type] = { body, glass, wheel, rim, beltY };
  return carGeoCache[type];
}
let carIdSeq = 0;
function makeCar(type, paint) {
  const t = CAR_TYPES[type], g = carGeometry(type);
  const root = new THREE.Group(), body = new THREE.Group(); root.add(body);
  const pm = paintMat(paint ?? pick(t.paint));
  const bm = new THREE.Mesh(g.body, pm); bm.castShadow = true; bm.receiveShadow = true; body.add(bm);
  const gm = new THREE.Mesh(g.glass, MAT_GLASS); gm.castShadow = true; gm.receiveShadow = false; body.add(gm);
  const L = t.len / 2, W = t.wid / 2;
  const box = (w, h, d, mat, x, y, z) => { const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), mat); m.position.set(x, y, z); body.add(m); return m; };
  box(t.wid * 0.94, 0.2, 0.14, MAT_CHROME, 0, t.profile[0][1] + 0.1, L + 0.06);   // bumpers
  box(t.wid * 0.94, 0.2, 0.14, MAT_CHROME, 0, t.profile[0][1] + 0.1, -L - 0.06);
  box(t.wid * 0.36, 0.14, 0.02, new THREE.MeshStandardMaterial({ color: col(0xe8e4d8), roughness: 0.5 }), 0, t.profile[0][1] + 0.34, -L - 0.09); // plate
  const hy = t.profile[2][1] - 0.12;
  const heads = [box(0.42, 0.13, 0.05, MAT_HEAD, -W + 0.36, hy, L + 0.1), box(0.42, 0.13, 0.05, MAT_HEAD, W - 0.36, hy, L + 0.1)];
  const tails = [box(0.46, 0.14, 0.05, MAT_TAIL, -W + 0.36, hy + 0.05, -L - 0.1), box(0.46, 0.14, 0.05, MAT_TAIL, W - 0.36, hy + 0.05, -L - 0.1)];
  box(t.wid * 0.5, 0.2, 0.04, MAT_CHROME, 0, hy - 0.08, L + 0.09); // grille
  const wheels = [];
  const wz = L - 0.82, wy = t.wheel;
  box(t.wid * 0.86, 0.22, t.len * 0.9, MAT_UNDER, 0, t.profile[0][1] + 0.05, 0); // underbody fills the arches from inside
  const mirY = (g.beltY + t.profile[2][1]) / 2 + 0.05, mirZ = t.profile[4] ? t.profile[4][0] * 0.95 : 0.8;
  box(0.18, 0.1, 0.12, MAT_CHROME, -W - 0.08, mirY, mirZ); box(0.18, 0.1, 0.12, MAT_CHROME, W + 0.08, mirY, mirZ);
  for (const [x, z, front] of [[-W + 0.2, wz, true], [W - 0.2, wz, true], [-W + 0.2, -wz + 0.05, false], [W - 0.2, -wz + 0.05, false]]) {
    const pivot = new THREE.Group(); pivot.position.set(x, wy, z); root.add(pivot);
    const spin = new THREE.Group(); pivot.add(spin);
    const tm = new THREE.Mesh(g.wheel, MAT_TIRE); tm.castShadow = true; spin.add(tm);
    spin.add(new THREE.Mesh(g.rim, MAT_RIM));
    wheels.push({ pivot, spin, front });
  }
  let siren = null;
  if (type === "taxi") { const s = box(0.6, 0.16, 0.24, new THREE.MeshBasicMaterial({ color: new THREE.Color(2, 1.6, 0.4) }), 0, 1.53, -0.1); s.castShadow = false; }
  if (type === "police") {
    // white doors + light bar
    box(0.02, 0.42, 2.0, paintMat(0xf2f2f2), -W - 0.05, 0.78, 0.1); box(0.02, 0.42, 2.0, paintMat(0xf2f2f2), W + 0.05, 0.78, 0.1);
    box(1.2, 0.08, 0.3, MAT_CHROME, 0, 1.52, -0.1);
    const r = box(0.5, 0.14, 0.26, MAT_SIREN_R, -0.3, 1.62, -0.1), b = box(0.5, 0.14, 0.26, MAT_SIREN_B, 0.3, 1.62, -0.1);
    siren = { r, b, mr: new THREE.MeshBasicMaterial({ color: new THREE.Color(0.3, 0.02, 0.02) }), mb: new THREE.MeshBasicMaterial({ color: new THREE.Color(0.02, 0.05, 0.3) }) };
    r.material = siren.mr; b.material = siren.mb;
  }
  const tailMats = tails.map(m => m);
  const beamGeo = new THREE.PlaneGeometry(4.2, 11); beamGeo.rotateX(-Math.PI / 2);
  const beamMesh = new THREE.Mesh(beamGeo, BEAM_MAT); beamMesh.position.set(0, 0.05, L + 5.6); beamMesh.renderOrder = 2; root.add(beamMesh);
  dynamic.add(root);
  return {
    id: ++carIdSeq, type, spec: t, root, body, wheels, heads, tails: tailMats, siren,
    pos: new THREE.Vector3(), yaw: 0, vel: new THREE.Vector2(0, 0), yawRate: 0, steer: 0, wheelSpin: 0,
    roll: 0, pitch: 0, health: 100, driver: null, ai: null, braking: false, mass: t.mass, radius: Math.max(1.1, t.wid * 0.62),
    throttle: 0, brake: 0, handbrake: false, steerIn: 0, slip: 0, lastHit: 0,
  };
}
function forwardOf(car) { return new THREE.Vector2(Math.sin(car.yaw), Math.cos(car.yaw)); }
function speedOf(car) { return car.vel.length(); }

// arcade vehicle model: inputs throttle [-1..1], brake [0..1], steerIn [-1..1], handbrake
function stepCar(car, dt, isPolice = false) {
  const C = CFG.car;
  const f = forwardOf(car), r = new THREE.Vector2(f.y, -f.x); // right = (cos, -sin)
  let vF = car.vel.dot(f), vR = car.vel.dot(r);
  const maxF = C.maxSpeed * (isPolice ? CFG.wanted.copSpeed * 0.95 : 1) / Math.sqrt(car.mass);
  // longitudinal
  let acc = 0;
  if (car.throttle > 0) acc += (vF < -0.5 ? C.brake : C.accel * (1 - clamp(vF / maxF, 0, 1) * 0.85)) * car.throttle * (isPolice ? 1.15 : 1);
  if (car.throttle < 0) acc += (vF > 0.5 ? -C.brake : -C.reverseAccel * (vF > -C.maxReverse ? 1 : 0)) * -car.throttle;
  if (car.brake > 0) acc -= Math.sign(vF) * Math.min(Math.abs(vF) / dt, C.brake * car.brake);
  acc -= C.drag * vF * Math.abs(vF) + Math.sign(vF) * Math.min(Math.abs(vF) / dt, C.rolling * (car.throttle === 0 ? 1 : 0.2));
  if (car.handbrake) acc -= Math.sign(vF) * Math.min(Math.abs(vF) / dt, 6);
  vF += acc * dt;
  // steering (speed-sensitive)
  const steerTarget = car.steerIn * C.steerMax * lerp(1, 0.3, clamp(Math.abs(vF) / 40, 0, 1));
  car.steer = damp(car.steer, steerTarget, C.steerSpeed * 2, dt);
  const yawRate = (vF / C.wheelbase) * Math.tan(car.steer) * (car.handbrake ? 1.45 : 1);
  car.yawRate = damp(car.yawRate, yawRate, car.handbrake ? 5 : 9, dt);
  car.yaw += car.yawRate * dt;
  // lateral grip -> drift
  const grip = car.handbrake ? C.handbrakeGrip : C.grip * (1 - clamp((Math.abs(vR) - 4) / 14, 0, 0.6));
  car.slip = Math.abs(vR);
  vR = damp(vR, 0, grip, dt);
  const nf = forwardOf(car), nr = new THREE.Vector2(nf.y, -nf.x);
  car.vel.set(nf.x * vF + nr.x * vR, nf.y * vF + nr.y * vR);
  // integrate + collide with buildings (two circles: front & rear)
  car.pos.x += car.vel.x * dt; car.pos.z += car.vel.y * dt;
  let impact = 0;
  for (const off of [car.spec.len * 0.3, -car.spec.len * 0.3]) {
    const p = { x: car.pos.x + nf.x * off, z: car.pos.z + nf.y * off };
    const hit = collideCircle(p, car.radius);
    if (hit) {
      car.pos.x += p.x - (car.pos.x + nf.x * off); car.pos.z += p.z - (car.pos.z + nf.y * off);
      const vn = car.vel.x * hit.x + car.vel.y * hit.z;
      if (vn < 0) { car.vel.x -= vn * 1.35 * hit.x; car.vel.y -= vn * 1.35 * hit.z; car.vel.multiplyScalar(0.8); impact = Math.max(impact, -vn); car.yawRate *= 0.5; }
    }
  }
  if (impact > 3) { car.health -= impact * 1.2; car.lastHit = performance.now(); }
  // visuals
  const ground = CITY.groundHeight(car.pos.x, car.pos.z) > 0.1 ? 0.18 : 0;
  car.pos.y = damp(car.pos.y, ground, 18, dt);
  const latAcc = vF * car.yawRate;
  car.roll = damp(car.roll, clamp(-latAcc * 0.012, -0.09, 0.09), 6, dt);
  car.pitch = damp(car.pitch, clamp(-acc * 0.006, -0.06, 0.06), 6, dt);
  car.wheelSpin += vF * dt / car.spec.wheel;
  car.braking = car.brake > 0.1 || (car.throttle < 0 && vF > 0.5) || car.handbrake;
  syncCar(car);
  return impact;
}
function syncCar(car) {
  car.root.position.copy(car.pos);
  car.root.rotation.set(0, car.yaw, 0);
  car.body.rotation.set(car.pitch, 0, car.roll);
  for (const w of car.wheels) { w.spin.rotation.x = car.wheelSpin; w.pivot.rotation.y = w.front ? car.steer : 0; }
  for (const t of car.tails) t.material = car.braking ? MAT_BRAKE : MAT_TAIL;
}
