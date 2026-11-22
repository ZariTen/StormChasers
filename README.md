# Storm Chasers

Storm Chasers runs on [Facepunch's Sandbox](https://sbox.game/facepunch/sandbox). Sandbox owns the player, camera, HUD, inventory, physgun, toolgun, and spawn menu. This project adds a flat test map and a tornado spawner.

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

The first tornado appears a few seconds after Play starts. The spawner alternates between two map markers, with one tornado active at a time. Tornadoes are visual only: no wind forces, damage, or debris yet. Move the markers in the scene to change the tracks; their forward direction determines drift.

### NixOS

Run `./launch.sh` from the project directory. The launcher starts the editor through `steam-run` without overriding Qt or SDL settings.

For a non-default Steam library, set `FACEPUNCH_ENGINE` to the s&box installation directory, or save that directory as a single line in `.sbox-engine`. This local file is ignored by Git. Standard Steam installation locations are detected automatically.

## Project layout

- `Assets/scenes/storm_chasers.scene` — terrain, lighting, player spawn point, and tornado tracks.
- `Code/StormTornadoSpawner.cs` — tornado lifecycle and funnel visual. No custom player, input, or inventory code.
- `ProjectSettings/` — Sandbox-derived input and collision settings plus platform settings.
- `Tests/SandboxFoundationSmoke.cs.txt` and `Tests/TornadoSmoke.cs.txt` — in-engine checks. Start Play and run them through the editor's `run_csharp` MCP tool. The first test spawns a prop and scripted entity; stopping Play clears them. The tornado check can take up to half a minute.

Sandbox input and collision settings are based on Facepunch's MIT-licensed source (`1cee1dd28b6de82b21afdcecdbc3f34c0047152c`). See `ThirdParty/Facepunch-Sandbox-LICENSE.txt`.

## Local files

Editor-generated projects, launch profiles, IDE settings, compiled assets, caches, and local environment files are ignored. The editor regenerates its project files when opened.
