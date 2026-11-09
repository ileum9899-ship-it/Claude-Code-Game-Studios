// ---------------------------------------------------------------- renderer, sky, world meshes
const renderer = new THREE.WebGLRenderer({ antialias: false, powerPreference: "high-performance" });
renderer.setPixelRatio(Math.min(devicePixelRatio, 1.5));
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
renderer.outputEncoding = THREE.LinearEncoding; // grade pass does tone mapping + gamma
renderer.toneMapping = THREE.NoToneMapping;
document.getElementById("view").appendChild(renderer.domElement);

const scene = new THREE.Scene();
scene.fog = new THREE.FogExp2(0x9fb7cc, 0.0021);
const camera = new THREE.PerspectiveCamera(62, 1, 0.1, 2500);

// --- sky + lights
const sky = new THREE.Sky();
sky.scale.setScalar(2000);
Object.assign(sky.material.uniforms.turbidity, { value: 6 });
sky.material.uniforms.rayleigh.value = 1.6;
sky.material.uniforms.mieCoefficient.value = 0.004;
sky.material.uniforms.mieDirectionalG.value = 0.82;
scene.add(sky);
const stars = (() => {
  const g = new THREE.BufferGeometry(), n = 1500, p = new Float32Array(n * 3);
  for (let k = 0; k < n; k++) { const u = RNG() * TAU, v = Math.acos(RNG() * 0.95), r = 1800; p[k * 3] = Math.sin(v) * Math.cos(u) * r; p[k * 3 + 1] = Math.cos(v) * r; p[k * 3 + 2] = Math.sin(v) * Math.sin(u) * r; }
  g.setAttribute("position", new THREE.BufferAttribute(p, 3));
  const m = new THREE.PointsMaterial({ color: 0xffffff, size: 2.2, sizeAttenuation: false, transparent: true, opacity: 0, fog: false });
  const s = new THREE.Points(g, m); scene.add(s); return s;
})();
const hemi = new THREE.HemisphereLight(0xcfe3ff, 0x4a4036, 0.6);
scene.add(hemi);
const sun = new THREE.DirectionalLight(0xffffff, 3);
sun.castShadow = true;
sun.shadow.mapSize.set(2048, 2048);
Object.assign(sun.shadow.camera, { left: -75, right: 75, top: 75, bottom: -75, near: 1, far: 600 });
sun.shadow.bias = -0.0004; sun.shadow.normalBias = 0.04;
scene.add(sun, sun.target);
const pmrem = new THREE.PMREMGenerator(renderer);
const envScene = new THREE.Scene();
const envSky = new THREE.Sky(); envSky.scale.setScalar(100); envScene.add(envSky);
let envRT = null;

// --- shared materials
const M = {};
function stdMat(o) { return new THREE.MeshStandardMaterial(o); }
for (const [name, f] of Object.entries(TEX.facade)) {
  M["f_" + name] = stdMat({ map: f.map, roughnessMap: f.rough, roughness: 1, metalness: f.metal, emissiveMap: f.emissive, emissive: 0xffffff, emissiveIntensity: 0 });
}
M.roof = stdMat({ map: TEX.roof, roughness: 0.92, color: 0xbbbbbb });
M.slab = stdMat({ map: TEX.concrete, roughness: 0.88 });
M.curb = stdMat({ color: col(0x8e8b84), roughness: 0.9 });
M.grass = stdMat({ map: TEX.grass, roughness: 1 });
M.asphalt = stdMat({ map: TEX.asphalt, roughness: 0.82, metalness: 0.0 });
M.markW = stdMat({ color: col(0xe8e8e2), roughness: 0.6, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 });
M.markY = stdMat({ color: col(0xf2b31a), roughness: 0.6, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 });
M.metal = stdMat({ color: col(0x3b4045), roughness: 0.45, metalness: 0.8 });
M.metalLight = stdMat({ color: col(0x9aa0a6), roughness: 0.4, metalness: 0.7 });
M.lamp = new THREE.MeshBasicMaterial({ color: new THREE.Color(1, 0.85, 0.6) });
M.redBlink = new THREE.MeshBasicMaterial({ color: new THREE.Color(4, 0.2, 0.15) });
M.signalLamp = new THREE.MeshBasicMaterial({ color: new THREE.Color(1, 1, 1) });
M.trunk = stdMat({ color: col(0x5a4330), roughness: 1 });
M.leaf = stdMat({ color: 0xffffff, roughness: 0.95, flatShading: true });
M.water = null; // ShaderMaterial below
M.crane = stdMat({ color: col(0xd9542b), roughness: 0.6, metalness: 0.4 });
M.container = stdMat({ color: 0xffffff, roughness: 0.65, metalness: 0.35 });
M.quay = stdMat({ map: TEX.concrete, roughness: 0.9, color: col(0xb0aaa0) });

