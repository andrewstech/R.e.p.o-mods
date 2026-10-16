#!/usr/bin/env bash
set -euo pipefail

GAME_DIR="${1:-/root/games/repo}"
MANAGED="$GAME_DIR/REPO_Data/Managed"
BEPINEX="$GAME_DIR/BepInEx/core"
OUT="$(pwd)/mods"

for f in "$BEPINEX/BepInEx.dll" "$BEPINEX/0Harmony.dll" "$MANAGED/netstandard.dll" "$MANAGED/UnityEngine.dll" "$MANAGED/UnityEngine.CoreModule.dll"; do
  if [[ ! -f "$f" ]]; then
    echo "Missing dependency: $f" >&2
    exit 1
  fi
done

mkdir -p "$OUT/REPOPeripheralLighting" "$OUT/REPOSoundboard/sounds"

mcs -target:library -langversion:latest \
  -out:"$OUT/REPOPeripheralLighting/REPOPeripheralLighting.dll" \
  -r:"$BEPINEX/BepInEx.dll" \
  -r:"$BEPINEX/0Harmony.dll" \
  -r:"$MANAGED/netstandard.dll" \
  -r:"$MANAGED/UnityEngine.dll" \
  -r:"$MANAGED/UnityEngine.CoreModule.dll" \
  -r:"$MANAGED/UnityEngine.IMGUIModule.dll" \
  -r:"$MANAGED/Assembly-CSharp.dll" \
  src/REPOPeripheralLighting/REPOPeripheralLighting.cs

mcs -target:library -langversion:latest \
  -out:"$OUT/REPOSoundboard/REPOSoundboard.dll" \
  -r:"$BEPINEX/BepInEx.dll" \
  -r:"$MANAGED/netstandard.dll" \
  -r:"$MANAGED/UnityEngine.dll" \
  -r:"$MANAGED/UnityEngine.CoreModule.dll" \
  -r:"$MANAGED/UnityEngine.AudioModule.dll" \
  -r:"$MANAGED/UnityEngine.IMGUIModule.dll" \
  -r:"$MANAGED/UnityEngine.UnityWebRequestModule.dll" \
  -r:"$MANAGED/UnityEngine.UnityWebRequestAudioModule.dll" \
  src/REPOSoundboard/REPOSoundboard.cs

if [[ ! -f "$OUT/REPOSoundboard/sounds/sample_beep.wav" ]]; then
  python3 scripts/make_sample_beep.py "$OUT/REPOSoundboard/sounds/sample_beep.wav"
fi

stat -c '%n %s bytes' \
  "$OUT/REPOPeripheralLighting/REPOPeripheralLighting.dll" \
  "$OUT/REPOSoundboard/REPOSoundboard.dll"
