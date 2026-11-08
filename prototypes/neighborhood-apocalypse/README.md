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
| Building footprints, heights, roads, shops, trees, parks | Overpass API (`overpass-api.de`, with mirrors as fallback) |
| Satellite photo (ground + roofs) | Esri World Imagery (fallback: OSM map tiles) |
| Terrain elevation | AWS Terrain Tiles (terrarium) |
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
3. **Making it look like the real place**
   - The ground is the real **satellite photo** draped over **real terrain elevation**, so hills, roads, parking lots and parks sit where they really are.
   - **Roofs** sample the same satellite photo, so each roof shows its real colour and rooftop clutter.
   - **Walls** get procedural Korean-street facades chosen from the building type, `building:material` and `building:colour`: white apartment blocks with balcony windows, brick, beige or white villas, glass offices, schools, corrugated warehouses. Windows snap to whole bays per wall.
   - **Ground-floor shops** get a glass storefront with Korean signboards (편의점, 약국, 마트, 치킨, 부동산 …), some with smashed glass or half-closed shutters.
   - **Trees** come from OSM `natural=tree` nodes, and parks and woods are scattered with trees.
   - At night a few windows stay lit (about 40% of buildings, only a few windows each).
4. **Roadview (🚶 로드뷰)**: a first-person street-level view of the generated buildings
   (drag to look, WASD/arrow keys or the on-screen pad to walk, walls block movement). Click a building in any view to explore it.
5. **Choose a base**, then each day:
   - a **morning event** card (rain, a stray dog, a trader, a crying child, raiders' toll, supply drone…)
   - **3 actions** (morning/afternoon/evening): explore a building (thorough vs quick), rest, fortify, craft a weapon, or heal
   - while exploring: zombie encounters (fight / flee / hide), survivors, locked storerooms, the police armory, the hospital isolation ward…
   - **night**: dinner for everyone, infection check, and a horde attack (strength vs defense)
6. Survive **day 30's final night**: rescue ending. HP reaching 0 ends the game.

Stats: HP, fullness, hydration, sanity. Inventory: food, water, medicine, materials. Also defense, companions, a dog, a generator and weapon tiers (bare hands → shotgun).

## Note on "road view" and photo-real walls

Real street-level photos (Kakao/Naver Roadview, Google Street View) need API keys and
forbid deriving 3D geometry or textures from their imagery. So walls are procedural, while
roofs and ground use satellite imagery whose terms allow display. "로드뷰" is a street-level
camera inside the 3D town.

A fully photographic town would need **Google Photorealistic 3D Tiles** (Map Tiles API key
and billing, plus Google's attribution and display rules). It would be a separate render mode,
because gameplay needs per-building click targets that photogrammetry meshes don't provide.

## Status

In progress: first playable build.

## Findings

_To be filled in after playtests._ Open questions:
- Does recognising your own street change how attached you get, or how you plan your routes?
- Balance: the 30-day difficulty curve (night strength `4 + day*1.45`, ×1.25 from day 20) is untuned.
- Areas with sparse OSM coverage fall back to procedural buildings. How often does that happen in Korean residential areas?