// --- geometry builder with world-space UVs
class GeoBuilder {
  constructor() { this.p = []; this.n = []; this.uv = []; this.i = []; }
  quad(a, b, c, d, nrm, uvs) { // a,b,c,d counter-clockwise seen from the normal
    const base = this.p.length / 3;
    for (const v of [a, b, c, d]) this.p.push(v[0], v[1], v[2]);
    for (let k = 0; k < 4; k++) this.n.push(nrm[0], nrm[1], nrm[2]);
    for (const t of uvs) this.uv.push(t[0], t[1]);
    this.i.push(base, base + 1, base + 2, base, base + 2, base + 3);
  }
  // axis-aligned box; side UVs in metres / (us, vs); top UV in metres / ts
  box(x0, y0, z0, x1, y1, z1, us = 1, vs = 1, ts = 1, opts = {}) {
    const U = (a) => a / us, V = (a) => a / vs;
    const w = x1 - x0, d = z1 - z0, vy0 = V(y0), vy1 = V(y1);
    if (!opts.noSides) {
      this.quad([x0, y0, z1], [x1, y0, z1], [x1, y1, z1], [x0, y1, z1], [0, 0, 1], [[U(x0), vy0], [U(x0 + w), vy0], [U(x0 + w), vy1], [U(x0), vy1]]);
      this.quad([x1, y0, z0], [x0, y0, z0], [x0, y1, z0], [x1, y1, z0], [0, 0, -1], [[U(x1), vy0], [U(x1 + w), vy0], [U(x1 + w), vy1], [U(x1), vy1]]);
      this.quad([x1, y0, z1], [x1, y0, z0], [x1, y1, z0], [x1, y1, z1], [1, 0, 0], [[U(z1), vy0], [U(z1 + d), vy0], [U(z1 + d), vy1], [U(z1), vy1]]);
      this.quad([x0, y0, z0], [x0, y0, z1], [x0, y1, z1], [x0, y1, z0], [-1, 0, 0], [[U(z0), vy0], [U(z0 + d), vy0], [U(z0 + d), vy1], [U(z0), vy1]]);
    }
    if (!opts.noTop) this.quad([x0, y1, z1], [x1, y1, z1], [x1, y1, z0], [x0, y1, z0], [0, 1, 0], [[x0 / ts, z1 / ts], [x1 / ts, z1 / ts], [x1 / ts, z0 / ts], [x0 / ts, z0 / ts]]);
  }
  flat(x0, z0, x1, z1, y, ts = 1, rot = false) {
    const uv = rot ? [[z1 / ts, x0 / ts], [z1 / ts, x1 / ts], [z0 / ts, x1 / ts], [z0 / ts, x0 / ts]] : [[x0 / ts, z1 / ts], [x1 / ts, z1 / ts], [x1 / ts, z0 / ts], [x0 / ts, z0 / ts]];
    this.quad([x0, y, z1], [x1, y, z1], [x1, y, z0], [x0, y, z0], [0, 1, 0], uv);
  }
  mesh(material, shadows = true) {
    if (!this.i.length) return new THREE.Group();
    const g = new THREE.BufferGeometry();
    g.setAttribute("position", new THREE.Float32BufferAttribute(this.p, 3));
    g.setAttribute("normal", new THREE.Float32BufferAttribute(this.n, 3));
    g.setAttribute("uv", new THREE.Float32BufferAttribute(this.uv, 2));
    g.setIndex(this.i);
    g.computeBoundingSphere();
    const m = new THREE.Mesh(g, material);
    m.castShadow = shadows; m.receiveShadow = true; m.matrixAutoUpdate = false;
    return m;
  }
}
const world = new THREE.Group(); scene.add(world);
const dynamic = new THREE.Group(); scene.add(dynamic);
// instanced prop collector
class Instancer {
  constructor(geo, mat, cast = true) { this.geo = geo; this.mat = mat; this.m = []; this.c = []; this.cast = cast; }
  add(x, y, z, sx = 1, sy = 1, sz = 1, ry = 0, color) {
    const o = new THREE.Object3D(); o.position.set(x, y, z); o.scale.set(sx, sy, sz); o.rotation.y = ry; o.updateMatrix();
    this.m.push(o.matrix.clone()); this.c.push(color || null); return this.m.length - 1;
  }
  build() {
    const im = new THREE.InstancedMesh(this.geo, this.mat, Math.max(1, this.m.length));
    this.m.forEach((mt, k) => { im.setMatrixAt(k, mt); if (this.c[k]) im.setColorAt(k, this.c[k]); });
    im.count = this.m.length; im.castShadow = this.cast; im.receiveShadow = true;
    if (im.instanceColor) im.instanceColor.needsUpdate = true;
    world.add(im); return im;
  }
}

