# Sightseeingway

Take Sightseeingway with you! This Dalamud addon for FFXIV automatically names your screenshots with character and location info, so you can always find that perfect vista again. Supports standard, ReShade and GShade screenshots.

-----

## Description

**Sightseeingway** is a Dalamud addon for XIV designed to help with screenshot organization.  It automatically renames your saved screenshots to include information directly in the filename:

  * **Character Name:**  Know exactly which character took the screenshot.
  * **Map Name:**  Quickly identify the in-game zone where the screenshot was taken.
  * **Landmark:**  Capture the specific spot you're standing in (e.g. *Summerford Farms*), so explorer and landmark shots name themselves.
  * **Coordinates (X, Y):** Pinpoint the exact location on the map.
  * **Eorzea Time:**  Records the in-game time of day, helping you remember the lighting conditions.
  * **Weather:**  Captures the in-game weather, useful for scenic shots with specific weather effects.
  * **Shader Preset:** When using ReShade or GShade, includes the active preset name (requires [Shadingway](https://github.com/gposingway/shadingway) addon).

This addon is perfect for:

  * **Gpose Enthusiasts:**  Easily catalog your scenic gpose locations with time and weather context.
  * **Location Recall:**  Quickly find that amazing vista you screenshotted weeks ago, remembering not just the place but also the time and weather!

No more generic screenshot names like `ffxiv_001.png`!  Sightseeingway helps you keep your FFXIV screenshot library organized and meaningful, now with even more detail.

## Features

  * **Automatic Screenshot Renaming:**  Renames screenshots immediately upon saving.
  * **Supports Standard, ReShade, and GShade Screenshots:** Works with various screenshot types.
  * **Customizable Timestamp Formats:** Choose between compact, regular, or readable date-time formats.
  * **Configurable Filename Elements:** Select which information appears in your filenames.
  * **Element Reordering:** Arrange filename elements in your preferred order (Timestamp will always be first).
  * **Embedded Metadata** *(new in 1.3)*: Optionally embed a structured JSON record of character, location, weather, time, shader, and state flags directly into each PNG (`iTXt` chunk) or JPEG (XMP packet). Per-field opt-in with privacy-respecting defaults. See the [v1 schema](docs/schema/v1.md).
  * **Lightweight and Easy to Use:**  Simple drop-in addon with no complex configurations.

## Installation

1.  Make sure you have [Dalamud](https://goatcorp.github.io/dalamud/) installed.
2.  Open the Dalamud plugin installer within FFXIV (usually by typing `/xlplugins` in chat).
3.  Search for `Sightseeingway` in the plugin list.
4.  Click "Install".

## How to Use

Sightseeingway works automatically in the background! Simply take screenshots as you normally would in FFXIV.

You can customize the filename format through the Sightseeingway settings panel, accessible via the `/sightseeingway` chat command or from the Dalamud plugin settings. The panel includes a live example of the filename that updates as you change options, ensuring you see the full result.

By default, screenshots will be named using the following format:

`[Timestamp]-[CharacterName]-[MapName]-[Landmark]-[Position (X,Y,Z)]-[EorzeaTimePeriod]-[Weather]-[ShaderPreset].[Extension]`

Example: `20250506103045123-WolOfLight-Middle La Noscea-Summerford Farms (23.4,18.1)-Day-ClearSkies.png`

**Filename Elements:**

*   **Timestamp:** Can be formatted in three ways:
    * **Compact:** `yyyyMMddHHmmssfff` (e.g., 20250507123045678)
    * **Regular:** `yyyyMMdd-HHmmss-fff` (e.g., 20250507-123045-678)
    * **Readable:** `yyyy-MM-dd_HH-mm-ss.fff` (e.g., 2025-05-07_12-30-45.678)
*   **CharacterName:** Your current character's name.
*   **MapName:** The name of the current map or zone.
*   **Landmark:** The most specific named place where you're standing (e.g. *Summerford Farms*), falling back to the zone name when there's no named landmark. It won't repeat the MapName element when the two would be identical, so you can safely enable both.
*   **Position:** Your character's X, Y (and Z if applicable) coordinates on the map.
*   **EorzeaTimePeriod:** The current Eorzea time period (e.g., Day, Night, Dawn, Dusk).
*   **Weather:** The current weather in the zone.
*   **ShaderPreset:** (Optional, requires [Shadingway](https://github.com/gposingway/shadingway) addon) The name of the active ReShade/GShade preset.
*   **Extension:** `.png` or the original screenshot extension.

## Configuration

Access the configuration window using the `/sightseeingway` chat command. The window has two columns:

**Filename** (left):
*   Enable or disable individual filename elements.
*   Reorder the elements (Note: Timestamp will always remain as the first element).
*   Select your preferred timestamp format (Compact, Regular, or Readable).
*   See a live preview of the filename format.

**Metadata** (right) — opt-in:
*   Toggle the master "Embed metadata in screenshot files" switch.
*   Pick which fields get embedded, grouped by **Scene** (location, time, weather, flags, shader), **Character** (name, world, race/tribe/sex, job/level, title, mount/minion), and **Affiliation** (free company, grand company).
*   Group-level tri-state checkboxes flip an entire group at once.
*   See a live preview of the JSON payload that would be embedded.

**Diagnostics** (bottom):
*   Pick logging verbosity: **Quiet** (errors only), **Status** (default; rename + metadata milestones in chat), or **Debug** (full pipeline trace).
*   Watch live pipeline status and recent events.
*   "Open Log Folder" / "Copy Diagnostic Snapshot" for support.

## Embedded metadata

When opted in, Sightseeingway writes a structured JSON document into each
screenshot file under the schema discriminator `sightseeingway/v1`. The
record describes character, location, time, weather, shader, and state
flags at the moment the screenshot was taken. Files become self-describing
through any rename, copy, or cloud-sync round-trip.

| Format | Storage location | Identifier |
|---|---|---|
| PNG | `iTXt` chunk | Keyword: `Sightseeingway` |
| JPEG | XMP packet inside an APP1 segment | Namespace: `https://gposingway.github.io/Sightseeingway/schema/v1` |

The schema is locked tolerant-reader: every field is optional, and v1
will only ever gain new optional fields. Readers ignore what they don't
recognise. Full reference: [`docs/schema/v1.md`](docs/schema/v1.md).

A correlation ID (GUID v7) accompanies every embedded payload and links
the file back to entries in the local pipeline log under
`<PluginConfigDir>/logs/`, useful for diagnostics.

## Contributing

Contributions are welcome! Please feel free to fork the repository, make your changes, and submit a pull request.

## License

This project is licensed under the [GNU Affero General Public License v3.0 or later](LICENSE.md) (AGPL-3.0-or-later).

-----

**Enjoy your sightseeing and happy screenshotting!**
