# Sightseeingway

Take Sightseeingway with you! This Dalamud addon for FFXIV automatically names your screenshots with character and location info, so you can always find that perfect vista again. Supports standard, ReShade and GShade screenshots.

-----

## Description

**Sightseeingway** is a Dalamud addon for XIV designed to help with screenshot organization.  It automatically renames your saved screenshots to include information directly in the filename:

  * **Character Name:**  Know exactly which character took the screenshot.
  * **Map Name:**  Quickly identify the in-game zone where the screenshot was taken.
  * **Coordinates (X, Y):** Pinpoint the exact location on the map.
  * **Eorzea Time:**  Records the in-game time of day, helping you remember the lighting conditions.
  * **Weather:**  Captures the in-game weather, useful for scenic shots with specific weather effects.

This addon is perfect for:

  * **Gpose Enthusiasts:**  Easily catalog your scenic gpose locations with time and weather context.
  * **Location Recall:**  Quickly find that amazing vista you screenshotted weeks ago, remembering not just the place but also the time and weather!

No more generic screenshot names like `ffxiv_001.png`!  Sightseeingway helps you keep your FFXIV screenshot library organized and meaningful, now with even more detail.
## Features

  * **Automatic Screenshot Renaming:**  Renames screenshots immediately upon saving.
  * **Supports Standard, ReShade, and GShade Screenshots:** Works with various screenshot types.
  * **Lightweight and Easy to Use:**  Simple drop-in addon with no complex configurations.

## Installation

1.  Make sure you have [Dalamud](https://goatcorp.github.io/dalamud/) installed.
2.  Open the Dalamud plugin installer within FFXIV (usually by typing `/xlplugins` in chat).
3.  Search for `Sightseeingway` in the plugin list.
4.  Click "Install".

## How to Use

Sightseeingway works automatically in the background! Simply take screenshots as you normally would in FFXIV.  When you save a screenshot, Sightseeingway will automatically rename it with the following format:

### `[Timestamp][Character][Map][Position][EorzeaTime][Weather][Extension]`

*   **Timestamp:**  `YYYYMMDDHHMMSSmil` (e.g., `20250222135356123` for February 22, 2025, 1:53:56 PM, 123 ms). Timestamps are in your local time.
*   **Character:** Your character's name, if available (e.g., "My Character").
*   **Map:** The name of the in-game map (e.g., "The Waking Sands").
*   **Position:**  The coordinates on the map in the format `([X-Coordinate],[Y-Coordinate])` (e.g., "(3.5,3.6)").
*   **EorzeaTime:** The in-game time of day when the screenshot was taken. Abbreviations are used:
    *   `Morn` - Morning
    *   `Aftn` - Afternoon
    *   `Evng` - Evening
    *   `Night` - Night
    *   `Noon` - Noon
    *   `Midnt` - Midnight
    *   `GoldH` - Golden Hour
*   **Weather:** The current weather condition in the game (e.g., "Fair Skies", "Rain", "Snow").
*   **Extension:** The file extension of your screenshot (e.g., ".png").

**Example:**

`20250225143201969-My Character-The Waking Sands (3.6,3.6)-Aftn-Fair Skies.png`

This screenshot was taken on February 25, 2025, at 2:32:01 PM (local time), by the character "My Character" in "The Waking Sands" at coordinates X:3.6, Y:3.6, in the Afternoon, under Fair Skies.

## Planned Features (Future Development)

  * **Customizable Filename Format:** Allow users to choose which elements to include in the filename and customize the order (e.g., date, time, etc.).
  * **Option to include Region Name:** Add the region name (e.g., Thanalan) to the filename for even more location context.
  * **Settings Panel:**  Create a Dalamud settings panel for configuration options.

## Contributing

Contributions are welcome!  If you have suggestions, bug reports, or would like to contribute code, please feel free to open issues or pull requests on this GitHub repository.

-----

**Enjoy your sightseeing and happy screenshotting!**