function buildWorld() {
  const { N, B, R, SW, PITCH, SIZE, HALF_R } = CITY;
  const E0 = -HALF_R, E1 = SIZE + HALF_R, Q = 28; // land extents, quay width on +x/+z
  // asphalt for the whole road network, quay concrete beyond
  const road = new GeoBuilder();
  road.flat(E0, E0, E1, E1, 0, 9);
  world.add(road.mesh(M.asphalt, false));
  const quay = new GeoBuilder();
  quay.box(E1, -4, E0, E1 + Q, 0.18, E1 + Q, 4, 4, 6);
  quay.box(E0, -4, E1, E1, 0.18, E1 + Q, 4, 4, 6);
  quay.box(E0 - 1.5, -4, E0 - 1.5, E0, 0.6, E1 + Q, 4, 4, 6); // sea walls
  quay.box(E0, -4, E0 - 1.5, E1 + Q, 0.6, E0, 4, 4, 6);
  quay.box(E1 + Q, -4, E0 - 1.5, E1 + Q + 1.5, 0.6, E1 + Q + 1.5, 4, 4, 6);
  quay.box(E0 - 1.5, -4, E1 + Q, E1 + Q, 0.6, E1 + Q + 1.5, 4, 4, 6);
  world.add(quay.mesh(M.quay));

  // markings
  const mw = new GeoBuilder(), my = new GeoBuilder();
  const Y = 0.012;
  for (let k = 0; k <= N; k++) for (let s = 0; s < N; s++) {
    const a = s * PITCH + HALF_R, b = (s + 1) * PITCH - HALF_R, c = k * PITCH;
    // road along X at z=c (segment a..b) and along Z at x=c
    my.flat(a + 4, c - 0.22, b - 4, c - 0.08, Y); my.flat(a + 4, c + 0.08, b - 4, c + 0.22, Y);
    my.flat(c - 0.22, a + 4, c - 0.08, b - 4, Y); my.flat(c + 0.08, a + 4, c + 0.22, b - 4, Y);
    for (const side of [-1, 1]) { // edge lines
      mw.flat(a + 4, c + side * (HALF_R - 0.6) - 0.08, b - 4, c + side * (HALF_R - 0.6) + 0.08, Y);
      mw.flat(c + side * (HALF_R - 0.6) - 0.08, a + 4, c + side * (HALF_R - 0.6) + 0.08, b - 4, Y);
    }
    // crosswalks + stop lines at both ends
    for (const [e, dir] of [[a, 1], [b, -1]]) {
      for (let t = -HALF_R + 0.8; t < HALF_R - 0.8; t += 1.2) {
        mw.flat(Math.min(e + dir * 0.5, e + dir * 3.3), c + t, Math.max(e + dir * 0.5, e + dir * 3.3), c + t + 0.6, Y);
        mw.flat(c + t, Math.min(e + dir * 0.5, e + dir * 3.3), c + t + 0.6, Math.max(e + dir * 0.5, e + dir * 3.3), Y);
      }
      // stop line on the incoming (right-hand) lane
      const z0 = dir > 0 ? c - HALF_R + 0.6 : c, z1 = dir > 0 ? c : c + HALF_R - 0.6;
      mw.flat(Math.min(e + dir * 3.8, e + dir * 4.3), dir > 0 ? c - HALF_R + 0.6 : c, Math.max(e + dir * 3.8, e + dir * 4.3), dir > 0 ? c : c + HALF_R - 0.6, Y);
      mw.flat(dir > 0 ? c : c - HALF_R + 0.6, Math.min(e + dir * 3.8, e + dir * 4.3), dir > 0 ? c + HALF_R - 0.6 : c, Math.max(e + dir * 3.8, e + dir * 4.3), Y);
      void z0; void z1;
    }
  }
  world.add(mw.mesh(M.markW, false)); world.add(my.mesh(M.markY, false));

  const lights = new Instancer(new THREE.CylinderGeometry(0.09, 0.13, 7, 8), M.metal);
  const arms = new Instancer(new THREE.BoxGeometry(0.12, 0.12, 2.2), M.metal);
  const heads = new Instancer(new THREE.BoxGeometry(0.5, 0.18, 0.9), M.lamp, false);
  const trunks = new Instancer(new THREE.CylinderGeometry(0.16, 0.24, 3, 6), M.trunk);
  const crowns = new Instancer(new THREE.IcosahedronGeometry(1, 1), M.leaf);
  const benches = new Instancer(new THREE.BoxGeometry(1.8, 0.45, 0.6), M.trunk);
  const bins = new Instancer(new THREE.CylinderGeometry(0.3, 0.3, 0.9, 10), M.metal);
  const acs = new Instancer(new THREE.BoxGeometry(1, 1, 1), M.metalLight);
  const tanks = new Instancer(new THREE.CylinderGeometry(1, 1, 1, 12), stdMat({ color: col(0x6b5a48), roughness: 0.9 }));
  const antennas = new Instancer(new THREE.CylinderGeometry(0.08, 0.12, 1, 6), M.metalLight);
  const blinks = new Instancer(new THREE.SphereGeometry(0.35, 8, 6), M.redBlink, false);
  const containers = new Instancer(new THREE.BoxGeometry(2.44, 2.6, 6.1), M.container);
  const leafCol = () => col(pick([0x3f7a2e, 0x4f8a34, 0x355f27, 0x5d8f3a, 0x6f9440]));
  function tree(x, z, s = 1) {
    CITY.addCollider({ x0: x - 0.3, z0: z - 0.3, x1: x + 0.3, z1: z + 0.3, h: 2.2 }); // trunk: solid for cars, people and the camera
    trunks.add(x, 1.5 * s + 0.18, z, s, s, s);
    crowns.add(x, 3.6 * s + 0.18, z, 1.9 * s, 1.6 * s, 1.9 * s, rand(0, TAU), leafCol());
    crowns.add(x + rand(-0.6, 0.6) * s, 4.7 * s + 0.18, z + rand(-0.6, 0.6) * s, 1.3 * s, 1.2 * s, 1.3 * s, rand(0, TAU), leafCol());
  }

  // blocks
  for (const blk of CITY.blocks) {
    const slab = new GeoBuilder(), curb = new GeoBuilder(), lot = new GeoBuilder(), rf = new GeoBuilder();
    const fac = {};
    slab.box(blk.x0, 0, blk.z0, blk.x1, 0.18, blk.z1, 4, 4, 4, { noSides: true });
    curb.box(blk.x0, 0, blk.z0, blk.x1, 0.18, blk.z1, 4, 4, 4, { noTop: true });
    const ix0 = blk.x0 + SW, iz0 = blk.z0 + SW, ix1 = blk.x1 - SW, iz1 = blk.z1 - SW;
    if (blk.type === "park" || blk.type === "residential") lot.flat(ix0, iz0, ix1, iz1, 0.185, 6);
    for (const b of blk.buildings) {
      const fb = fac[b.style] || (fac[b.style] = new GeoBuilder());
      for (const t of b.tiers) {
        fb.box(t.x0, t.y0 === 0 ? 0.18 : t.y0, t.z0, t.x1, t.y1, t.z1, b.style === "glass" || b.style === "warehouse" ? 16 : 11, b.style === "glass" ? 14 : 12, 1, { noTop: true });
        rf.box(t.x0 - 0.3, t.y1, t.z0 - 0.3, t.x1 + 0.3, t.y1 + 0.9, t.z1 + 0.3, 4, 4, 6); // parapet / roof cap
      }
      const top = b.tiers[b.tiers.length - 1], ty = top.y1 + 0.9;
      const tw = top.x1 - top.x0, td = top.z1 - top.z0;
      if (b.style !== "warehouse") for (let k = 0; k < Math.min(6, (tw * td) / 60); k++) acs.add(rand(top.x0 + 1.5, top.x1 - 1.5), ty + 0.6, rand(top.z0 + 1.5, top.z1 - 1.5), rand(1.2, 2.6), rand(0.8, 1.6), rand(1.2, 2.6));
      if ((b.style === "brick" || b.style === "concrete") && RNG() < 0.55) {
        const tx = rand(top.x0 + 3, top.x1 - 3), tz = rand(top.z0 + 3, top.z1 - 3);
        tanks.add(tx, ty + 3.2, tz, 1.6, 2.6, 1.6);
        for (const [dx, dz] of [[-1, -1], [1, -1], [-1, 1], [1, 1]]) antennas.add(tx + dx, ty + 1, tz + dz, 1.2, 2, 1.2);
      }
      if (top.y1 > 70) {
        const ax = (top.x0 + top.x1) / 2, az = (top.z0 + top.z1) / 2, ah = rand(8, 22);
        antennas.add(ax, ty + ah / 2, az, 2, ah, 2);
        blinks.add(ax, ty + ah + 0.3, az);
      }
    }
    // sidewalk furniture: street lights every ~22 m facing the road, trees between
    const edge = [
      { x0: blk.x0, z0: blk.z0, x1: blk.x1, z1: blk.z0, nx: 0, nz: -1 }, { x0: blk.x0, z0: blk.z1, x1: blk.x1, z1: blk.z1, nx: 0, nz: 1 },
      { x0: blk.x0, z0: blk.z0, x1: blk.x0, z1: blk.z1, nx: -1, nz: 0 }, { x0: blk.x1, z0: blk.z0, x1: blk.x1, z1: blk.z1, nx: 1, nz: 0 },
    ];
    for (const e of edge) {
      const len = Math.hypot(e.x1 - e.x0, e.z1 - e.z0), tx = (e.x1 - e.x0) / len, tz = (e.z1 - e.z0) / len;
      for (let s = 6; s < len - 4; s += 11) {
        const px = e.x0 + tx * s - e.nx * 0.7, pz = e.z0 + tz * s - e.nz * 0.7;
        const k = Math.round((s - 6) / 11);
        if (k % 2 === 0) {
          const ry = Math.atan2(e.nx, e.nz);
          lights.add(px, 3.68, pz); CITY.addCollider({ x0: px - 0.15, z0: pz - 0.15, x1: px + 0.15, z1: pz + 0.15, h: 7 });
          arms.add(px + e.nx * 1, 7.1, pz + e.nz * 1, 1, 1, 1, ry);
          heads.add(px + e.nx * 2, 7.0, pz + e.nz * 2, 1, 1, 1, ry);
          LAMP_POINTS.push([px + e.nx * 2, 7, pz + e.nz * 2]);
        } else if (blk.type !== "harbor" && blk.type !== "downtown") {
          tree(px - e.nx * 0.6, pz - e.nz * 0.6, rand(0.85, 1.15));
        } else if (k % 4 === 1) {
          bins.add(px, 0.63, pz);
        } else {
          benches.add(px - e.nx * 0.8, 0.41, pz - e.nz * 0.8, 1, 1, 1, Math.atan2(e.nx, e.nz) + Math.PI / 2);
        }
      }
    }
    if (blk.type === "park") {
      const pathB = new GeoBuilder();
      pathB.flat(blk.cx - 1.6, iz0, blk.cx + 1.6, iz1, 0.19, 4); pathB.flat(ix0, blk.cz - 1.6, ix1, blk.cz + 1.6, 0.19, 4);
      world.add(pathB.mesh(M.slab, false));
      const fountain = new THREE.Mesh(new THREE.CylinderGeometry(4, 4.4, 0.8, 32), M.slab);
      fountain.position.set(blk.cx, 0.5, blk.cz); fountain.castShadow = true; world.add(fountain);
      const pool = new THREE.Mesh(new THREE.CircleGeometry(3.6, 32), stdMat({ color: col(0x2c6d8a), roughness: 0.05, metalness: 0.3 }));
      pool.rotation.x = -Math.PI / 2; pool.position.set(blk.cx, 0.86, blk.cz); world.add(pool);
      for (let k = 0; k < 26; k++) {
        const x = rand(ix0 + 2, ix1 - 2), z = rand(iz0 + 2, iz1 - 2);
        if (Math.abs(x - blk.cx) < 3.5 || Math.abs(z - blk.cz) < 3.5) continue;
        tree(x, z, rand(0.9, 1.5));
      }
      for (let k = 0; k < 6; k++) benches.add(blk.cx + (k < 3 ? -2.6 : 2.6), 0.41, blk.cz + 8 + k % 3 * 4, 1, 1, 1, Math.PI / 2);
    }
    if (blk.type === "residential") for (let k = 0; k < 6; k++) tree(rand(ix0 + 1, ix1 - 1), rand(iz0 + 1, iz1 - 1), rand(0.8, 1.2));
    if (blk.containers) {
      const palette = [0xb23a2a, 0x2a5fa8, 0xd99a1e, 0x2e7d4f, 0x7b3fa0, 0xc4c4c0, 0x1f8a9a];
      for (const c of blk.containers) for (let s = 0; s < c.stack; s++) containers.add(c.x, 0.18 + 1.3 + s * 2.62, c.z + 3, 1, 1, 1, 0, col(pick(palette)));
    }
    for (const [name, gb] of Object.entries(fac)) world.add(gb.mesh(M["f_" + name]));
    world.add(rf.mesh(M.roof)); world.add(slab.mesh(M.slab, false)); world.add(curb.mesh(M.curb, false));
    const lm = lot.mesh(M.grass, false); if (lm) world.add(lm);
  }

  // harbour: gantry cranes along the quay
  const crane = new GeoBuilder();
  for (let k = 0; k < 4; k++) {
    const x = E1 + 18, z = 40 + k * 70;
    for (const [dx, dz] of [[-5, -6], [5, -6], [-5, 6], [5, 6]]) crane.box(x + dx - 0.5, 0.18, z + dz - 0.5, x + dx + 0.5, 30, z + dz + 0.5, 4, 4, 4);
    crane.box(x - 6, 30, z - 7, x + 6, 33, z + 7, 4, 4, 4);
    crane.box(x - 2, 30, z - 1.5, x + 36, 32.5, z + 1.5, 4, 4, 4); // boom over the water
    crane.box(x - 16, 30.5, z - 1.5, x - 6, 32.5, z + 1.5, 4, 4, 4);
    crane.box(x - 4, 33, z - 3, x + 2, 37, z + 3, 4, 4, 4);
    blinks.add(x + 36, 33, z);
  }
  world.add(crane.mesh(M.crane));
  for (let k = 0; k < 40; k++) containers.add(E1 + rand(6, 14), 0.18 + 1.3 + (k % 3) * 2.62, 30 + Math.floor(k / 3) * 7.5 + rand(-0.3, 0.3), 1, 1, 1, Math.PI / 2, col(pick([0xb23a2a, 0x2a5fa8, 0xd99a1e, 0x2e7d4f, 0x1f8a9a])));

  lights.build(); arms.build(); heads.build(); trunks.build(); crowns.build(); benches.build(); bins.build();
  acs.build(); tanks.build(); antennas.build(); blinks.build(); containers.build();
  world.updateMatrixWorld(true);
}
const LAMP_POINTS = [];
// soft additive light pools on the ground (street lamps, headlights) — cheap stand-in for many real lights
function glowTexture(stops) {
  const c = document.createElement("canvas"); c.width = c.height = 128; const g = c.getContext("2d");
  const grd = g.createRadialGradient(64, 64, 0, 64, 64, 64); stops.forEach(([o, a]) => grd.addColorStop(o, `rgba(255,255,255,${a})`));
  g.fillStyle = grd; g.fillRect(0, 0, 128, 128); return new THREE.CanvasTexture(c);
}
const POOL_MAT = new THREE.MeshBasicMaterial({ map: glowTexture([[0, 0.9], [0.35, 0.45], [1, 0]]), color: new THREE.Color(1, 0.75, 0.45), transparent: true, opacity: 0, blending: THREE.AdditiveBlending, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -4 });
const BEAM_MAT = new THREE.MeshBasicMaterial({ map: (() => { const c = document.createElement("canvas"); c.width = 64; c.height = 128; const g = c.getContext("2d");
  const grd = g.createLinearGradient(0, 0, 0, 128); grd.addColorStop(0, "rgba(255,255,255,0.9)"); grd.addColorStop(1, "rgba(255,255,255,0)"); g.fillStyle = grd;
  g.beginPath(); g.moveTo(20, 0); g.lineTo(44, 0); g.lineTo(64, 128); g.lineTo(0, 128); g.closePath(); g.fill(); return new THREE.CanvasTexture(c); })(),
  color: new THREE.Color(1, 0.95, 0.8), transparent: true, opacity: 0, blending: THREE.AdditiveBlending, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -4 });
