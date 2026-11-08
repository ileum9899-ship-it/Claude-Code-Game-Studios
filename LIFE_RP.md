# Life RP — Unreal Engine 5.8

A 3D open-town **life simulation / role-play** game written in C++ for
**Unreal Engine 5.8**. It uses the current high-end UE5 rendering stack (Lumen GI and reflections,
virtual shadow maps, TSR, Sky Atmosphere, volumetric clouds and fog, MegaLights-ready street lighting)
with a full day/night cycle.

Design record: [`design/game-brief.md`](design/game-brief.md).

## What's in the game

- **A town generated at runtime from a seed:** a 5x5 grid of blocks with roads, lane markings and crosswalks,
  sidewalks with trees and street lights, downtown towers, mid-rise blocks, suburban houses with gable roofs,
  a central park with a fountain, and hills on the horizon.
- **Six buildings you can walk into**, each with things to use: Home (bed, shower, TV, fridge), Sunny Diner, Nova Corp office,
  City Bank (ATMs), Auto Garage and Iron Gym.
- **Five needs and a mood**, timed activities that speed up time (sleep, shower, TV, workouts).
- **Jobs:** Barista, Office Clerk, Mechanic, Night Guard. You clock in during your shift, get paid by the hour based
  on your mood, and get promoted to new titles as you put in hours.
- **Money:** cash and a bank account, rent and interest every midnight, ATMs.
- **28 named citizens** with daily schedules, speech bubbles, and relationships that go from Stranger to Best Friend.
- **Skills:** Fitness (run speed) and Charisma (better conversations).
- **A drivable car** with arcade handling, headlights, body roll and wheels that steer and spin, plus AI traffic that brakes for you.
- **HUD:** clock, money, need bars, job status, interaction prompts, notifications, speedometer, and a TAB status panel.
- **Save/load** (F5/F9), autosave when you wake up, autoload on start.
- **Data-driven:** every number is in **Project Settings > Game > Life RP**.

## Requirements

- Unreal Engine **5.8** (the `.uproject` is associated with 5.8). The code also targets 5.6/5.7-era APIs but is only
  meant for 5.8.
- A C++ toolchain for UE 5.8 (C++20): Visual Studio 2022 17.14+ (2026 recommended) on Windows, or Xcode / clang per Epic's docs.
- For MegaLights (optional): a GPU with hardware ray tracing.

## Build and run

1. Right-click `LifeRP.uproject` and choose **Generate Visual Studio project files**. On Linux and macOS, use `GenerateProjectFiles.sh`.
2. Build the **LifeRPEditor** target in **Development Editor**, or just open the `.uproject` and let it compile.
3. Press **Play**. The default map is the empty engine map `/Engine/Maps/Entry`, and the game mode builds everything.

Command-line build and tests (Linux shown; Windows/macOS equivalents are in
`docs/engine-reference/unreal/current-best-practices.md`):

```bash
Engine/Build/BatchFiles/Linux/Build.sh LifeRPEditor Linux Development -Project="$PWD/LifeRP.uproject"
Engine/Binaries/Linux/UnrealEditor "$PWD/LifeRP.uproject" \
  -ExecCmds="Automation RunTests LifeRP.; Quit" -unattended -nullrhi -stdout -FullStdOutLogOutput
```

## Controls

| On foot | | In a car | |
|---|---|---|---|
| WASD / left stick | move | W / RT | accelerate |
| Mouse / right stick | look | S / LT | brake / reverse |
| Wheel | zoom | A D / left stick | steer |
| Shift | run | Space / B | handbrake |
| Space / A | jump, stop an activity | E / Y | get out |
| E / X | interact, stop an activity | | |
| TAB | status panel | F5 / F9 | save / load |

## Making it look like a shipped game

Everything is built from engine primitive shapes, so the repository contains only text and no binary assets.
Each visual is easy to replace:

- **Characters:** `ALifeCharacterBase` leaves the inherited skeletal `Mesh` empty. Make a Blueprint subclass of
  `LifePlayerCharacter` / `CitizenNPC`, assign a MetaHuman or Manny mesh and an AnimBP, and call
  `SetProceduralBodyVisible(false)`. Then set that Blueprint as the pawn class (`DefaultPawnClass` in a Blueprint of `LifeGameMode`).
- **Town:** subclass `LifeTownBuilder` or set `TownBuilderClass`. You can also place your own level art (Megascans,
  PCG, Nanite meshes) and drop a `LifeStation` actor wherever you want a usable object.
- **Materials:** every mesh uses a dynamic instance of `BasicShapeMaterial`. Swap in Substrate materials per component.
- **Lighting:** `LifeEnvironment` exposes the sun and moon intensities and the post-process; enable MegaLights in
  `Config/DefaultEngine.ini` on RT hardware and turn on `bStreetLightShadows`.

## Code map (`Source/LifeRP`)

| File | Role |
|---|---|
| `LifeRules` | Pure formulas (unit-tested) |
| `LifeSettings` | All tuning (Developer Settings) |
| `LifeGameMode` / `LifeGameState` | Bootstrap, game clock and calendar |
| `LifeEnvironment` | Sky, sun, moon, clouds, fog, post-process, day/night |
| `LifeTownBuilder` | Procedural town, buildings, furniture, walk graph, citizens, traffic |
| `LifeCharacterBase` | Procedural humanoid body and walk cycle |
| `LifePlayerCharacter` | Player: input, activities, driving, social, skills |
| `CitizenNPC` | Townsfolk AI, schedules, dialogue |
| `LifeVehicle` | Player car and traffic AI |
| `LifeStation` | Beds, showers, shops, jobs, ATMs, gym |
| `LifeNeeds/Wallet/Career/InteractionComponent` | Gameplay components |
| `LifePlayerController` | Enhanced Input (built in code), save/load, notifications |
| `LifeHUD` | Canvas HUD |
| `Private/Tests/LifeRulesTests.cpp` | 17 automation tests (`LifeRP.*`) |
