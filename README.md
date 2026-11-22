# Storm Chasers

Storm Chasers runs on [Facepunch's Sandbox](https://sbox.game/facepunch/sandbox). Sandbox owns the player, camera, HUD, inventory, physgun, toolgun, and spawn menu. This project adds a rural crossroads, a gas station, a walk-in house, and a tornado spawner.

## Getting started

Install s&box through Steam, open `storm_chasers.sbproj`, and press **Play** from `scenes/storm_chasers.scene`. Click the game viewport to focus it. The editor downloads the pinned `facepunch.sandbox#446139` dependency on first use. The dependency's `scenes/system.scene` supplies the stock camera and HUD; `storm_chasers.scene` supplies the map.

### Controls

| Input | Action |
| --- | --- |
| WASD | Move |
| Mouse | Look around |
| Shift | Run |
| Space | Jump |
| Hold Q | Sandbox spawn menu |
| Hold C | Sandbox context menu |
| X | Switch first/third person |
| Z | Undo a spawn |
| Shift+Esc | Native pause menu in editor Play mode |

Dark clouds, rain and light distance fog cover the whole map, even between tornadoes. An active storm intensifies rain and fog across the map and adds two broad cloud bands above a lower rotating shelf. Rain gusts sideways and thickens further near the funnel, except inside the F5's eye-like opening. Nearby drops are suppressed under roofs. The first tornado appears a few seconds after Play starts; the spawner alternates between two track markers. Move or rotate the markers to change where each tornado appears and drifts.

Each spawn rolls a visual F0–F5 preset. The odds from F0 to F5 are **45%, 27%, 15%, 8%, 4%, and 1%**. Higher presets tend to be wider, but width is rolled separately from the rating and the ranges overlap. Rating controls how much the map-wide weather intensifies. These are game presets, not real-world ratings based on surveyed damage. Wind forces and destruction are not implemented.

You spawn on the forecourt of Mike’s Service Station, beside the west approach. The station has two driveways, four decorative fuel pumps under a lit canopy, and a walk-in shop. Head east through the intersection to reach the house on the left. All four approaches run to the edges of the 12,500-by-12,500-unit field. The map uses stock s&box materials. Selected pieces on the house and station, including the fuel pumps and canopy panels, have separate colliders and the `storm_breakable` tag. The tag does not apply damage by itself.

### NixOS

Run `./launch.sh` from the project directory. The launcher uses `steam-run`, preloads the bundled HarfBuzz library when present to avoid a [Linux editor startup crash](https://github.com/Facepunch/sbox-public/issues/11770), and enables [input events in the embedded X11 viewport](https://github.com/Facepunch/sbox-public/issues/12204). This does not fix intermittent mouse capture.

For a non-default Steam library, set `FACEPUNCH_ENGINE` to the s&box installation directory, or save that directory as a single line in `.sbox-engine`. This local file is ignored by Git. Standard Steam installation locations are detected automatically.

## Project layout

- `Assets/scenes/storm_chasers.scene` — terrain, four-way road, gas station, house, player spawn point, lighting, and tornado tracks. Keep its `storm_chasers.scene_d` sidecar: it stores the map's source polygon meshes and UVs.
- `Code/StormTornado*.cs`, `Code/StormWeather*.cs`, and `Code/StormSmokeTexture.cs` — tornado lifecycle and ratings, weather and funnel effects, and procedural textures. No custom player, input, or inventory code.
- `ProjectSettings/` — Sandbox-derived input and collision settings plus platform settings.
- `Tests/*.cs.txt` — in-engine checks. Start Play and run them through the editor's `run_csharp` MCP tool. Run `StationSmoke` before moving to verify the actual spawn, station access and a removable canopy panel. `MapSmoke` checks road collision; `HouseSmoke` checks entry and removable house parts. `SandboxFoundationSmoke` spawns a prop and scripted entity, which stopping Play clears. `TornadoSmoke` checks the visual layers and their lifecycle; `TornadoRatingSmoke` checks all rating rolls and size presets. `WeatherSmoke` checks background rain, local storm intensity, fog and shelter. Each weather-related test can take up to a minute.

Sandbox input and collision settings are based on Facepunch's MIT-licensed source (`1cee1dd28b6de82b21afdcecdbc3f34c0047152c`). See `ThirdParty/Facepunch-Sandbox-LICENSE.txt`.

## Local files

Editor-generated projects, launch profiles, IDE settings, compiled assets, caches, and local environment files are ignored. The editor regenerates its project files when opened.
