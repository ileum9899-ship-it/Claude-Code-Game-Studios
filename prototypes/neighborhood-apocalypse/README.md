# 우리동네 아포칼립스 (Neighborhood Apocalypse) — HTML prototype

## Hypothesis

A survival game is more compelling when it takes place in **the player's own real
neighborhood**: search a real place on a map, the real buildings around it are
rebuilt in 3D, and the player survives 30 days by making choices (card-style
decisions) and scavenging those familiar buildings.

## How to run

Open `index.html` in a modern browser (Chrome/Edge/Safari/Firefox). No build step and no API key.
It needs internet access for:

| Purpose | Service |
|---|---|
| Map tiles | `tile.openstreetmap.org` |
| Place search | Nominatim (`nominatim.openstreetmap.org`) |
| Building footprints, heights, roads, shops | Overpass API (`overpass-api.de`, with mirrors as fallback) |
| Libraries | three.js r128, Leaflet 1.9.4 via cdnjs |

If the building server can't be reached, or the spot has almost no mapped buildings,
the game **generates a procedural neighborhood** so it stays playable. You can also
pick **가상 동네로 시작** to start in one directly.

## How it plays

1. **Pick the map**: search for a place (or click the map, or use 📍), choose a radius (180–350 m) and start.
2. **Build the town**: OSM building footprints are extruded to their real height
   (`height` / `building:levels`, or an estimate from the building type). The type comes
   from the OSM tags plus any shop/amenity nodes inside the footprint: convenience store,
   mart, pharmacy, hospital, police, school, hardware store, and so on. Each type has its own loot table and danger level.
3. **Roadview (🚶 로드뷰)**: a first-person street-level view of the generated buildings
   (drag to look, WASD/arrow keys or the on-screen pad to walk, walls block movement). Click a building in any view to explore it.
4. **Choose a base**, then each day:
   - a **morning event** card (rain, a stray dog, a trader, a crying child, raiders' toll, supply drone…)
   - **3 actions** (morning/afternoon/evening): explore a building (thorough vs quick), rest, fortify, craft a weapon, or heal
   - while exploring: zombie encounters (fight / flee / hide), survivors, locked storerooms, the police armory, the hospital isolation ward…
   - **night**: dinner for everyone, infection check, and a horde attack (strength vs defense)
5. Survive **day 30's final night**: rescue ending. HP reaching 0 ends the game.

Stats: HP, fullness, hydration, sanity. Inventory: food, water, medicine, materials. Also defense, companions, a dog, a generator and weapon tiers (bare hands → shotgun).

## Note on "road view"

Real street-level photos (Kakao/Naver Roadview, Google Street View) need API keys and
forbid deriving 3D geometry from their imagery. So the prototype builds buildings from
OpenStreetMap data, and "로드뷰" is a street-level camera inside that 3D town. A real
roadview panel (e.g. Kakao Roadview with the player's own key) could be added as a reference picture.

## Status

In progress: first playable build.

## Findings

_To be filled in after playtests._ Open questions:
- Does recognising your own street change how attached you get, or how you plan your routes?
- Balance: the 30-day difficulty curve (night strength `4 + day*1.45`, ×1.25 from day 20) is untuned.
- Areas with sparse OSM coverage fall back to procedural buildings. How often does that happen in Korean residential areas?
