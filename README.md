# R.E.P.O Mods

Custom BepInEx mods for R.E.P.O.

## Mods

### REPOPeripheralLighting

Dynamic RGB peripheral lighting integration.

Current backend:
- Logitech Gaming LED SDK / G HUB / LGS

Effects:
- Idle blue
- Damage/death/low health red
- Heal green pulse
- Valuable/shop/enemy-hit amber pulse
- WASD/space highlight on per-key keyboards
- Health display on number row
- Configurable idle breathing, effect intensity, effect toggles and fade durations via `F6`

Controls:
- `F6` toggles in-game config UI

### REPOSoundboard

In-game soundboard with configurable slots.

Controls:
- `F1`-`F8` play slots
- `F9` stop current sound
- `F10` reload sounds
- `F7` toggles in-game config UI

Sound files go in:

```text
BepInEx/plugins/REPOSoundboard/sounds/
```

Supported by Unity decoder: `.wav`, `.ogg`, `.mp3` where available.

## Install

1. Install BepInEx 5 x64 into the R.E.P.O game folder.
2. Copy each mod folder from `mods/` into:

```text
BepInEx/plugins/
```

Example final layout:

```text
BepInEx/plugins/REPOPeripheralLighting/REPOPeripheralLighting.dll
BepInEx/plugins/REPOPeripheralLighting/LogitechLedEnginesWrapper.dll
BepInEx/plugins/REPOPeripheralLighting/LogitechGamingLEDSDK.pdf
BepInEx/plugins/REPOSoundboard/REPOSoundboard.dll
BepInEx/plugins/REPOSoundboard/sounds/sample_beep.wav
```

## Build on Linux

Requires Mono compiler:

```bash
apt install mono-devel
```

Then run:

```bash
./build.sh /path/to/REPO
```

The build downloads Logitech LED SDK 9.00 from Logitech, verifies the ZIP and x64 DLL hashes, and includes `LogitechLedEnginesWrapper.dll` alongside the lighting mod and Logitech's SDK documentation/license PDF. No manual SDK DLL copy is needed. Keep G HUB/LGS running on Windows; a live device test is still required, and Proton support is unverified. The SDK license is in the bundled `LogitechGamingLEDSDK.pdf`; distribute the DLL only as part of the mod, not as a standalone SDK download.

The game path must contain:

```text
REPO_Data/Managed/
BepInEx/core/
```

## Roadmap

- OpenRGB backend
- Razer Chroma backend
- Corsair iCUE backend
- SignalRGB/OpenRGB profile support
- Shared config UI framework
- More game-event lighting hooks
