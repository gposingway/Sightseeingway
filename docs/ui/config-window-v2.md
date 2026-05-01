# Config Window v2 Specification

**Status:** Locked.

## Layout

Two-column horizontal layout, default ~880×640, resizable.

```
┌─ Sightseeingway Configuration ─────────────────────────────────┐
│ ┌── Filename ────────────────┐ ┌── Metadata ─────────────────┐ │
│ │ (existing config preserved │ │ [✓] Embed metadata in       │ │
│ │  unchanged from v1.2)      │ │     screenshot files        │ │
│ │                            │ │                             │ │
│ │ Timestamp format: ▼        │ │ ▼ [✓] Scene                 │ │
│ │ Field selection & order    │ │     ✓ Location              │ │
│ │  ↑↓ ✓ Timestamp            │ │     ✓ Time                  │ │
│ │  ↑↓ ✓ Character Name       │ │     ✓ Weather               │ │
│ │  ↑↓ ✓ Map/Zone Name        │ │     ✓ Flags                 │ │
│ │  ↑↓ ✓ Position             │ │     ✓ Shader                │ │
│ │  ↑↓ ✓ Eorzea Time          │ │ ▼ [◼] Character             │ │
│ │  ↑↓ ✓ Weather              │ │     ✓ Name                  │ │
│ │  ↑↓ ✓ Shader Preset        │ │     ☐ World                 │ │
│ │                            │ │     ✓ Race / Tribe / Sex    │ │
│ │                            │ │     ✓ Job / Level           │ │
│ │                            │ │     ☐ Title                 │ │
│ │                            │ │     ✓ Mount / Minion        │ │
│ │                            │ │ ▶ [☐] Affiliation           │ │
│ └────────────────────────────┘ └─────────────────────────────┘ │
│ Filename example: 20260501-...png                              │
│ Metadata preview: { ... }  [▶ expand]                          │
│ ──────────────────────────────────────────────────────────────  │
│ Diagnostics                                                    │
│ Logging:  [Quiet] [Status] [Debug]                             │
│ Pipeline: Idle (0 pending)                                     │
│ Recent events: [▶ expand]                                      │
│ [Open Log Folder]  [Copy Diagnostic Snapshot]                  │
│ ──────────────────────────────────────────────────────────────  │
│ [Save Settings (amber when dirty)] [Revert] [Reset to Defaults]│
└─────────────────────────────────────────────────────────────────┘
```

## Metadata block

### Master toggle

`[ ] Embed metadata in screenshot files` — default OFF on first install.
Single line under the "Metadata" header. Below it, a one-line explainer:
"Writes character, location, and shader information into the image file."
Block contents are dimmed (`BeginDisabled`) when the master is off.

### Tri-state group toggles

Three collapsible groups: Scene, Character, Affiliation. Each with a parent
checkbox following standard tri-state semantics:

| Children state | Parent shown | Click parent |
|---|---|---|
| All on | ✓ checked | turn all OFF |
| All off | ☐ unchecked | turn all ON |
| Mixed | ◼ indeterminate | turn all OFF |

The parent's expand/collapse chevron (`▶`/`▼`) is independent of its checked
state.

### Default field selections

| Group | Field | Default |
|---|---|---|
| Scene | Location | ON |
| Scene | Time | ON |
| Scene | Weather | ON |
| Scene | Flags | ON |
| Scene | Shader Preset (collection, name, path) | ON |
| Scene | Display (resolution, aspect, screen type) | ON |
| Character | Character Data (name, race/tribe/sex, job/level, title) | ON |
| Character | World | OFF |
| Character | Mount / Minion | ON |
| Affiliation | Free Company | OFF |
| Affiliation | Grand Company | OFF |

### JSON preview component

Mirror of the existing `FilenamePreviewComponent`. Shows the live JSON payload
that would be written for the current toggle state. Uses an example
StateSnapshot when not in-game, the live state when available. Collapsed by
default; expand chevron reveals.

## Diagnostics section

New collapsible section below the metadata preview, above the action buttons.

### Logging verbosity

Three-button segmented control. Implemented as three side-by-side `ImGui.Button`
calls, the selected one rendered with `ImGuiCol.ButtonActive` styling.

| Tier | Chat output |
|---|---|
| Quiet | Errors only |
| Status (default) | Errors + rename milestones + metadata-embedded milestones |
| Debug | Full pipeline trace (high volume) |

### Pipeline status

Single-line live readout: `Idle | Processing N | Error: <message>`. Updates on
the framework tick from in-memory worker state.

### Recent events

Collapsible. Backed by an in-memory ring buffer (last 50 events). Each entry
shows: timestamp, level, correlation ID prefix (8 chars), event message.

### Action buttons

- **Open Log Folder** — opens
  `PluginInterface.GetPluginConfigDirectory()/logs/` in the OS file browser.
- **Copy Diagnostic Snapshot** — copies a multi-line text block to the
  clipboard containing: plugin version, Dalamud version, OS, current config
  (sanitized of any user-identifying values), last 50 events.

## Save state visualization

Driven by the existing `configChanged` flag in `ConfigWindow.Draw`.

| State | Save button | Revert button | Window title |
|---|---|---|---|
| `configChanged == false` | Disabled, default color | Disabled, default color | `Sightseeingway Configuration` |
| `configChanged == true`  | Enabled, amber tint | Enabled, default color | `Sightseeingway Configuration *` |

Color/disabled state must reset on the **click** of Save, not on save
completion. Implementation: set `configChanged = false` synchronously in the
Save handler before any I/O.

## Configuration migration

Bump `Configuration.Version` from 4 to 5. New fields:

- `EmbedMetadata : bool` (default false)
- `MetadataFields : Dictionary<string, bool>` (defaults per the table above)
- `LogVerbosity : LogVerbosity` enum (default `Status`)

Retiring fields:

- `ShowNameChangesInChat` (bool) → maps to `LogVerbosity`:
  `true → Status`, `false → Quiet`
- `DebugMode` (bool) → if true, overrides to `LogVerbosity = Debug`

Migration runs once on load when `Version < 5`, then `Version = 5` is persisted
on next save.
