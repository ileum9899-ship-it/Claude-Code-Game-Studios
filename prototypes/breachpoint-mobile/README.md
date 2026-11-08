# Breachpoint Mobile — prototype

An original 5v5 tactical hero shooter in the attack/defend, plant-the-device style,
built **for phones only** (landscape, touch controls). It runs as a single HTML file
using three.js (vendored as `three.module.min.js`, MIT). All names, characters,
weapons, abilities and the map are original to this prototype.

## Hypothesis

A round-based tactical shooter (buy phase → push/hold a site → plant/defuse the core)
can feel good on a phone with: a floating left stick, drag-to-look, a fire button
you can drag to aim while firing, and slower, more readable time-to-kill than on PC.

## How to run

```bash
cd prototypes/breachpoint-mobile
python3 -m http.server 8000
# open http://<your-PC-IP>:8000 on a phone on the same network, in landscape
```

ES modules do not load over `file://`, so you need a local server.
For desktop testing there is a fallback: WASD, mouse (click locks the pointer),
right-click to aim, R reload, Q/E/X abilities, F plant/defuse (hold), B shop.
`?ts=6` speeds up time (test-only; used for automated full-match runs).

## What is in it

- **Mode**: 5v5 (you + 4 ally bots vs 5 enemy bots); first to 5 rounds wins; sides swap after round 4.
- **Round flow**: 12s buy phase (spawn barrier) → 95s action → plant the core (3.5s) → 40s fuse → defuse (6s).
- **Economy**: start ¤800; win +3000, loss +1900 (+500 loss streak, max 2 steps), kill +200, plant +300.
- **Weapons**: P-9 Pip (sidearm), Hornet (SMG), Mauler (shotgun), Kestrel (rifle; one-shot headshot), Heron (sniper). Light/heavy shields.
- **Agents**:
  - VEIL: Flash / Smoke / Blackout
  - RIFT: Dash / Flash / Overdrive
  - ROOT: Heal / Recon / Fortify
  - Ultimates charge from kills, rounds and plants (6 points).
- **Bot AI**: grid BFS pathing, field-of-view + line-of-sight perception (smoke blocks it), reaction delay, aim error, burst fire, reacting to sound and damage, a site push or hold plan, picking up a dropped core, post-plant positions and defusing. Three difficulty levels.
- **HUD**: minimap (shows enemies your team has spotted), kill feed, round timer, hit markers (red for headshots), flash and smoke overlays, spectating a teammate after you die.

## Status

**In progress.** Verified in headless Chromium with mobile emulation (iPhone 13 / Pixel 7 landscape):
no runtime errors, and a full match runs to completion (side swap, plant, round wins, final scoreboard).
Screenshots are in `evidence/`.

## Findings so far / next questions

- Hasn't been tested on a real touch device yet. That's the main open risk: sensitivity, button placement, thumb reach.
- Bots never use abilities; enemies don't play around the player's utility.
- No audio assets (synthesized sound only), no crouch/jump, and walls are treated as infinitely tall for bullets.
- If this moves to production: choose an engine with `/setup-engine` and rewrite to production standards. Don't extend this code.
