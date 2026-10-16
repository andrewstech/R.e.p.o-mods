# R.E.P.O Peripheral Lighting

Adds dynamic peripheral RGB lighting to R.E.P.O through BepInEx.

## Current backend

- Logitech Gaming LED SDK / G HUB / LGS via `LogitechLedEnginesWrapper.dll`
- Whole-device color
- Logitech mouse zones when available
- Logitech per-key keyboard bitmap when available

## Effects

- Idle: blue
- Damage/death/low health: red
- Healing: green pulse
- Valuable discovery / value update / shop initialize: amber pulse
- WASD/space highlighted cyan on per-key keyboards
- Number row shows health: green lit keys, red missing keys

## Install

This build is already installed at:

`/root/games/repo/BepInEx/plugins/REPOPeripheralLighting.dll`

Required for Logitech lighting on Windows/Proton:

1. Install/run Logitech G HUB or Logitech Gaming Software in the same Windows/Proton environment that runs the game.
2. Put Logitech's x64 `LogitechLedEnginesWrapper.dll` beside `REPO.exe` if G HUB/LGS does not expose it globally to the game.
3. Start the game once so BepInEx creates config/log files.

## Config

After first launch, edit:

`/root/games/repo/BepInEx/config/andrew.repo.peripheral-lighting.cfg`

Useful settings:

- `General.Enabled`
- `General.Brightness`
- `Logitech.PerKeyKeyboard`
- `Logitech.MouseZones`
- `Effects.HealthNumberKeys`
- RGB color values under `[Colors]`

## Notes

Built from source:

`/root/.hermes/profiles/devteck/cache/scratch/REPOPeripheralLighting.cs`

REA evidence used:

- `PlayerHealth.Hurt/Heal/Death`
- `EnemyHealth.Hurt`
- `ValuableObject.Discover/DollarValueSetLogic`
- `ShopManager.ShopInitialize`
