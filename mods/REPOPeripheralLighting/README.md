# R.E.P.O Peripheral Lighting

Adds dynamic peripheral RGB lighting to R.E.P.O through BepInEx.

## Current backend

- Logitech Gaming LED SDK / G HUB / LGS via bundled `LogitechLedEnginesWrapper.dll`
- Whole-device color
- Logitech mouse zones when available
- Logitech per-key keyboard bitmap when available

## Effects

- Idle: gently breathing blue (optional)
- Damage/death/low health: red with adjustable intensity and fade
- Healing: green with adjustable fade
- Valuable discovery / value update / shop initialize / enemy hit: amber with adjustable fade
- WASD/space highlighted cyan on per-key keyboards (toggleable)
- Number row shows health: green lit keys, red missing keys (toggleable)
- `F6` opens a scrollable in-game menu: effect toggles, intensity, breathing speed, fade times, brightness and tests; Save config persists changes.

## Install

Copy the entire `mods/REPOPeripheralLighting/` folder to `BepInEx/plugins/` in your game installation.

Required for Logitech lighting on Windows:

1. Install/run Logitech G HUB or Logitech Gaming Software in the same Windows environment that runs the game.
2. Copy the entire `mods/REPOPeripheralLighting/` folder into `BepInEx/plugins/`; it includes both `REPOPeripheralLighting.dll` and the x64 `LogitechLedEnginesWrapper.dll` from Logitech LED SDK 9.00. The mod explicitly loads the DLL from its own directory.
3. Install [Microsoft Visual C++ 2013 Redistributable (x64)](https://www.microsoft.com/en-us/download/details.aspx?id=40784) if you see Windows error 126: the Logitech DLL imports `MSVCR120.dll`, which that runtime provides. On Proton, install the runtime in the *game's prefix*, not just on the Linux host. Do not download `MSVCR120.dll` from third-party DLL sites.
4. Start the game once so BepInEx creates config/log files. Logitech hardware integration is not tested here; Proton support is unverified.

## Config

After first launch, edit:

`BepInEx/config/andrew.repo.peripheral-lighting.cfg`

Useful settings:

- `General.Enabled`
- `General.Brightness`
- `Logitech.PerKeyKeyboard`
- `Logitech.MouseZones`
- `Effects.HealthNumberKeys`
- RGB color values under `[Colors]`

## Notes

Built from `src/REPOPeripheralLighting/REPOPeripheralLighting.cs` via `./build.sh`.

REA evidence used:

- `PlayerHealth.Hurt/Heal/Death`
- `EnemyHealth.Hurt`
- `ValuableObject.Discover/DollarValueSetLogic`
- `ShopManager.ShopInitialize`
