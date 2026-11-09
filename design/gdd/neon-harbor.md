# Neon Harbor — Open-World City Game

> Status: implemented (Unity `Assets/NeonHarbor/`, browser twin `prototypes/neon-harbor-web/`).
> Balance: `Assets/NeonHarbor/Resources/Data/harbor_balance.json`.

## 1. Overview

A third-person open-world game in a procedurally generated 8×8-block harbour city.
The player walks, takes any car, drives an arcade drift model through rule-following
traffic, earns money from street jobs and cash pickups, and is chased by police when
their wanted level rises. Original setting and content; the genre is the open-world
crime-driving sandbox.

## 2. Player Fantasy

"The city is mine for the night." Neon on the streets, a stolen sports car, sirens in
the mirror, and a narrow escape down a long avenue.

## 3. Detailed Rules

**City.** Roads on every grid line (pitch = block + road). Intersections are graph
nodes; blocks are typed by distance from the centre (downtown < 0.38, midtown < 0.75,
else residential) with fixed parks and harbour blocks. Buildings, tree trunks, lamp
posts and crane legs are solid boxes.

**On foot.** Camera-relative walk/run, jump, collision with buildings and cars. A car
faster than 5 m/s knocks the player down (damage = speed × 3.2). Water = knocked out.

**Vehicles.** `F` enters the nearest car within 4.2 m that is under 6 m/s; an occupied
traffic car is stolen (driver flees, +heat). Exit only below 4 m/s. Arcade model:
engine, brake, drag, speed-sensitive steering, lateral grip that breaks into a drift
(handbrake lowers grip). Walls bounce the car (restitution 1.35, 20 % speed loss);
car–car contacts exchange impulse by mass.

**Traffic.** Lane-following on the right, quadratic curves through intersections,
random turn choice (no U-turns), stops for red/yellow before the stop line, follows
with a gap, stops for people on the road, honks at a player blocking it. A struck
traffic car becomes a physics wreck.

**Wanted level.** Heat accumulates; stars = ⌊heat⌋ (max 5). Police cars = stars ×
cops-per-star (max 5), spawned 90–200 m away. They route over the road graph (BFS,
re-planned each second) and switch to direct pursuit within 32 m. Escape: every cop
beyond the evade radius for the evade time → stars cleared. Arrest: a cop within the
bust radius while the player's speed < 2 m/s for the bust time → fine and respawn.

**Combat.** Weapons: fists (18 dmg, 1.7 m, 0.42 s), pistol (34 dmg, semi-auto, 12-round
magazine, 1.3 s reload), SMG (19 dmg, automatic, 30 rounds, 1.8 s reload). Guns are
hitscan from the camera through the crosshair; spread doubles from the hip and grows
×1.6 while running. Holding aim pulls the camera over the shoulder (FOV 46).
Pedestrians have 40 health and officers 60; at zero they fall and fade after 12–14 s.
Armour soaks two thirds of damage. Cars lose 70 % of a bullet's damage from their
health; at zero they burn for 4.5 s, then explode (radius 8 m, linear falloff:
120 to officers, 110 to the player on foot, 70 to cars, knocks people down).
Heat: gunfire within 55 m of police +1 (once per 6 s), killing a pedestrian +1,
hurting an officer +0.6 / downing one +1, shooting a police car +1, an explosion the
player caused +1, punching +0.25 (+0.5 for a knockout, +1 if police are within 30 m).
At 2+ stars, police cars that get close and slow down release two officers who run
in and shoot (every 0.55–1 s) with hit chance `clamp(0.55 − 0.012·distance − 0.012·target_speed, 0.08, 0.55)`
for 7 damage (6 to a car). Downed officers drop ammo.

**Missions.** Three street markers: courier (one drop-off 160–330 m away), street race
(7 checkpoints along the road graph, car required), evade (reach 2 stars, then escape).
Cash pickups respawn elsewhere when collected.

## 4. Formulas

| Quantity | Formula |
|---|---|
| Engine accel | `car_accel · throttle · (1 − 0.85 · clamp(vF / top, 0, 1))` |
| Top speed | `car_max_speed · speed_factor / √mass` |
| Drag | `car_drag · vF · |vF|` + rolling `car_rolling` (coasting) |
| Steer angle | `steer · car_steer_max · lerp(1, 0.3, clamp(|vF| / 40, 0, 1))` |
| Yaw rate | `vF / car_wheelbase · tan(steer_angle)` (×1.45 with handbrake) |
| Lateral grip | `car_grip · (1 − clamp((|vR| − 4) / 14, 0, 0.6))`, or `car_handbrake_grip` |
| Courier time | `time_base + distance · time_per_meter` |
| Courier pay | `lerp(courier_reward_min, courier_reward_max, clamp((d − 160) / 170, 0, 1))`, rounded to $10 |
| Race time | `12 + route_length · 0.062` |
| Arrest fine | `money · bust_fine` |
| Signal | per intersection offset `(7i + 13j) mod 9 · 2` s; x-axis green for `cycle/2 − yellow`, then yellow, then red |

## 5. Edge Cases

- Traffic that reaches the end of a route piece continues on the next piece; the grid
  has no dead ends, so no U-turns are needed.
- Police stuck against a wall for 1.4 s reverse with opposite lock for 1.3 s.
- Stars drop to 0 → chasing police park and despawn beyond 260 m.
- Leaving the map edge is blocked by the quay walls; on foot, the water knocks you out.
- A frame hitch is capped at 50 ms of simulation.

## 6. Dependencies

`NeonHarbor.Core` (layout, vehicle model, lights, wanted, missions) has no engine
references. `NeonHarbor.Runtime` builds meshes, runs AI and presentation on top.

## 7. Tuning Knobs

All keys in `harbor_balance.json` (snake_case, mapped by `HarborConfigDto`). Most
impactful: `car_grip` / `car_handbrake_grip` (drift feel), `car_max_speed`,
`traffic_count` / `ped_count` (density vs. performance), `evade_radius` /
`evade_seconds` (chase difficulty), `cop_speed`, `day_length`.

## 8. Acceptance Criteria

Automated (`Assets/Tests/EditMode/`, 40 tests): deterministic city, every district
present, no building on a road, downtown taller than residential, shortest grid path,
road/ground queries, circle push-out, camera raycast, sidewalk ring; car accelerates
below top speed, brakes to stop, steering sign, handbrake drift, reverse cap, wall
bounce, mass vs. top speed; heat → stars and cap, evade, evade reset, arrest only when
stopped, cop count, signals never green both ways, courier/race/timeout/evade flow,
config defaults, RNG parity with the browser build; fists-only start, semi-auto vs
automatic, fire rate, auto-reload, empty click, ammo pickups, armour soak, ray–cylinder
and rotated ray–box hits, officer accuracy, explosion falloff, heat never negative.

Visual: city by day and night, driving, police chase, phone layout — browser build
captures in `production/qa/evidence/neon-harbor-web/`. A Unity capture needs the
editor (see `Assets/NeonHarbor/README.md` § Screenshot run).
