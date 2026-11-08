# Life RP — Game Brief

> Design record for the Life RP prototype (Unreal Engine 5.8, C++). Code:
> `Source/LifeRP/`. Every number below is a default in `ULifeSettings`
> (`Source/LifeRP/Private/LifeSettings.cpp`) and is editable in
> **Project Settings > Game > Life RP**.

## 1. Overview

A third-person 3D life simulation with role-play. You are a new resident of a
small modern town. You keep five needs up, earn money at a job, pay rent, build
relationships with the townsfolk, train skills, and drive around a living town
with a full day/night cycle. The town, its buildings, citizens and traffic are
generated at runtime from a seed, so the project runs on an empty level.

## 2. Player Fantasy

"I have my own life in this town." Small daily routines (coffee, work, gym,
a chat in the park, getting home before you collapse) add up to a day you
planned and a life that improves: a promotion, a fuller bank account, a best
friend.

## 3. Detailed Rules

### Time
- 1 real second = 1 game minute (24-minute day). The game starts at 08:00, Day 1.
- Sleep fast-forwards x60; other activities x10-12. Rent and interest settle at midnight.
- Sun and moon follow the clock; street lights and car headlights switch on at night (19:30-06:30).

### Needs
- Hunger, Energy, Hygiene, Fun, Social: each 0-100, decaying every game hour.
- Mood combines all five (see Formulas). A need at 15 or less warns once.
- Hunger at 0 doubles Energy decay. Hygiene under 15 speeds Social decay and costs relationship.
- Energy at 0 on foot: you collapse, pay a medical fee and sleep on the spot.
- Sprinting multiplies Energy decay x2.5; below 20 Energy you move slower.

### Places and stations (press **E**)
| Building | Stations |
|---|---|
| Home | Bed (sleep until 07:00 or until rested), Shower, TV, Fridge (cook, $6) |
| Sunny Diner | Coffee, Burger Meal, Fresh Salad, **Barista** job |
| Nova Corp office | **Office Clerk** job, Energy Drink vending |
| City Bank | ATM deposit-all, ATM withdraw $100, **Night Guard** job |
| Auto Garage | **Mechanic** job |
| Iron Gym | 3 workout machines (Fitness skill), Protein Shake |

### Jobs
- Use a workstation to get hired (replaces your current job). Use it during your shift to clock in.
- While clocked in and within 6 m of the station you earn pay every game minute; extra need drain applies.
- Leaving, the shift ending, starting an activity or entering a car clocks you out. Pay goes to the bank.
- Every 16 hours worked is a promotion (max level 5) with a new title and +15% wage.

| Job | Wage/h | Shift |
|---|---|---|
| Barista | $16 | 07-15 |
| Office Clerk | $22 | 09-17 |
| Mechanic | $20 | 12-20 |
| Night Guard | $19 | 22-06 |

### Money
- Start: $120 cash, $600 bank. Purchases use cash first, then the bank card.
- Midnight: rent $45 is charged and 0.2% interest is paid on a positive balance. The bank can go negative.

### Social
- 28 named citizens walk the sidewalks and cross at crosswalks, go home late evening and come out in the morning.
- Talking gives Social (+ a little Fun). Relationship +6 at most once per game hour per person (-3 if you are filthy) and Charisma +0.1.
- Labels: Rival < -20 <= Stranger < 20 <= Acquaintance < 50 <= Friend < 80 <= Best Friend.

### Driving
- Your car is parked on the street outside home. **E** to enter or exit (below ~12 km/h).
- Arcade handling: W/S throttle/brake/reverse, A/D steer, Space handbrake, top speed ~100 km/h.
- Traffic loops the lanes around blocks, slows for corners and stops for people and cars ahead.

### Save
- F5 saves, F9 loads, autosave on waking. The save is loaded automatically on start.
- Saved: day/time, position, needs, money, job and hours, skills, relationships.

## 4. Formulas

All in `Source/LifeRP/Private/LifeRules.cpp`, tested in `Source/LifeRP/Private/Tests/LifeRulesTests.cpp`.

| Rule | Formula |
|---|---|
| Need decay | `need' = clamp(need - rate * hours, 0, 100)` |
| Effective rate | Energy rate x2 if Hunger = 0; Social rate x1.5 if Hygiene < 15 |
| Mood | `0.6 * average(needs) + 0.4 * min(needs)` |
| Performance | mood 0 -> 0.5, 60 -> 1.0, 100 -> 1.25 (piecewise linear) |
| Job level | `clamp(1 + floor(hours / HoursPerLevel), 1, MaxLevel)` |
| Shift pay | `wage * (1 + growth * (level - 1)) * hours * performance` |
| Social gain | `base * (1 + rel / 200) * (1 + charisma / 20)` |
| Interest | `floor(bank * rate)`, 0 when bank <= 0 |
| Speed | x1 above 20 Energy, lerps to x0.65 at 0; run speed x(1 + 0.03 * Fitness) |
| Sun elevation | `MaxElevation * sin((hour - 6) / 24 * 2pi)` |

**Rationale for the defaults:** a day's decay (Hunger 5.5/h -> 132/day) needs about
two meals; Energy 4/h is covered by one 7-8 h sleep at 14/h; a full 8 h Office shift at mood 60 pays
$176 against $45 rent and ~$30 food, so a working player saves ~$100/day and an idle one goes into
debt in about two weeks.

## 5. Edge Cases

- Sleeping when not tired (Energy > 80 between 08:00 and 20:00) is refused.
- Activities stop on E or Space; time returns to x1.
- Collapsing while driving is not possible (the check is skipped); you collapse after getting out.
- Exiting a car looks for a free spot on the left, then the right, then above the car.
- Citizens that get stuck reverse their route; traffic cars ignore crossing traffic so loops never gridlock.
- Night shifts wrap midnight (22-06 counts 23:00 and 02:00 as in-shift).

## 6. Dependencies

Engine plugins: Enhanced Input. Engine content only (`/Engine/BasicShapes`,
`/Engine/EngineSky`). No marketplace assets, no binary assets in the repository.

## 7. Tuning Knobs

Project Settings > Game > Life RP: time scale, wake hour, sun height, starting
needs, decay rates, activity rates and durations, prices, jobs table, shop items
table, rent/interest, town seed and size, citizen count and names, small-talk
lines, traffic count and car handling, save slot and autosave/autoload.

## 8. Acceptance Criteria

1. Press Play on any empty level: a lit town with sky, clouds, citizens and traffic appears and the player stands outside HOME.
2. Over 24 real minutes the sun sets, street lights and headlights switch on, the moon rises, and the sun rises again.
3. Buying a Burger at the diner costs $12 and raises Hunger by 55 (capped at 100).
4. Getting hired as Barista, clocking in at 07:00-15:00 and staying 1 game hour at mood 60 pays about $16 to the bank on clock-out.
5. Sleeping in the home bed at 23:00 wakes you at 07:00 (or earlier once Energy is 100) and autosaves.
6. Talking to a citizen raises Social and shows their relationship in the prompt and on the TAB panel.
7. Entering the car, driving and exiting works; traffic stops when you stand in front of it.
8. F5 then F9 restores money, needs, job, time and position.
9. `Automation RunTests LifeRP.` passes all tests.
