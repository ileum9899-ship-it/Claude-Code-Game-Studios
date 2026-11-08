# Mini City — City Builder Core Loop

> Status: implemented (Unity: `Assets/CityGame/`, browser port: `prototypes/city-web/`).
> Balance data: `Assets/CityGame/Resources/Data/city_balance.json`.

## 1. Overview

A compact 3D city builder on a 48×48 grid. The player lays roads and paints
residential (R), commercial (C) and industrial (I) zones; buildings grow on
their own when a zone touches a road, there is demand for it, and the
neighbourhood is happy enough. Taxes collected every month pay for more city.

## 2. Player Fantasy

"I'm the mayor watching my town turn into a skyline." Small decisions (one
park here, the factories over there) visibly change what grows, and the city
lights up at night.

## 3. Detailed Rules

- **Tools**: Road, Residential, Commercial, Industrial, Park, Bulldoze. Every
  tool except Bulldoze needs an empty cell; Bulldoze needs an occupied one.
  Each application costs its `cost_*` and fails if funds are short.
- **Road access**: a zone is *served* when one of its 4 orthogonal neighbours
  is a road. Unserved zones never grow and slowly lose levels.
- **Growth** (once per game day, per zone cell, level 0..3): if served, the
  zone's demand > 0 and local happiness ≥ `happiness_for_level[next]`, the
  cell grows one level with chance `growth_chance_per_day × demand`.
- **Decay**: if demand < `decay_demand_threshold`, or local happiness is below
  what the current level needs, the cell loses a level with chance
  `decay_chance_per_day`.
- **Month end** (every `days_per_month` days): tax income is added and upkeep
  for roads and parks is paid.
- **Milestones**: population thresholds give the city a new title
  (마을 → 소도시 → 도시 → 대도시 → 광역시 → 메트로폴리스).
- **Tax rate** 0–20 %; above the default it reduces happiness, below it raises
  happiness.

## 4. Formulas

| Quantity | Formula |
|---|---|
| Population `P` | Σ `residents_per_level[level]` over R cells |
| Jobs `Jc`, `Ji` | Σ `commercial_jobs_per_level` / `industrial_jobs_per_level` |
| R demand | clamp((`base_residential_demand` + (Jc+Ji)·`residents_per_job` − P) / `demand_scale`, −1, 1) |
| C demand | clamp((`base_commercial_demand` + P·`commercial_jobs_per_resident` − Jc) / `demand_scale`, −1, 1) |
| I demand | clamp((`base_industrial_demand` + P·`industrial_jobs_per_resident` − Ji) / `demand_scale`, −1, 1) |
| Tax modifier `m` | (`default_tax_rate` − rate) · `happiness_per_tax_point` |
| Local happiness | clamp(`base_happiness` + min(parks·`park_bonus`, `park_bonus_cap`) − Σ industry level·`pollution_per_industry_level` + m, 0, 100); park/pollution counted within a Chebyshev radius; industry ignores pollution |
| City happiness | resident-weighted mean of R cells (base + m with no residents) |
| Monthly income | round((P·`tax_per_resident` + (Jc+Ji)·`tax_per_job`) · rate / 100) |
| Monthly upkeep | roads·`upkeep_road` + parks·`upkeep_park` |

## 5. Edge Cases

- Bulldozing a road strands neighbouring zones: they decay (2× chance) and
  cars on that road are removed.
- A drag that runs out of money places what it can afford, then shows "자금이 부족합니다".
- Money can go negative from upkeep; building is blocked until it recovers.
- A save from a different grid size is rejected and nothing changes.
- The clock processes at most 16 days per frame, so a frame hitch never stalls the game.

## 6. Dependencies

Simulation (`CityGame.Core`, no engine references) ← Unity runtime
(`CityGame.Runtime`: renderer, traffic, day/night, camera, HUD) ← editor
menu (`CityGame.Editor`). Traffic and day/night are cosmetic only.

## 7. Tuning Knobs

All values in `city_balance.json` (snake_case keys; `CityConfigDto` maps them
onto `CityConfig`; a missing key keeps the default in `CityConfig.cs`).
Most impactful: `growth_chance_per_day` (pace), `demand_scale` (how quickly
demand saturates), `happiness_for_level` (how hard high-rises are), `tax_per_*`.

## 8. Acceptance Criteria

Automated (`Assets/Tests/EditMode/CitySimulationTests.cs`, 17 tests):
placement costs / rejection rules, bulldoze, growth only with road access,
growth gated by the roll, park and pollution happiness, month-end budget,
tax clamping and its happiness effect, jobs creating R demand, save/load round
trip, wrong-size save rejection, milestone event, config DTO defaults.

Visual: city renders with roads, zones, buildings at 3 levels, parks, cars,
day/night lighting and lit windows at night — see
`production/qa/evidence/city-web-prototype/` (browser port; a Unity capture
needs the editor, see `Assets/CityGame/README.md`).