function buildLightPools() {
  const g = new THREE.PlaneGeometry(1, 1); g.rotateX(-Math.PI / 2);
  const im = new THREE.InstancedMesh(g, POOL_MAT, LAMP_POINTS.length), o = new THREE.Object3D();
  LAMP_POINTS.forEach((p, k) => { o.position.set(p[0], 0.03, p[2]); o.scale.set(11, 1, 11); o.updateMatrix(); im.setMatrixAt(k, o.matrix); });
  im.renderOrder = 2; world.add(im);
}

// --- ocean
function buildOcean() {
  const mat = new THREE.ShaderMaterial({
    uniforms: { time: { value: 0 }, sunDir: { value: new THREE.Vector3(0, 1, 0) }, sunCol: { value: new THREE.Color(1, 1, 1) },
      skyCol: { value: new THREE.Color(0.5, 0.65, 0.8) }, deepCol: { value: col(0x0b2b3c) }, night: { value: 0 },
      fogColor: { value: new THREE.Color() }, fogDensity: { value: 0.002 } },
    vertexShader: `varying vec3 vW; uniform float time;
      void main(){ vec4 w = modelMatrix * vec4(position,1.0);
        w.y += sin(w.x*0.05+time*0.9)*0.25 + cos(w.z*0.07+time*0.7)*0.2; vW = w.xyz;
        gl_Position = projectionMatrix * viewMatrix * w; }`,
    fragmentShader: `varying vec3 vW; uniform float time; uniform vec3 sunDir, sunCol, skyCol, deepCol, fogColor; uniform float night, fogDensity;
      float h(vec2 p){ return sin(p.x*0.21+time*1.3)*0.5+sin(p.y*0.17-time*1.1)*0.5+sin((p.x+p.y)*0.53+time*2.1)*0.25+sin((p.x-p.y)*0.91-time*2.6)*0.15; }
      void main(){
        vec2 p = vW.xz; float e = 0.4;
        vec3 n = normalize(vec3(h(p+vec2(e,0.))-h(p-vec2(e,0.)), 2.4, h(p+vec2(0.,e))-h(p-vec2(0.,e))));
        vec3 v = normalize(cameraPosition - vW);
        float fres = 0.02 + 0.98*pow(1.0 - max(dot(n,v),0.0), 5.0);
        vec3 r = reflect(-v, n);
        float spec = pow(max(dot(r, normalize(sunDir)),0.0), 280.0) * 6.0;
        vec3 c = mix(deepCol*(1.0-night*0.8), skyCol, fres) + sunCol*spec*step(0.0,sunDir.y);
        float d = length(cameraPosition - vW); float f = 1.0 - exp(-fogDensity*fogDensity*d*d);
        gl_FragColor = vec4(mix(c, fogColor, f), 1.0);
      }`,
  });
  const g = new THREE.PlaneGeometry(4000, 4000, 200, 200); g.rotateX(-Math.PI / 2);
  const m = new THREE.Mesh(g, mat); m.position.set(CITY.SIZE / 2, -1.2, CITY.SIZE / 2); scene.add(m);
  M.water = mat;
  // a distant skyline of islands for the horizon
  const isl = new GeoBuilder();
  const rr = mulberry32(5);
  for (let k = 0; k < 60; k++) {
    const a = rr() * TAU, dist = 900 + rr() * 300, x = CITY.SIZE / 2 + Math.cos(a) * dist, z = CITY.SIZE / 2 + Math.sin(a) * dist, w = 20 + rr() * 40, h = 10 + rr() * 70;
    isl.box(x - w / 2, -2, z - w / 2, x + w / 2, h, z + w / 2, 16, 14, 1);
  }
  const far = isl.mesh(M.f_office, false); scene.add(far);
}

