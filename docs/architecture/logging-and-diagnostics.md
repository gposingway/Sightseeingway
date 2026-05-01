# Logging & Diagnostics Specification

**Status:** Locked.

## Channels

Two output channels, each with independent verbosity control:

### Channel 1 — Dalamud `IPluginLog`

Existing. Continues to receive `Debug`/`Information`/`Warning`/`Error` calls.
Survives via the shared `dalamud.log`. Verbosity follows the existing Dalamud
filter; nothing changes here.

### Channel 2 — Pipeline log (new)

Dedicated rolling text file written by a new `PipelineLog` companion class:

- **Location:** `PluginInterface.GetPluginConfigDirectory()/logs/`
- **Filename:** `pipeline-yyyy-MM-dd.log`
- **Rotation:** daily, retain last 7 days. Plus a soft cap at 10 MB per file
  (rollover to `pipeline-yyyy-MM-dd-NN.log` if exceeded).
- **Format:** structured plain text, one event per line:

```
2026-05-01T18:30:00.123 INFO  fsw.created             id=01902abc-d1e2-7234-9000-000000000000 path=ffxiv_001.png
2026-05-01T18:30:00.140 DEBUG state.capture.start     id=01902abc-d1e2-7234-9000-000000000000
2026-05-01T18:30:00.142 INFO  state.capture.complete  id=01902abc-d1e2-7234-9000-000000000000 duration_ms=2
2026-05-01T18:30:00.145 INFO  sidecar.write           id=01902abc-d1e2-7234-9000-000000000000 path=…sw-pending.json bytes=1024
2026-05-01T18:30:00.510 INFO  rename.complete         id=01902abc-d1e2-7234-9000-000000000000 from=ffxiv_001.png to=20260501-…png attempts=3
2026-05-01T18:30:01.450 INFO  injection.complete      id=01902abc-d1e2-7234-9000-000000000000 duration_ms=250 bytes_in=12_438_192 bytes_out=12_439_265
```

The pipeline log is written **regardless of `LogVerbosity` setting**. Verbosity
controls only what reaches chat. Disk telemetry is always rich.

## Correlation IDs

Generated via `Guid.CreateVersion7()` (.NET 9+, native, no library).

Generated **once** at the entry point of each screenshot's lifecycle
(`OnFileCreated` handler). Propagated via:

1. `StateSnapshot.CorrelationId` field.
2. Sidecar JSON (`correlationId` key).
3. Embedded payload (`correlationId` field — see schema doc).
4. Logger calls — every Logger method gains an optional
   `Guid? correlationId = null` parameter, formatted into the message prefix
   as `id=...`.
5. Pipeline log entries — first structured field after timestamp/level.

GUID v7 specifically (not v4) so logs sort lexicographically into chronological
order, and burst-capture events cluster lexicographically adjacent.

## Verbosity tiers

| Tier | Errors | Warnings | Status milestones | Pipeline trace |
|---|---|---|---|---|
| Quiet  | chat | log only | log only | log only |
| Status | chat | chat (`showInChat:true` sites) | chat | log only |
| Debug  | chat | chat | chat | chat |

"Status milestones" = `rename.complete`, `injection.complete`, plus existing
user-facing notifications (file renamed messages).

The `LogVerbosity` enum lives on `Configuration`; runtime cache on
`Plugin.LogVerbosity`. The existing `_debugMode` field on `Logger` becomes
`_chatVerbosity`. The `SetDebugMode(bool)` method is replaced with
`SetVerbosity(LogVerbosity)`.

## Event vocabulary

| Event | Level | Context fields |
|---|---|---|
| `fsw.created` | INFO | id, path |
| `state.capture.start` | DEBUG | id |
| `state.capture.complete` | INFO | id, duration_ms |
| `sidecar.write` | INFO | id, path, bytes |
| `sidecar.move` | INFO | id, from, to |
| `sidecar.delete` | INFO | id |
| `rename.attempt` | DEBUG | id, attempt |
| `rename.complete` | INFO | id, from, to, attempts |
| `rename.failed` | ERROR | id, from, to, exception |
| `wait.release.start` | DEBUG | id, path, file_access |
| `wait.release.success` | DEBUG | id, attempt |
| `wait.release.timeout` | WARN | id, path, attempts |
| `injection.start` | INFO | id, writer |
| `injection.complete` | INFO | id, duration_ms, bytes_in, bytes_out |
| `injection.failed` | ERROR | id, exception |
| `recovery.scan.start` | INFO | dirs |
| `recovery.scan.complete` | INFO | found, processed, orphans_cleaned |
| `worker.start` / `worker.stop` | INFO | (none) |
| `config.changed` | INFO | fields |

## Diagnostics panel data source

Backed by an in-memory ring buffer (`Queue<DiagnosticEvent>`, capped at 50
entries) inside the `PipelineLog` class. Every event written to disk is also
pushed to the ring buffer, oldest evicted. The Diagnostics panel in the config
window reads from this buffer on each draw.

## What we deliberately do NOT log

- Full `StateSnapshot` JSON in event lines — too verbose; payload is already
  in the sidecar at write time, and in the embedded chunk after injection.
- Per-iteration scan loops in the worker — only state changes.
- Successful no-op recovery sweeps — one summary line.
- The user's correlation IDs as `id=full-guid` more than once per line — no
  shorthand or repetition needed.
