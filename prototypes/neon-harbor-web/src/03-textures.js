// ---------------------------------------------------------------- procedural textures (canvas)
const TEX = (() => {
  const r = mulberry32(99);
  function canvas(w, h) { const c = document.createElement("canvas"); c.width = w; c.height = h; return c; }
  function tex(c, repeat = true, srgb = true) {
    const t = new THREE.CanvasTexture(c);
    if (repeat) t.wrapS = t.wrapT = THREE.RepeatWrapping;
    t.anisotropy = 8;
    if (srgb) t.encoding = THREE.sRGBEncoding;
    return t;
  }
  function noise(g, w, h, amount, alpha = 0.08) {
    for (let k = 0; k < amount; k++) {
      const v = Math.floor(r() * 255);
      g.fillStyle = `rgba(${v},${v},${v},${alpha * r()})`;
      g.fillRect(r() * w, r() * h, 1 + r() * 2, 1 + r() * 2);
    }
  }
  // Facade: one texture tile = 4 window columns x 4 floors (u: 4 m per column, v: 3.5 m per floor)
  const FACADES = {
    glass:    { wall: "#3d4f63", frame: "#8fa3b5", win: ["#1d2b3a", "#24384d", "#2b4560"], rough: 0.12, metal: 0.65, mullion: 3, lit: 0.42, litCol: ["#ffe2a8", "#fff3d6", "#cfe7ff"] },
    office:   { wall: "#9aa1a6", frame: "#5b6268", win: ["#22303d", "#2b3947", "#1c2733"], rough: 0.45, metal: 0.25, mullion: 6, lit: 0.36, litCol: ["#ffe2a8", "#fff6e0"] },
    brick:    { wall: "#8a4a36", frame: "#d8cfc0", win: ["#1f2a33", "#26323c"], rough: 0.9, metal: 0, mullion: 10, lit: 0.3, litCol: ["#ffcf7a", "#ffd99a"], brick: true },
    concrete: { wall: "#b3ada3", frame: "#6f6a62", win: ["#232c33", "#2c3640"], rough: 0.85, metal: 0, mullion: 8, lit: 0.3, litCol: ["#ffd88f", "#fff0cc"] },
    plaster:  { wall: "#d9c9a8", frame: "#f2ece0", win: ["#26323b", "#2f3d48"], rough: 0.9, metal: 0, mullion: 12, lit: 0.34, litCol: ["#ffc874", "#ffdca0"] },
    warehouse:{ wall: "#6f7b80", frame: "#4c5559", win: ["#2a3135"], rough: 0.7, metal: 0.4, mullion: 30, lit: 0.15, litCol: ["#ffe7b0"], ribbed: true },
  };
  const facade = {};
  for (const [name, f] of Object.entries(FACADES)) {
    const W = 512, H = 512, cw = W / 4, fh = H / 4;
    const c = canvas(W, H), g = c.getContext("2d");
    const e = canvas(W, H), ge = e.getContext("2d");
    const rough = canvas(W, H), gr = rough.getContext("2d");
    g.fillStyle = f.wall; g.fillRect(0, 0, W, H);
    ge.fillStyle = "#000"; ge.fillRect(0, 0, W, H);
    gr.fillStyle = `rgb(${f.rough * 255},0,0)`; gr.fillRect(0, 0, W, H);
    if (f.brick) {
      for (let y = 0; y < H; y += 8) for (let x = (y / 8) % 2 ? -8 : 0; x < W; x += 16) {
        const s = 0.85 + r() * 0.3; g.fillStyle = `rgb(${138 * s | 0},${74 * s | 0},${54 * s | 0})`; g.fillRect(x + 1, y + 1, 14, 6);
      }
    }
    if (f.ribbed) for (let x = 0; x < W; x += 8) { g.fillStyle = "rgba(0,0,0,0.18)"; g.fillRect(x, 0, 3, H); }
    noise(g, W, H, 9000, 0.1);
    for (let fy = 0; fy < 4; fy++) for (let cx = 0; cx < 4; cx++) {
      const m = f.mullion, x = cx * cw + m, y = fy * fh + m + 4, w = cw - m * 2, h = fh - m * 2 - (name === "glass" ? 0 : 10);
      if (f.ribbed && fy !== 1) continue;
      g.fillStyle = f.frame; g.fillRect(x - 3, y - 3, w + 6, h + 6);
      const wc = f.win[Math.floor(r() * f.win.length)];
      const grd = g.createLinearGradient(x, y, x + w, y + h);
      grd.addColorStop(0, wc); grd.addColorStop(1, "#0d141b"); g.fillStyle = grd; g.fillRect(x, y, w, h);
      g.fillStyle = "rgba(255,255,255,0.08)"; g.fillRect(x, y, w * 0.45, h);  // sky reflection streak
      gr.fillStyle = "rgb(20,0,0)"; gr.fillRect(x, y, w, h);
      if (r() < f.lit) {
        const lc = f.litCol[Math.floor(r() * f.litCol.length)];
        ge.fillStyle = lc; ge.globalAlpha = 0.35 + r() * 0.5; ge.fillRect(x, y, w, h); ge.globalAlpha = 1;
        if (r() < 0.5) { ge.fillStyle = "rgba(0,0,0,0.55)"; ge.fillRect(x + w * r() * 0.6, y, w * 0.3, h); } // curtain
      }
      if (name !== "glass") { g.fillStyle = "rgba(0,0,0,0.25)"; g.fillRect(x - 3, y + h + 3, w + 6, 4); } // sill shadow
    }
    if (name === "glass") { // horizontal spandrel bands
      for (let fy = 0; fy < 4; fy++) { g.fillStyle = "#56687a"; g.fillRect(0, fy * fh, W, 6); }
    }
    facade[name] = { map: tex(c), emissive: tex(e), rough: tex(rough, true, false), metal: f.metal, roughness: f.rough };
  }
  // asphalt
  const asphalt = (() => {
    const c = canvas(512, 512), g = c.getContext("2d");
    g.fillStyle = "#3a3c3f"; g.fillRect(0, 0, 512, 512);
    noise(g, 512, 512, 60000, 0.22);
    for (let k = 0; k < 14; k++) { // tar patches / cracks
      g.strokeStyle = "rgba(15,15,15,0.35)"; g.lineWidth = 1 + r() * 2; g.beginPath();
      let x = r() * 512, y = r() * 512; g.moveTo(x, y);
      for (let s = 0; s < 6; s++) { x += (r() - 0.5) * 60; y += (r() - 0.5) * 60; g.lineTo(x, y); } g.stroke();
    }
    return tex(c);
  })();
  const concrete = (() => {
    const c = canvas(256, 256), g = c.getContext("2d");
    g.fillStyle = "#a7a49d"; g.fillRect(0, 0, 256, 256); noise(g, 256, 256, 12000, 0.12);
    g.strokeStyle = "rgba(60,60,55,0.35)"; g.lineWidth = 2;
    for (let k = 0; k <= 256; k += 64) { g.beginPath(); g.moveTo(k, 0); g.lineTo(k, 256); g.stroke(); g.beginPath(); g.moveTo(0, k); g.lineTo(256, k); g.stroke(); }
    return tex(c);
  })();
  const grass = (() => {
    const c = canvas(256, 256), g = c.getContext("2d");
    g.fillStyle = "#4d7a35"; g.fillRect(0, 0, 256, 256);
    for (let k = 0; k < 9000; k++) { const s = 0.7 + r() * 0.6; g.fillStyle = `rgba(${70 * s | 0},${125 * s | 0},${50 * s | 0},0.7)`; g.fillRect(r() * 256, r() * 256, 1, 2 + r() * 3); }
    return tex(c);
  })();
  const roof = (() => {
    const c = canvas(256, 256), g = c.getContext("2d");
    g.fillStyle = "#4a4c4f"; g.fillRect(0, 0, 256, 256); noise(g, 256, 256, 14000, 0.2);
    return tex(c);
  })();
  return { facade, FACADES, asphalt, concrete, grass, roof };
})();
