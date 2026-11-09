# Neon Harbor (Unity)

An original open-world crime-driving game set in a procedurally generated harbour city:
walk the streets, get into any car (or pull the driver out), drive with an arcade
drift model through traffic that obeys signals, take jobs, and shake off the police
when your wanted level rises. Everything — city, buildings, cars, people, textures,
sounds — is generated from code; there are no imported assets.

Design: `design/gdd/neon-harbor.md`. Browser twin (same rules, playable without Unity):
`prototypes/neon-harbor-web/`.

## Open and play

1. Install **Unity 6 (6000.0 LTS)** with Unity Hub (2022.3 LTS also works).
2. Unity Hub → **Add → Add project from disk** → select the repository root
   (the folder containing `Assets/`, `Packages/`, `ProjectSettings/`).
3. On first import the editor script switches the project to **Linear** colour space
   and creates `Assets/NeonHarbor/Scenes/Main.unity`. Press **Play** — in that scene or
   in any other: `GameBootstrap` starts the game automatically.

Render pipeline: the **Built-in** pipeline is the default (Standard shader + the
bundled bloom/ACES post shader). If you add **URP**, the code switches to URP/Lit and
a URP post-processing Volume automatically (`NEON_URP` is defined by the asmdef);
assign a URP asset in Project Settings → Graphics as usual.

Input uses the classic Input Manager. If the project is set to the new Input System
only, set **Project Settings → Player → Active Input Handling** to **Both**.

## Controls

| Input | On foot | Driving |
|---|---|---|
| `W A S D` / arrows | walk (camera-relative) | throttle / brake-reverse / steer |
| `Shift` | run | — |
| `Space` | jump | handbrake (drift) |
| `F` / `Enter` | get in the nearest car (steal it if occupied) | get out (below ~15 km/h) |
| Mouse | orbit camera (click to lock the cursor, `Esc` to free it) | same; camera swings behind the car |
| Wheel | camera distance | camera distance |
| `H` | — | horn |
| `T` | skip time of day forward | same |

## What's in the city

- 8×8 blocks: glass-tower downtown with setbacks and aircraft beacons, midtown offices
  and brick, low-rise residential with lawns and trees, three parks with fountains, and
  a container harbour with gantry cranes. Neon signs (Korean/English) light up at night.
- Traffic drives on the right, follows lanes through intersections, stops at red lights
  and behind obstacles, honks at you. Hit it and it becomes a physics wreck.
- Pedestrians walk the sidewalks, flee from danger, and get knocked down by cars.
- Wanted level ★–★★★★★: hitting people, stealing occupied cars, ramming or stealing
  police cars. Police route through the street grid, then ram you. Stay 85 m away from
  every cop for 9 s to lose them; stop next to one for 2.5 s and you're arrested.
- Jobs at the yellow light beams: courier delivery, 7-checkpoint street race, and
  "get 2 stars and escape". Green cash pickups on sidewalks.
- Day/night cycle (12 real minutes per day): procedural sky, golden hour, lit windows,
  street-lamp light pools, headlight cones, bloom.

## Code layout

| Path | Assembly | What |
|---|---|---|
| `Scripts/Core/` | `NeonHarbor.Core` (no engine refs) | City layout + road graph + collision, vehicle model, traffic lights, wanted level, missions, config DTO, RNG |
| `Scripts/Runtime/` | `NeonHarbor.Runtime` | Game root, city mesh builder, procedural textures/materials, car factory, human rig, AI director, player, camera, HUD, audio, post effects, day/night |
| `Scripts/Editor/` | `NeonHarbor.Editor` | First-import setup and the **Neon Harbor** menu |
| `Shaders/` | — | Bloom/tone-map post shader (Built-in), additive glow shader |
| `Resources/Data/harbor_balance.json` | — | Every tuning value (snake_case) |
| `Assets/Tests/EditMode/` | `NeonHarbor.Tests.EditMode` | 28 NUnit tests for the Core |

## Tests

- In Unity: **Window → General → Test Runner → EditMode → Run All**.
- Without Unity: `dotnet test tools/ci/neonharbor` (same test files on .NET 8).

## Screenshot run

Build a player, then e.g.:

```
NeonHarbor.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -harborDrive -harborNight -harborChase -harborShot C:/abs/path/night-chase.png
```

`-harborDrive` starts in the sports car, `-harborNight` jumps to 22:00, `-harborChase`
sets three stars, `-harborShot` captures after 120 frames and quits.
