# neon-harbor-web — browser twin of Neon Harbor

**Hypothesis**: the Neon Harbor loop (walk → take a car → drive through living traffic
→ jobs → police chase) reads and feels right from procedural geometry alone, at a
quality level that looks like a real open-world game rather than a block demo.

**How to run**: `./build.sh` concatenates `head.html` + `src/*.js` into `index.html`;
open `index.html` in a desktop or mobile browser (loads three.js r128 from cdnjs/jsDelivr).
The file is written as an Artifact body (no `<!doctype>`), so it also publishes as-is.

**Status**: in-progress. Same city seed, balance values and rules as the Unity build
(`Assets/NeonHarbor/`), written so the game can be played and screenshotted where no
Unity editor exists. Per the prototype rules nothing here is imported by the Unity code.
Note: three.js is right-handed and Unity left-handed, so the two builds show mirror
images of the same map.

**Findings so far**
- Extruded side-profile cars with cut wheel arches, an offset glass band and emissive
  lamps read as real cars; box cars did not.
- Night carries the look: emissive windows, neon, additive light pools under street
  lamps and headlight cones on the road, plus bloom.
- The orbit camera needs tree/lamp colliders or it ends up inside foliage.
- Aiming from a third-person camera that looks *at* the player's head puts the crosshair
  on the pavement; on foot the camera now looks past the shoulder along its pitch.
- Officers who leave their cars and fight on foot make 2+ stars feel dangerous; car fires
  with a short fuse before the explosion give the player time to bail out.

Evidence: `production/qa/evidence/neon-harbor-web/`.
