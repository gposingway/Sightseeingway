using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace Sightseeingway.Gear
{
    /// <summary>
    /// Watches the player's visible gear and keeps Shadingway in sync with a minimum
    /// of work:
    ///
    /// - <b>On connect</b> (first discovery, or Shadingway restarts — detected by a
    ///   changed pid) it pushes the full current set, because the bus is empty.
    /// - <b>On change</b> it pushes only the slot(s) that actually changed, and
    ///   deletes slots that emptied — unchanged slots are never re-sent.
    /// - A slow heartbeat re-checks the connection so a Shadingway that starts (or
    ///   restarts) after the plugin gets the full set without a gear change.
    ///
    /// The framework-thread poll only reads gear + computes a change signature; all
    /// building and HTTP runs on a worker, serialized behind a gate.
    /// </summary>
    public sealed class GearPublisher : IDisposable
    {
        private const long PollIntervalMs = 750;
        private const long SyncIntervalMs = 5000; // periodic connect/retry, independent of gear changes
        private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(500);

        private static readonly GlamTextureKind[] AllKinds =
        {
            GlamTextureKind.Icon, GlamTextureKind.Name, GlamTextureKind.Rarity,
            GlamTextureKind.Dye1, GlamTextureKind.Dye2,
        };

        private readonly HttpClient _http;
        private readonly ShadingwayClient _client;
        private readonly CancellationTokenSource _cts = new();
        private readonly SemaphoreSlim _gate = new(1, 1);

        // Framework-thread-only state (OnUpdate runs solely on the framework thread).
        private long _lastPollTicks;
        private long _lastSyncTicks;
        private string _lastSignature = "";
        private DateTime _lastChangeUtc = DateTime.MinValue;
        private bool _dirty;
        private bool _wasEnabled;

        // Cross-thread state.
        private volatile bool _publishing;
        private volatile bool _probing;
        private volatile bool _forceFull; // set by the UI "Re-publish now" button
        private volatile int _pushedCount;
        private volatile string _statusLine = "Idle";

        // Worker-owned state — touched only while holding _gate.
        private readonly Dictionary<string, string> _publishedSlots = new(); // slot key → published signature
        private readonly HashSet<string> _publishedNames = new();            // resident texture names on the bus
        private bool _wasConnected;                                          // were we connected last cycle?
        private int? _connectedPid;                                          // pid we last synced with

        public string StatusLine => _statusLine;
        public bool ShadingwayDetected => _client.LastDiscoveryOk;
        public int? DiscoveredPort => _client.DiscoveredPort;
        public int PushedCount => _pushedCount;

        public GearPublisher()
        {
            // Bypass any system/WinHTTP proxy — it can hijack or break loopback requests,
            // which is a common reason a local /hello probe silently fails.
            _http = new HttpClient(new HttpClientHandler { UseProxy = false })
            {
                Timeout = TimeSpan.FromSeconds(5),
            };
            _client = new ShadingwayClient(_http);
            Plugin.Framework.Update += OnUpdate;
        }

        /// <summary>
        /// Checks for Shadingway without publishing (drives the Gear tab's live status).
        /// Safe to call fire-and-forget; coalesced so overlapping calls are cheap.
        /// </summary>
        public async Task ProbeAsync()
        {
            if (_probing || _publishing) return;
            _probing = true;
            try { await _client.DiscoverAsync(Plugin.Config.GearShadingwayPort, _cts.Token); }
            catch (Exception ex) { Plugin.Logger?.Debug($"Shadingway probe failed: {ex.Message}"); }
            finally { _probing = false; }
        }

        /// <summary>Forces the next cycle to re-send the full current set (manual test trigger).</summary>
        public void RequestResync()
        {
            _forceFull = true;
            _lastSyncTicks = 0; // make the next poll trigger immediately
        }

        private void OnUpdate(IFramework framework)
        {
            var enabled = Plugin.Config.GearPublishEnabled;
            // Rising edge (re-enabled): force the next cycle to treat gear as changed and
            // to re-sync the connection immediately.
            if (enabled && !_wasEnabled)
            {
                _lastSignature = "\0reenabled";
                _lastSyncTicks = 0;
            }
            _wasEnabled = enabled;

            if (!enabled || _publishing) return;

            var now = Environment.TickCount64;
            if (now - _lastPollTicks < PollIntervalMs) return;
            _lastPollTicks = now;

            IReadOnlyList<GearSlotData> slots;
            try
            {
                slots = GearReader.ReadVisibleGear();
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"Gear poll failed: {ex.Message}");
                return;
            }

            var sig = GearSignature.Of(slots);
            if (sig != _lastSignature)
            {
                _lastSignature = sig;
                _lastChangeUtc = DateTime.UtcNow;
                _dirty = true;
            }

            var gearReady = _dirty && DateTime.UtcNow - _lastChangeUtc >= Debounce;
            var syncDue = now - _lastSyncTicks >= SyncIntervalMs;
            if (!gearReady && !syncDue) return;

            _dirty = false;
            _lastSyncTicks = now;
            _publishing = true;
            var captured = slots; // captured by value on the framework thread
            _ = Task.Run(async () =>
            {
                try { await PublishAsync(captured, _cts.Token); }
                catch (OperationCanceledException) { /* disposing */ }
                catch (Exception ex) { Plugin.Logger?.Debug($"Gear publish task failed: {ex.Message}"); }
                finally { _publishing = false; }
            });
        }

        private async Task PublishAsync(IReadOnlyList<GearSlotData> slots, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try { await PublishLockedAsync(slots, ct); }
            finally { _gate.Release(); }
        }

        private async Task PublishLockedAsync(IReadOnlyList<GearSlotData> slots, CancellationToken ct)
        {
            var baseUrl = await _client.DiscoverAsync(Plugin.Config.GearShadingwayPort, ct);
            if (baseUrl == null)
            {
                // Disconnected. Record it so the next successful connect re-sends the full
                // set — we can't assume the bus still holds our textures after a gap.
                _wasConnected = false;
                _statusLine = "Shadingway not found";
                return;
            }

            var pid = _client.DiscoveredPid;
            // Send all on a (re)connection: we were disconnected, or it's a new Shadingway
            // instance (changed pid), or a manual re-publish. Otherwise just the diff.
            var reconnect = !_wasConnected || pid != _connectedPid || _forceFull;
            _wasConnected = true;
            _connectedPid = pid;
            _forceFull = false;

            var current = new Dictionary<string, GearSlotData>(slots.Count);
            foreach (var s in slots) current[s.Slot.Key] = s;

            // Slots to (re)push: everything on reconnect, otherwise only new/changed signatures.
            var toPush = new List<GearSlotData>();
            foreach (var s in slots)
            {
                if (reconnect
                    || !_publishedSlots.TryGetValue(s.Slot.Key, out var prev)
                    || prev != s.Signature())
                {
                    toPush.Add(s);
                }
            }

            // Slots that were published but are no longer present → delete.
            var toRemove = _publishedSlots.Keys.Where(k => !current.ContainsKey(k)).ToList();

            if (toPush.Count == 0 && toRemove.Count == 0)
            {
                _pushedCount = _publishedNames.Count;
                _statusLine = reconnect ? $"In sync (pid {pid})" : "Up to date";
                return;
            }

            var ok = 0;
            var total = 0;
            foreach (var s in toPush)
            {
                var (slotOk, slotTotal) = await PublishSlotAsync(baseUrl, s, ct);
                ok += slotOk;
                total += slotTotal;
            }
            foreach (var key in toRemove) await RemoveSlotAsync(baseUrl, key, ct);

            _pushedCount = _publishedNames.Count;
            var scope = reconnect ? "full" : "delta";
            _statusLine = $"Sent {ok}/{total} textures · {toPush.Count} slot(s) {scope}, {toRemove.Count} removed";
            Plugin.Logger?.Debug($"Gear publish ({scope}): {ok}/{total} POSTs ok across {toPush.Count} slot(s).");
        }

        /// <summary>
        /// Builds and pushes one slot's textures, clearing any of its now-stale names.
        /// Returns (successful POSTs, attempted POSTs).
        /// </summary>
        private async Task<(int Ok, int Total)> PublishSlotAsync(string baseUrl, GearSlotData slot, CancellationToken ct)
        {
            var built = new List<PushTexture>();
            var newNames = new HashSet<string>();

            var icon = await IconTexture.ReadAsync(slot.IconId);
            if (icon != null)
                Stage(built, newNames, TextureNaming.For(slot.Slot, GlamTextureKind.Icon), icon);

            var name = NameTexture.Render(slot.Name);
            if (name != null)
                Stage(built, newNames, TextureNaming.For(slot.Slot, GlamTextureKind.Name), name);

            Stage(built, newNames, TextureNaming.For(slot.Slot, GlamTextureKind.Rarity),
                Swatch(SwatchFactory.RaritySwatch(slot.Rarity)));

            if (slot.Stain0Color != 0)
                Stage(built, newNames, TextureNaming.For(slot.Slot, GlamTextureKind.Dye1),
                    Swatch(SwatchFactory.StainSwatch(slot.Stain0Color)));

            if (slot.Stain1Color != 0)
                Stage(built, newNames, TextureNaming.For(slot.Slot, GlamTextureKind.Dye2),
                    Swatch(SwatchFactory.StainSwatch(slot.Stain1Color)));

            var ok = 0;
            foreach (var t in built)
                if (await _client.PostTextureAsync(baseUrl, t, ct)) ok++;

            // Delete any of this slot's names that are no longer present (e.g. a removed dye),
            // then make _publishedNames reflect exactly this slot's current set.
            foreach (var kind in AllKinds)
            {
                var n = TextureNaming.For(slot.Slot, kind);
                if (!newNames.Contains(n) && _publishedNames.Contains(n))
                    await _client.DeleteTextureAsync(baseUrl, n, ct);
                _publishedNames.Remove(n);
            }
            foreach (var n in newNames) _publishedNames.Add(n);

            _publishedSlots[slot.Slot.Key] = slot.Signature();
            return (ok, built.Count);
        }

        /// <summary>Deletes everything for a slot that is no longer worn.</summary>
        private async Task RemoveSlotAsync(string baseUrl, string slotKey, CancellationToken ct)
        {
            foreach (var s in GlamSlots.All)
            {
                if (s.Key != slotKey) continue;
                foreach (var kind in AllKinds)
                {
                    var n = TextureNaming.For(s, kind);
                    if (_publishedNames.Remove(n))
                        await _client.DeleteTextureAsync(baseUrl, n, ct);
                }
                break;
            }
            _publishedSlots.Remove(slotKey);
        }

        /// <summary>Removes everything this publisher owns from the bus (disable/logout).</summary>
        public async Task FlushAsync()
        {
            try { await _gate.WaitAsync(_cts.Token); }
            catch (Exception) { return; } // disposed/cancelled — nothing to flush

            try
            {
                var baseUrl = await _client.DiscoverAsync(Plugin.Config.GearShadingwayPort, _cts.Token);
                if (baseUrl != null)
                    foreach (var n in _publishedNames.ToList())
                        await _client.DeleteTextureAsync(baseUrl, n, _cts.Token);
            }
            catch (Exception ex) { Plugin.Logger?.Debug($"Gear flush failed: {ex.Message}"); }
            finally
            {
                _publishedNames.Clear();
                _publishedSlots.Clear();
                _connectedPid = null;
                _wasConnected = false; // a later re-enable re-sends everything
                _pushedCount = 0;
                _statusLine = "Cleared";
                _gate.Release();
            }
        }

        private static RawTexture Swatch(byte[] rgba)
            => new("rgba8", SwatchFactory.SwatchSize, SwatchFactory.SwatchSize, rgba);

        private static void Stage(List<PushTexture> list, HashSet<string> names, string name, RawTexture tex)
        {
            list.Add(new PushTexture(name, tex));
            names.Add(name);
        }

        public void Dispose()
        {
            // Stop new work, signal cancellation, then release resources. An in-flight
            // worker observes the token at its next await; any HTTP call racing disposal
            // surfaces as a cancellation/ObjectDisposedException that the worker swallows.
            Plugin.Framework.Update -= OnUpdate;
            _cts.Cancel();
            _http.Dispose();
            _gate.Dispose();
            _cts.Dispose();
        }
    }
}
