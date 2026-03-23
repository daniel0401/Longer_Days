# Longer Days

Slow down time in Lethal Company to make days last longer.

## Features

- Easy-to-use time speed dropdown presets:
  - 0.1 (Very slow)
  - 0.5 (Half speed)
  - 1.0 (Normal)
- Host-controlled TimeSpeed in multiplayer (synced to all players)
- 12-hour or 24-hour clock display
- Optional setting to show the clock indoors
- Clean and safe preset values
- Works with LethalConfig

## How it works

The mod adjusts the in-game time speed so that:

- `1.0` = normal speed
- `0.5` = half speed (days last twice as long)
- `0.1` = very slow

The mod can also change the in-game clock display between:

- `24 Hour`
- `12 Hour`

And optionally:

- Show the clock inside the ship and buildings

## Multiplayer

- `TimeSpeed` is controlled by the host and synced to all players
- `ClockFormat` and `ShowClockIndoors` are local settings per player

## Configuration

You can change the settings using:

- LethalConfig (recommended)
- or manually in:
  `BepInEx/config/ElecTRiCbOi59.LongerDays.cfg`

## Notes

- The host controls time speed in multiplayer
- Other time-related mods may conflict
- If updating from an older version, your existing config may keep previous values until you change them

## Installation

1. Install BepInEx
2. Install CSync
3. Place `Longer_Days.dll` into:
   `BepInEx/plugins/`

## Author

ElecTRiCbOi59
