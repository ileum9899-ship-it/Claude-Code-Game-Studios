# Mini City (Unity)

3D city builder: roads, R/C/I zones that grow into houses, shops, towers and
factories, parks, money, taxes, demand, happiness, traffic and a day/night
cycle. Everything is built from code and primitives — no imported art, no
prefabs, no scene setup needed.

Design: `design/gdd/city-builder.md`.

## Open and play

1. Install **Unity 6 (6000.0 LTS)** via Unity Hub. 2022.3 LTS also works.
2. Unity Hub → **Add → Add project from disk** → select the repository root
   (the folder that contains `Assets/`, `Packages/`, `ProjectSettings/`).
   Unity generates the remaining `ProjectSettings/` and `.meta` files on first open.
3. Press **Play** in any scene (even the empty default one). `GameBootstrap`
   creates the game automatically. Optional: menu **City Game → Create Main
   Scene** saves `Assets/CityGame/Scenes/Main.unity` and adds it to Build Settings.

Render pipeline: the Built-in pipeline (Standard shader) works as-is; URP is
detected and uses `Universal Render Pipeline/Lit`. Input uses the classic Input
Manager — if the project is set to "Input System Package (New)" only, switch
**Project Settings → Player → Active Input Handling** to **Both**.

## Controls

| Input | Action |
|---|---|
| `1`–`6` / toolbar | Road, Residential, Commercial, Industrial, Park, Bulldoze (`X`) |
| Left click / drag | Build (roads paint along the drag, other tools fill a rectangle) |
| `WASD` / arrows, middle-drag | Pan |
| `Q` / `E`, right-drag | Rotate / tilt |
| Wheel, `+` / `-` | Zoom |
| `Space`, `[` `]` | Pause, slower / faster |
| `F5` / `F9` | Save / load (`persistentDataPath/city_save.json`) |
| `H` | Help panel |

## Code layout

| Path | Assembly | What |
|---|---|---|
| `Scripts/Core/` | `CityGame.Core` (no engine refs) | Grid, simulation, config DTO, save data |
| `Scripts/Runtime/` | `CityGame.Runtime` | Controller, renderer, traffic, day/night, camera, IMGUI HUD |
| `Scripts/Editor/` | `CityGame.Editor` | Scene setup menu |
| `Resources/Data/city_balance.json` | — | All balance values (snake_case) |
| `Assets/Tests/EditMode/` | `CityGame.Tests.EditMode` | NUnit tests |

## Tests

- In Unity: **Window → General → Test Runner → EditMode → Run All**.
- Without Unity: `dotnet test tools/ci/citygame` (same test files, plain .NET 8).

## Screenshot run (run-and-observe)

Build a player, then:

```
MiniCity.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -cityDemo -cityShot C:/abs/path/01-city.png
```

`-cityDemo` lays out a sample town and fast-forwards 240 days, `-cityNight`
jumps to night, `-cityShot` captures after 90 frames and quits.