// --- neon signs (canvas text, HDR emissive at night)
const NEON = [];
function buildNeon() {
  const words = [["24시 편의점", "#3cf2ff"], ["HOTEL", "#ff3b7f"], ["치킨", "#ffd23c"], ["NOODLE BAR", "#ff6a2b"], ["카페", "#9dff6a"], ["PHARMACY", "#3cff9e"],
    ["노래방", "#d14bff"], ["BAR", "#ff3b3b"], ["CINEMA", "#ffe66d"], ["약국", "#4bffb2"], ["GARAGE", "#5ab0ff"], ["SUSHI", "#ff5ac8"], ["피시방", "#5affff"], ["DINER", "#ff9a3c"]];
  let n = 0;
  for (const blk of CITY.blocks) {
    if (blk.type !== "midtown" && blk.type !== "downtown") continue;
    for (const b of blk.buildings) {
      if (RNG() < 0.45) continue;
      const [txt, color] = words[n++ % words.length];
      const c = document.createElement("canvas"); c.width = 512; c.height = 128;
      const g = c.getContext("2d");
      g.fillStyle = "#0a0a0f"; g.fillRect(0, 0, 512, 128);
      g.font = `bold ${txt.length > 6 ? 62 : 80}px "IBM Plex Sans KR", "Apple SD Gothic Neo", "Malgun Gothic", sans-serif`;
      g.textAlign = "center"; g.textBaseline = "middle";
      g.shadowColor = color; g.shadowBlur = 18; g.fillStyle = color; g.fillText(txt, 256, 66);
      g.shadowBlur = 0; g.fillStyle = "#ffffff"; g.globalAlpha = 0.6; g.fillText(txt, 256, 66); g.globalAlpha = 1;
      g.strokeStyle = color; g.lineWidth = 6; g.strokeRect(8, 8, 496, 112);
      const t = new THREE.CanvasTexture(c); t.encoding = THREE.sRGBEncoding; t.anisotropy = 4;
      const mat = new THREE.MeshBasicMaterial({ map: t, color: new THREE.Color(1, 1, 1), fog: true });
      const w = Math.min(10, (b.x1 - b.x0) * 0.8), h = w / 4;
      const sign = new THREE.Mesh(new THREE.PlaneGeometry(w, h), mat);
      // face the nearest street
      const faces = [[b.z0 - blk.z0, 0, -1], [blk.z1 - b.z1, 0, 1], [b.x0 - blk.x0, -1, 0], [blk.x1 - b.x1, 1, 0]].sort((p, q) => p[0] - q[0]);
      const [, fx, fz] = faces[0];
      const y = rand(6, 11);
      sign.position.set(fx ? (fx > 0 ? b.x1 + 0.08 : b.x0 - 0.08) : (b.x0 + b.x1) / 2, y, fz ? (fz > 0 ? b.z1 + 0.08 : b.z0 - 0.08) : (b.z0 + b.z1) / 2);
      sign.rotation.y = Math.atan2(fx, fz);
      world.add(sign); NEON.push(mat);
    }
  }
}
