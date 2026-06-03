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
    /// Watches the player's visible gear and publishes it to Shadingway as a set of
    /// per-slot primitive textures (icon, name-coverage, rarity + dye swatches).
    ///
    /// Cadence: a throttled framework-thread poll reads gear and computes a change
    /// signature; on change (after a short debounce) the build + HTTP push runs on a
    /// worker thread. Content is resident on the bus until overwritten or cleared,
    /// so there is no per-frame cost.
    /// </summary>
    public sealed class GearPublisher : IDisposable
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(750);
        private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(500);

        private readonly HttpClient _http;
        private readonly ShadingwayClient _client;
        private readonly CancellationTokenSource _cts = new();

        // Serializes all bus mutations (publish vs flush) so _pushedNames has a single writer.
        private readonly SemaphoreSlim _gate = new(1, 1);

        // Framework-thread-only state (OnUpdate runs solely on the framework thread).
        private long _lastPollTicks;
        private string _lastSignature = "";
        private DateTime _lastChangeUtc = DateTime.MinValue;
        private bool _dirty;
        private bool _wasEnabled;

        // Cross-thread state.
        private volatile bool _publishing;
        private volatile bool _probing;
        private volatile int _pushedCount;
        private volatile string _statusLine = "Idle";
        private readonly HashSet<string> _pushedNames = new(); // touched only while holding _gate

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

        private void OnUpdate(IFramework framework)
        {
            var enabled = Plugin.Config.GearPublishEnabled;
            // Rising edge (re-enabled): force the next poll to treat gear as changed so
            // we re-publish even if nothing about the outfit moved while disabled.
            if (enabled && !_wasEnabled) _lastSignature = "\0reenabled";
            _wasEnabled = enabled;

            if (!enabled || _publishing) return;

            var now = Environment.TickCount64;
            if (now - _lastPollTicks < PollInterval.TotalMilliseconds) return;
            _lastPollTicks = now;

            // Framework thread: read gear + compute the change signature.
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

            if (_dirty && DateTime.UtcNow - _lastChangeUtc >= Debounce)
            {
                _dirty = false;
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
        }

        private async Task PublishAsync(IReadOnlyList<GearSlotData> slots, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                await PublishLockedAsync(slots, ct);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task PublishLockedAsync(IReadOnlyList<GearSlotData> slots, CancellationToken ct)
        {
            var baseUrl = await _client.DiscoverAsync(Plugin.Config.GearShadingwayPort, ct);
            if (baseUrl == null)
            {
                _statusLine = "Shadingway not found";
                return;
            }

            var current = new HashSet<string>();
            var toPush = new List<PushTexture>();

            foreach (var slot in slots)
            {
                var icon = await IconTexture.ReadAsync(slot.IconId);
                if (icon != null)
                    Stage(toPush, current, TextureNaming.For(slot.Slot, GlamTextureKind.Icon), icon);

                var name = NameTexture.Render(slot.Name);
                if (name != null)
                    Stage(toPush, current, TextureNaming.For(slot.Slot, GlamTextureKind.Name), name);

                Stage(toPush, current, TextureNaming.For(slot.Slot, GlamTextureKind.Rarity),
                    Swatch(SwatchFactory.RaritySwatch(slot.Rarity)));

                if (slot.Stain0Color != 0)
                    Stage(toPush, current, TextureNaming.For(slot.Slot, GlamTextureKind.Dye1),
                        Swatch(SwatchFactory.StainSwatch(slot.Stain0Color)));

                if (slot.Stain1Color != 0)
                    Stage(toPush, current, TextureNaming.For(slot.Slot, GlamTextureKind.Dye2),
                        Swatch(SwatchFactory.StainSwatch(slot.Stain1Color)));
            }

            var ok = 0;
            foreach (var t in toPush)
                if (await _client.PostTextureAsync(baseUrl, t, ct)) ok++;

            // Clear any names we previously pushed for slots/dyes that are now gone.
            foreach (var stale in _pushedNames.Where(n => !current.Contains(n)).ToList())
                await _client.DeleteTextureAsync(baseUrl, stale, ct);

            _pushedNames.Clear();
            foreach (var n in current) _pushedNames.Add(n);
            _pushedCount = _pushedNames.Count;

            _statusLine = $"Pushed {ok}/{toPush.Count} textures · {slots.Count} slots";
        }

        /// <summary>Removes everything this publisher owns from the bus (disable/logout).</summary>
        public async Task FlushAsync()
        {
            try
            {
                await _gate.WaitAsync(_cts.Token);
            }
            catch (Exception) { return; } // disposed/cancelled — nothing to flush

            try
            {
                var baseUrl = await _client.DiscoverAsync(Plugin.Config.GearShadingwayPort, _cts.Token);
                if (baseUrl != null)
                    foreach (var n in _pushedNames.ToList())
                        await _client.DeleteTextureAsync(baseUrl, n, _cts.Token);
            }
            catch (Exception ex) { Plugin.Logger?.Debug($"Gear flush failed: {ex.Message}"); }
            finally
            {
                _pushedNames.Clear();
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
