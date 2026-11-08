# Crystal Cavern Simulator — Game Brief

An original Roblox simulator. You mine glowing crystals in an underground cavern
to earn Gems, spend them on tools that mine faster and drills that mine for you,
rebirth for a permanent multiplier, and hatch pets that add more.

## Core loop

1. **Mine**: click a crystal in the world, press the MINE button, or press E / gamepad X.
   Each mine pays `perMine` Gems.
2. **Upgrade**:
   - *Pickaxe* adds Gems per mine.
   - *Drill* adds passive Gems per second. Drills also pay while you're offline, at reduced efficiency.
   - *Buy* buys one level. *Max* buys as many as you can afford.
3. **Hatch**: spend Gems on eggs. The best 3 pets equip automatically and add to the multiplier.
   You can check each egg's odds by pressing **Details** before you buy.
4. **Rebirth**: once you hold the requirement, reset Gems and upgrades (pets are kept)
   and get +0.5x on everything.

## Formulas

All implemented in `src/shared/SimRules.luau` and `src/shared/EconomyMath.luau`.

| Quantity | Formula |
|---|---|
| Multiplier | `(1 + rebirths × 0.5) × (1 + Σ bonus of best 3 pets)` |
| Gems per mine | `max(1, floor((baseClick + Pickaxe × 1) × multiplier))` |
| Gems per second | `(baseIncome + Drill × 2) × multiplier` (fractions carried between ticks) |
| Next level price | `floor(baseCost × costGrowth ^ owned)` |
| Rebirth requirement | `floor(50 000 × 1.8 ^ rebirths)` |
| Offline Gems | `floor(min(away, 8 h) × perSecond × 0.5)`, server clock only |

## Tuning knobs (`src/shared/Config.luau`)

| Knob | Value | Why |
|---|---|---|
| Pickaxe `baseCost`/`costGrowth` | 15 / 1.15 | The first levels take seconds, and the price doubles about every 5 levels. |
| Drill `baseCost`/`costGrowth` | 50 / 1.12 | Idle income is more expensive up front but grows a bit more slowly than Pickaxe prices. |
| Rebirth base / growth / per-rebirth | 50 000 / 1.8 / 0.5 | First rebirth after ~5–10 min. Each rebirth takes about ×1.2–1.6 longer than the last (roblox-simulator-economy pacing). |
| Offline cap / efficiency | 8 h / 0.5 | Playing stays worth more than idling. |
| Eggs | Stone 500, Geode 25 000 | Stone is affordable in the first minutes. Geode is a mid-run goal. |
| Equipped / inventory | 3 / 50 | Keeps the pet multiplier bounded. |
| Rate limits | mine 12 burst @10/s; actions 6 @3/s | Faster than any human clicks. Stops auto-clickers and remote spam. |

## Edge cases handled

- Remotes carry only ids. The server prices everything and never yields between checking the balance and spending it.
- When a save is loaded, tampered or old data is repaired:
  - negative, NaN or fractional amounts are fixed,
  - levels are clamped to the max,
  - unknown upgrades and pets are dropped,
  - pet counts are capped at the inventory size.
- A future or missing `lastSeen` pays no offline income.
- **Session lock**: a second server can't load a save while another holds a live lock. A server that lost its lock never overwrites the save.
- **Failed load**:
  - In a live server, the player is kicked rather than overwriting their real data with an empty save.
  - In Studio without API access, the game runs on unsaved data and prints a warning.
- Players who leave while their save is still loading give the lock back.
- On server shutdown, everyone's save runs in parallel via `BindToClose`.

## Architecture

```text
default.project.json            Rojo tree: Remotes, cavern floor, spawn, lighting
src/shared/  → ReplicatedStorage.Shared
  Config.luau                   every gameplay number
  EconomyMath.luau              verbatim from awesome-gamedev-agent-skills
                                (roblox-simulator-economy, Apache-2.0)
  SimRules.luau                 pure rules; config + maths injected
src/server/  → ServerScriptService.Server
  Main.server.luau              sessions, remotes, income tick, autosave
  PlayerData.luau               DataStore load/save with session lock (store injected)
  RateLimiter.luau              per-player token bucket (clock injected)
  World.luau                    places the tagged crystal ring
src/client/  → StarterPlayerScripts.Client
  Hud.client.luau               HUD: stats, mine button, shop, rebirth, eggs + odds, pets
tests/unit/
  sim_rules/sim_rules_test.luau
  player_data/player_data_test.luau
```

## Build and run

Requires [Rojo](https://rojo.space) 7.4+ and, for the tests, [Lune](https://lune-org.github.io/docs).

```sh
rojo build -o CrystalCavern.rbxl      # open the file in Roblox Studio and press Play
# or live-sync: rojo serve, then connect from the Rojo Studio plugin

lune run tests/unit/sim_rules/sim_rules_test.luau
lune run tests/unit/player_data/player_data_test.luau
```

To test saving in Studio:

1. Publish the place.
2. Enable *Game Settings → Security → Enable Studio Access to API Services*.
   Use a separate test place, because Studio writes to the same live data that players use.

## Playing on a phone

Roblox Studio only runs on Windows or Mac, so the build and publish steps need a computer once.

1. On the computer, run `rojo build -o CrystalCavern.rbxl` and open the file in Studio.
2. **File → Publish to Roblox** creates a new experience.
3. On the [Creator Dashboard](https://create.roblox.com/dashboard/creations), open the experience. In its settings:
   - set **Playable Devices** to include *Phone* and *Tablet*;
   - set access to *Public*, or *Private* if you only want to test it yourself.
4. On the phone, sign in to the Roblox app with the same account. Open **Profile → Creations**, or search for the experience name.

Before publishing, you can preview the layout in Studio: **Test → Device** emulates a phone or tablet.

Touch layout (`Hud.client.luau` `adapt()`):

- the HUD scales down on small screens and stays inside the safe area (`CoreUISafeInsets`);
- MINE is a large button at the bottom centre, between the thumbstick and the jump button;
- the shop is closed by default and opens from **🛒 Shop** in the top-right as a centred panel with a Close button;
- you can also mine by tapping a crystal.

## Acceptance criteria

- [ ] Clicking a crystal or MINE increases Gems by `perMine`. Spamming past 12 in a burst is ignored.
- [ ] Buy / Max charge the shown price and raise the level. They do nothing when you can't afford them, and are disabled at max level.
- [ ] Drills pay every second. Leaving and rejoining pays capped offline Gems and shows a toast.
- [ ] Rebirth is only possible at the requirement. It resets Gems and levels, keeps pets, and raises the multiplier.
- [ ] Eggs show odds behind *Details* that add up to 100%. Hatching charges the price and shows a toast. The best 3 pets show as equipped.
- [ ] Leaving and rejoining keeps Gems, levels, pets and rebirths, and **Server & Clients** with 2 players shows no errors in Output.
- [ ] Both Lune test files pass.

## Verification status

The code was written but **not executed** in the authoring session. The sandbox didn't
allow running the downloaded Rojo/Lune binaries, so these are still to do:

- the Lune unit tests,
- a `rojo build`,
- a Studio playtest.

None of the acceptance criteria above have been observed yet.
