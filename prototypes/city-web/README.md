# city-web — browser port of Mini City

**Hypothesis**: the Mini City core loop (roads → zones → demand-driven growth,
happiness from parks vs. factories, monthly taxes) is readable and fun enough
in 3D to play without a tutorial.

**How to run**: open `index.html` in a browser (needs internet for three.js
r128 from cdnjs and Google Fonts). No build step. The file is written as an
Artifact body (no `<!doctype>`), so it also publishes as-is.

**Status**: in-progress. It is a straight JS port of
`Assets/CityGame/Scripts/Core/CitySimulation.cs` with the same balance values,
made so the game can be played and screenshotted where no Unity editor is
installed. The Unity project is the real game; per the prototype rules this
code is never migrated into it.

**Findings so far**
- The demo town reaches ~500 residents ("도시") in 200 days; R/C/I demand settles
  near zero, so the growth loop balances itself instead of running away.
- Blocks of 5×5 between roads leave the inner 3×3 without road access; the
  demo fills those with parks, which also keeps happiness ≥ 65 so towers grow.
  Players will likely build denser road grids — watch for that in playtests.
- Night lighting (emissive window bands + street lamps) carries the "skyline"
  fantasy better than any daytime detail.

Evidence: `production/qa/evidence/city-web-prototype/`.
