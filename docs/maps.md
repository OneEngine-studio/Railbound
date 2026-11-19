# Railbound maps

Every mode on the mode screen has its own map, designed for how that mode plays. MAP-2 stays the Classic BR map. The eight new maps are listed below.

![The eight maps in a local test build](images/maps-preview.jpg)

## How maps are built

- **One match scene, many maps.** MAP-2 already holds everything a match needs: networking, the player and camera, match setup, loot sync, zone, bots, HUD, pause menu, lighting and sky. A new map reuses all of that. On scene load, `MapLoader` reads the map ID, switches off MAP-2's own ground (terrain, rocks, water, baked NavMesh, reflection probes) and builds the new map in its place.
- **Maps are code, in git.** Each map is a layout class (`Assets/Project/Scripts/Maps/Layouts/`) that builds terrain, buildings, cover, props, spawn points and objectives from a fixed seed. The server and every client build exactly the same geometry. Nothing depends on the gitignored MAP-2 folder (see `docs` on map assets), except that trees, grass and rocks are taken from MAP-2's terrain when it is loaded, with fallbacks from the packs in git.
- **Surfaces and props:**
  - **Surfaces:** materials and terrain layers are baked by `Railbound > Maps > Bake Map Kit` from the textures in git (NWH ground set: asphalt, concrete, cliff, dirt, gravel, road, sand, grass, with normal maps).
  - **Props:** shipping containers, wagons, locomotives and rails come from the WSM train pack, copied as static meshes (no train physics).
  - **Everything else** is built from boxes with world-scaled UVs, so textures keep their real size on any wall.
- **Gameplay hooks:**
  - **Map data:** each map places `MapAnchor` markers (team spawns, free-for-all spawns, capture zones, bomb sites, extraction points), `BRLootPoint`s inside buildings, and out-of-bounds walls. It also sets the play area (`BRMatchSetup` centre and radius, which the zone and spawns use), the player count (`BRMatchCoordinator.Capacity`), the bot budget, and the sun and fog.
  - **NavMesh:** baked at load for bots.
  - **Minimap:** bakes from the new terrain by itself.
- **Choosing a map:**
  - **Server:** `-map ID`.
  - **Client:** `-map ID`, otherwise the map of the mode the player queued for.
  - **Check:** the server tells each client its map; a client on the wrong map leaves with a clear message.
  - **MAP-2 stays the default.** Classic BR is unchanged.
- **Honest lobby:** a mode without its rules stays COMING SOON. The mode screen shows each mode's real map name. The maps are playable for testing (`-map`) and ready for when each mode's rules are built.

## The maps

Sizes are the playable area. The terrain around it is bigger, for the view.

| map | mode | players | size | concept |
|---|---|---|---|---|
| **RAILHEAD** | Resurgence | 20 (duo–squad) | 520 × 520 m | A compact rail junction town: tight, fast, close fights, lots of cover between respawns |
| **TIMBERLINE** | Tactical (zero build) | 20 | 900 × 900 m | Forested highland of ridges, gullies and rock outcrops: natural cover only, no buildings to hide in |
| **DEPOT 9** | Team Deathmatch | 6 v 6 | 120 × 76 m | A mirrored three-lane train depot: an engine shed in the middle, containers and a platform on the flanks |
| **FOUNDRY** | Free-for-all | 8 | 76 × 76 m | An industrial courtyard laid out as a ring around a two-floor furnace hall: no safe corner, threats from every side |
| **SWITCHBACK** | Gun Game | 8 | 60 × 60 m | The smallest map: two tracks crossing at a signal box, wagons for cover, short loops that keep everyone fighting |
| **SIGNAL HILL** | Control | 6 v 6 | 160 × 110 m | Three zones in a line: A relay hut, B radio mast on the hill, C pump house; team spawns north and south, the same distance from all three |
| **BLACKWATER** | Search & Destroy | 6 v 6 | 150 × 96 m | Attackers west, defenders east; site A at a fuel depot (north), site B in a warehouse (south), a rail cut through the middle |
| **DUSTFALL** | Extraction | 20 (duo–squad) | 1000 × 1000 m | Dry canyon and mesa country: four loot areas (mine, ghost town, wrecked train, quarry) and three extraction pads at the edges |

