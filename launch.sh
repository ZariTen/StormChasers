#!/usr/bin/env bash
set -euo pipefail

project="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
engine="${FACEPUNCH_ENGINE:-}"

if [[ -z "$engine" && -f "$project/.sbox-engine" ]]; then
  IFS= read -r engine < "$project/.sbox-engine" || [[ -n "$engine" ]]
fi

if [[ -z "$engine" ]]; then
  for candidate in \
    "$HOME/.local/share/Steam/steamapps/common/sbox" \
    "$HOME/.steam/steam/steamapps/common/sbox"; do
    if [[ -x "$candidate/sbox-dev" ]]; then
      engine="$candidate"
      break
    fi
  done
fi

if [[ -z "$engine" || ! -x "$engine/sbox-dev" ]]; then
  printf '%s\n' 's&box not found. Set FACEPUNCH_ENGINE or put its installation directory in .sbox-engine.' >&2
  exit 1
fi
if ! command -v steam-run >/dev/null 2>&1; then
  printf '%s\n' 'steam-run is required. Install it or enter a shell with nix-shell -p steam-run.' >&2
  exit 1
fi

cd -- "$engine"
engine="$PWD"
exec steam-run env \
  LD_PRELOAD="$engine/bin/linuxsteamrt64/libHarfBuzzSharp.so" \
  QT_QPA_PLATFORM=xcb \
  SDL_VIDEO_DRIVER=x11 \
  SDL_VIDEO_X11_EXTERNAL_WINDOW_INPUT=1 \
  ./sbox-dev -project "$project/storm_chasers.sbproj" "$@"
