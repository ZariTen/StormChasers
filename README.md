# Storm Chasers

An early s&box prototype with flat terrain, a third-person character, and the native pause menu.

## Getting started

Install s&box through Steam, then open `storm_chasers.sbproj` in the editor and press **Play**. Click the game viewport to focus it.

### Controls

| Input | Action |
| --- | --- |
| WASD | Move |
| Mouse | Look around |
| Shift | Run |
| Space | Jump |
| Esc | Open the native pause menu and release the cursor |

Closing the pause menu resumes gameplay and captures the mouse again.

### NixOS

Run `./launch.sh` from the project directory. The launcher uses `steam-run` and includes the Linux editor's HarfBuzz and embedded-X11 input workarounds.

For a non-default Steam library, set `FACEPUNCH_ENGINE` to the s&box installation directory, or save that directory as a single line in `.sbox-engine`. This local file is ignored by Git. Standard Steam installation locations are detected automatically.

## Project layout

- `Assets/scenes/storm_chasers.scene` — terrain, character, camera, and lighting.
- `Code/StormControls.cs` — mouse capture and native pause-menu integration.
- `ProjectSettings/` — shared input and platform settings.
- `Tests/Smoke.cs.txt` — in-engine smoke checks, run in Play mode through MCP's `run_csharp` tool.

## Local files

Editor-generated projects, launch profiles, IDE settings, compiled assets, caches, and local environment files are ignored. The editor regenerates its project files when opened.