### RAILHEAD (Resurgence)
- **Layout:**
  - **Rail yard (centre):** four parallel tracks with parked wagons and container stacks, and a station hall in the middle of it.
  - **Four districts around the yard,** each with its own look and 2-floor houses with interiors:
    - Market (north-west)
    - Workshops (north-east)
    - Terraces (south-west)
    - Water-tower hill (south-east)
- **Edge:** a river on the south side and low cliffs on the others, so the circle closes on built-up ground.
- **Why it suits Resurgence:** short distances between buildings let respawning players get back into the fight in seconds, and the yard gives a strong final circle.

### TIMBERLINE (Tactical)
- **Terrain:** two long ridges with a river valley between them, gullies down the slopes, and clusters of rock outcrops on the crests.
- **Cover:**
  - Dense pine stands with open meadows in between.
  - Stone ruins (low walls only), a fire lookout tower and two log bridges.
- **Why it suits Tactical:** fights are decided by high ground and natural cover.

### DEPOT 9 (Team Deathmatch, 6 v 6)
- **Layout:** mirrored along the long axis. Spawns at each end, behind cover, with no sightline between them.
- **Centre lane:** an engine shed with a parked locomotive, inspection pits and side doors.
- **North lane:** container stacks two high, with gaps.
- **South lane:** a raised platform with a footbridge over the tracks.
- **Cross-routes:** three routes cross between the lanes, so no lane is a dead end.

### FOUNDRY (Free-for-all, 8)
- **Centre:** a furnace hall with an upper gallery and stairs at both ends.
- **Ring:** a loop of yards and small rooms around the hall, linked by doors every few metres.
- **Spawns:** 16 spread around the ring, so a respawn is never in a fight.
- **Why it suits Free-for-all:** sightlines stay short, so there is no safe corner.

### SWITCHBACK (Gun Game, 8)
- **Layout:** two tracks crossing in an X, with a two-floor signal box in the middle.
- **Cover:** wagons and low walls in the four quarters.
- **Spawns:** 12, one per quarter edge in turn.
- **Why it suits Gun Game:** every weapon from pistol to sniper has a use within a few seconds of a spawn.

### SIGNAL HILL (Control, 6 v 6)
- **Zones:** three capture zones on a line west to east, each a marked pad with a lettered sign:
  - A: relay hut (west)
  - B: radio mast on a low hill (centre)
  - C: pump house (east)
- **Spawns:** teams spawn north and south, the same distance from all three zones.
- **Cover:** a trench line and rock outcrops between the zones.

### BLACKWATER (Search & Destroy, 6 v 6)
- **Spawns:** attackers west, defenders east.
- **Bomb sites:**
  - Site A (north): a fuel depot with tanks and pipe racks.
  - Site B (south): a warehouse with two entrances and a catwalk.
- **Middle:** a rail cut below ground level joins the lanes.
- **Timing:** defenders reach either site about ten seconds before attackers.

### DUSTFALL (Extraction, 20)
- **Terrain:** mesas and canyons in sand and red rock.
- **Loot areas:**
  - Copper mine (north): pit and buildings.
  - Ghost town (west): main street and saloon.
  - Wrecked train (east): derailed wagons in a canyon.
  - Quarry (south).
- **Extraction pads:** three at the map edges, each with a beacon mast.
- **Why it suits Extraction:** a squad has to cross open ground to get out.

## Testing a map

Preview in the editor: `Railbound > Maps > Preview > ...` builds a map into an empty scene (do not save that scene). After changing the kit list or a surface, run `Railbound > Maps > Bake Map Kit`.

In a build:

```
Railbound.exe -batchmode -nographics -server -port 7777 -match test -insecure-join -map depot9
Railbound.exe -client -ip 127.0.0.1 -port 7777 -match test -join-token local -map depot9
```

`-maptest DIR` on the client logs the map's build time, object counts and NavMesh size, checks that every spawn and objective marker is on walkable ground and that the player and bots stand on the ground, then flies the camera through the map's viewpoints and saves a screenshot of each.

What a map does not have yet: its mode's rules. A match on a new map runs the battle royale systems (zone, loot, bots) until each mode's rules are built on its markers (`MapAnchor`).
