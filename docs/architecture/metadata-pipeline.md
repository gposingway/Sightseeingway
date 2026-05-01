# Sightseeingway Metadata Pipeline Architecture

**Status:** Locked.

## Goals

- Capture full game state at the moment closest to the actual screenshot.
- Write metadata into the image file without blocking the game thread.
- Survive process crashes without losing already-captured state.
- Stay forward-only: never attempt to backfill metadata into pre-feature files.

## Components

### `StateSnapshot`

Immutable record holding the full v1 schema payload as resolved data
(localized names, integer IDs, normalized strings). Created on the framework
thread; freely readable from any thread thereafter. No live game-object
references — every value is captured by value. Includes
`CorrelationId : Guid` (generated via `Guid.CreateVersion7()`).

### Sidecar file

Per-screenshot durability artifact. Lives next to the image file as
`<filename>.sw-pending.json`. Contains:

```json
{
  "correlationId": "01902abc-d1e2-7234-9000-000000000000",
  "originalPath":  "C:/.../ffxiv_001.png",
  "targetPath":    "C:/.../20260501-183000-Wol-Limsa.png",
  "createdAt":     "2026-05-01T18:30:00.123Z",
  "renamed":       false,
  "injected":      false,
  "snapshot":      { /* the v1 metadata payload, ready to embed */ }
}
```

`renamed` and `injected` are **hints** for fast-path skipping. The filesystem
(target file existence, presence of our embedded chunk) is the ground truth on
every recovery.

### `IMetadataWriter`

```csharp
interface IMetadataWriter {
    Task Write(string filePath, StateSnapshot snapshot, CancellationToken ct);
}
```

Implementations: `PngMetadataWriter`, `JpegMetadataWriter`. Pure — no
`Plugin.*` references. Testable in isolation. Resolved by file extension at
dequeue time; unknown extensions are logged and skipped.

### Background worker

A single dedicated `Thread`, `IsBackground = true`, named
`Sightseeingway.MetadataWorker`. No priority hint — single-threadedness is the
throttling mechanism. Wakes on `ManualResetEventSlim` signal with a 30-second
timeout fallback for self-heal. Drains all available sidecars when woken.

## Lifecycle

```
[FSW.Created on threadpool]
        │
        ▼  (Pattern B: eager capture)
[Dispatch to framework tick → CaptureState() → StateSnapshot]
        │
        ▼  (existing helper)
[Wait for file release on .png]
        │
        ▼
[Resolve target path from snapshot + config]
        │
        ▼
[Write sidecar at originalPath.sw-pending.json (renamed:false, injected:false)]
        │
        ▼
[Rename .png → target] → set renamed:true
        │
        ▼
[Move sidecar to track .png] → still injected:false
        │
        ▼
[Signal worker]
        │
        ▼
[Worker: wait for file release → write metadata via IMetadataWriter]
        │
        ▼
[set injected:true → delete sidecar]
```

## Pattern B: eager state capture

Snapshot is requested *before* file release. Rationale: the player may have
moved during the OS write/release window; capturing at the FSW event time
produces accurate "moment of screenshot" data. The file-release wait proceeds
in parallel with snapshot dispatch.

## Two-stage idempotency

Each step uses the filesystem as ground truth:

| Step | Ground-truth check | Action if already done |
|---|---|---|
| Rename | `File.Exists(targetPath)` | Skip rename; set `renamed:true`. |
| Inject | Image already contains a `Sightseeingway` chunk | Skip inject; set `injected:true`. |
| Cleanup | None | Delete sidecar when both flags true. |

Re-running any step is safe.

## File-release waiting

Two distinct points, both reusing `WaitForFileReleaseGeneric`:

1. **Before rename** (existing): game/ReShade/GShade may still hold the file.
2. **Before injection** (new): cloud sync (OneDrive, Dropbox), antivirus,
   Explorer thumbnail generation may briefly grab the renamed file.

Without (2), injection sporadically fails on machines with cloud-synced
screenshot folders.

## Crash recovery

Plugin startup scans every monitored directory for `*.sw-pending.json`. Each
sidecar is processed by the same worker logic as live operation — no separate
"recovery mode" code path. Result table:

| File at originalPath | File at targetPath | Sidecar at | Action |
|---|---|---|---|
| present | absent | originalPath | Standard: rename, move sidecar, inject. |
| absent | present | originalPath | Mid-rename crash: move sidecar, inject. |
| absent | present | targetPath | Standard: inject, delete sidecar. |
| absent | absent | any | Orphan after grace period: delete sidecar. |

**Orphan cleanup**: a sidecar whose target is absent for >60 seconds is
deleted. Avoids accumulating dead sidecars from manually-deleted screenshots.

## Atomic writes

PNG injection writes to `<target>.tmp`, then atomic-renames over the target. A
crash mid-write leaves both original and `.tmp` on disk; recovery detects the
orphan `.tmp` and deletes it.

## Worker shutdown contract

On `Plugin.Dispose`:

1. Signal cancellation token.
2. Wait up to 5 seconds for the worker's current file to complete.
3. If still running, abandon — pending sidecars survive on disk for the next
   launch's recovery scan.

## What the pipeline does NOT do

- Backfill metadata into pre-existing files. The state isn't recoverable.
- Process files outside monitored directories.
- Modify files that already contain a current-version chunk (unless re-tagging
  is explicitly requested in a future version).
- Run multiple writers in parallel.
