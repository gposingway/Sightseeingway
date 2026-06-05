using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Sightseeingway.Gear; // ShadingwayClient, NameTexture, RawTexture, PushTexture, GlamFonts, TextureNaming

namespace Sightseeingway.CharacterCard
{
    /// <summary>
    /// Publishes the local character's identity + appearance to Shadingway as <c>CHAR_*</c>
    /// textures (name / identity labels, number labels) and uniforms (numeric + boolean customize
    /// options). The character is one logical entity that changes rarely, so this re-sends the
    /// whole set on any signature change rather than per-field delta. Reuses the gear pipeline
    /// primitives unchanged; only the read and the flat CHAR_* name set differ.
    /// </summary>
    public sealed class CharacterPublisher : IDisposable
    {
        private const long PollIntervalMs = 750;
        private const long SyncIntervalMs = 5000;
        private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(500);

        private readonly HttpClient _http;
        private readonly ShadingwayClient _client;
        private readonly CancellationTokenSource _cts = new();
        private readonly SemaphoreSlim _gate = new(1, 1);

        // Framework-thread-only state.
        private long _lastPollTicks;
        private long _lastSyncTicks;
        private string _lastSignature = "";
        private DateTime _lastChangeUtc = DateTime.MinValue;
        private bool _dirty;
        private bool _wasEnabled;

        // Cross-thread state.
        private volatile bool _publishing;
        private volatile bool _probing;
        private volatile bool _forceFull;
        private volatile int _pushedCount;
        private volatile string _statusLine = "Idle";

        // Worker-owned (under _gate).
        private readonly HashSet<string> _publishedNames = new();    // resident texture names
        private readonly HashSet<string> _publishedUniforms = new(); // resident uniform keys
        private string _publishedSignature = "";
        private bool _wasConnected;
        private int? _connectedPid;

        public string StatusLine => _statusLine;
        public bool ShadingwayDetected => _client.LastDiscoveryOk;
        public int? DiscoveredPort => _client.DiscoveredPort;
        public int PushedCount => _pushedCount;

        public CharacterPublisher()
        {
            _http = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(5) };
            _client = new ShadingwayClient(_http);
            Plugin.Framework.Update += OnUpdate;
        }

        /// <summary>Checks for Shadingway without publishing (drives the Character tab status).</summary>
        public async Task ProbeAsync()
        {
            if (_probing || _publishing) return;
            _probing = true;
            try { await _client.DiscoverAsync(Plugin.Config.GearShadingwayPort, _cts.Token); }
            catch (Exception ex) { Plugin.Logger?.Debug($"Shadingway probe failed: {ex.Message}"); }
            finally { _probing = false; }
        }

        /// <summary>Forces the next cycle to re-send the full current set.</summary>
        public void RequestResync()
        {
            _forceFull = true;
            _lastSyncTicks = 0;
        }

        private void OnUpdate(IFramework framework)
        {
            var enabled = Plugin.Config.CharacterPublishEnabled;
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

            CharSnapshot? snap;
            try { snap = CharReader.ReadSnapshot(); }
            catch (Exception ex) { Plugin.Logger?.Debug($"Char poll failed: {ex.Message}"); return; }

            if (snap == null) return; // unreliable read → hold the last published state

            // An empty (blank-name) snapshot means logged out → its empty signature drives a clear.
            var sig = string.IsNullOrEmpty(snap.Name) ? "\0empty" : snap.Signature();
            if (sig != _lastSignature)
            {
                _lastSignature = sig;
                _lastChangeUtc = DateTime.UtcNow;
                _dirty = true;
            }

            var ready = _dirty && DateTime.UtcNow - _lastChangeUtc >= Debounce;
            var syncDue = now - _lastSyncTicks >= SyncIntervalMs;
            if (!ready && !syncDue) return;

            _dirty = false;
            _lastSyncTicks = now;
            _publishing = true;
            var captured = snap;
            _ = Task.Run(async () =>
            {
                try { await PublishAsync(captured, _cts.Token); }
                catch (OperationCanceledException) { /* disposing */ }
                catch (Exception ex) { Plugin.Logger?.Debug($"Char publish task failed: {ex.Message}"); }
                finally { _publishing = false; }
            });
        }

        private async Task PublishAsync(CharSnapshot snap, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try { await PublishLockedAsync(snap, ct); }
            finally { _gate.Release(); }
        }

        private async Task PublishLockedAsync(CharSnapshot snap, CancellationToken ct)
        {
            var baseUrl = await _client.DiscoverAsync(Plugin.Config.GearShadingwayPort, ct);
            if (baseUrl == null)
            {
                _wasConnected = false;
                _statusLine = "Shadingway not found";
                return;
            }

            var pid = _client.DiscoveredPid;
            var reconnect = !_wasConnected || pid != _connectedPid || _forceFull;
            _wasConnected = true;
            _connectedPid = pid;
            _forceFull = false;

            // Logged out → clear everything we put on the bus.
            if (string.IsNullOrEmpty(snap.Name))
            {
                await ClearAllAsync(baseUrl, ct);
                _statusLine = "Cleared (logged out)";
                return;
            }

            // Up to date: same appearance and still the same connection → nothing to do.
            var sig = snap.Signature();
            if (!reconnect && sig == _publishedSignature)
            {
                _pushedCount = _publishedNames.Count + _publishedUniforms.Count;
                _statusLine = "Up to date";
                return;
            }

            var textures = BuildTextures(snap);
            var uniforms = BuildUniforms(snap);
            var newNames = textures.Select(t => t.Name).ToHashSet();
            var newKeys = uniforms.Select(u => u.Name).ToHashSet();

            // Overwrite-publish the whole set (POST is idempotent by name).
            var okT = 0;
            foreach (var t in textures)
                if (await _client.PostTextureAsync(baseUrl, t, ct)) okT++;
            var okU = 0;
            foreach (var u in uniforms)
                if (await _client.PostUniformAsync(baseUrl, u.Name, u.Values, ct)) okU++;

            // Remove anything previously published that's no longer in the set (e.g. GC left, a
            // now-empty label). Only forget on a confirmed delete so a failure is retried.
            foreach (var n in _publishedNames.Where(n => !newNames.Contains(n)).ToList())
                if (await _client.DeleteTextureAsync(baseUrl, n, ct)) _publishedNames.Remove(n);
            foreach (var k in _publishedUniforms.Where(k => !newKeys.Contains(k)).ToList())
                if (await _client.DeleteUniformAsync(baseUrl, k, ct)) _publishedUniforms.Remove(k);

            foreach (var n in newNames) _publishedNames.Add(n);
            foreach (var k in newKeys) _publishedUniforms.Add(k);

            // Only mark in-sync if everything actually landed, so a transient failure re-publishes.
            if (okT == textures.Count && okU == uniforms.Count) _publishedSignature = sig;
            else _publishedSignature = "";

            _pushedCount = _publishedNames.Count + _publishedUniforms.Count;
            _statusLine = $"Sent {okT}/{textures.Count} tex · {okU}/{uniforms.Count} uniforms{(reconnect ? " (full)" : "")}";
        }

        // ---- build ----

        private static List<PushTexture> BuildTextures(CharSnapshot snap)
        {
            var list = new List<PushTexture>();

            // Character name in each bundled font (CHAR_NAME0..3), 128px.
            for (var i = 0; i < TextureNaming.NameFontKeys.Length; i++)
                if (GlamFonts.Get(TextureNaming.NameFontKeys[i]) is { } font
                    && NameTexture.Render(snap.Name, font, TextureNaming.NameHeight) is { } tex)
                    list.Add(new PushTexture(CharNaming.Name(i), tex));

            // Identity: the value label (CHAR_RACE = "Hyur") + its caption (CHAR_RACE_LABEL =
            // "Race"), both 128px. Empty fields are omitted entirely (so they get cleaned up).
            AddLabeled(list, CharNaming.World, snap.HomeWorld);
            AddLabeled(list, CharNaming.CurrentWorld, snap.CurrentWorld);
            AddLabeled(list, CharNaming.DataCenter, snap.DataCenter);
            AddLabeled(list, CharNaming.Race, snap.RaceName);
            AddLabeled(list, CharNaming.Clan, snap.ClanName);
            AddLabeled(list, CharNaming.Gender, snap.GenderName);
            AddLabeled(list, CharNaming.Job, snap.JobName);
            AddLabeled(list, CharNaming.GcName, snap.GcName);
            AddLabeled(list, CharNaming.FcName, snap.FcName);
            AddLabeled(list, CharNaming.FcTag, snap.FcTag);

            // Numeric options: a small "<KEY>_NUM" value label + the "<KEY>_LABEL" caption (both
            // 28px), so a card can render "Face  3". The value also rides a uniform (slider/needle).
            foreach (var n in snap.Numbers)
            {
                AddLabel(list, CharNaming.NumberLabel(n.Key), n.Value.ToString(), TextureNaming.DyeNameHeight);
                // Live race-correct caption (CharaMakeType) wins; else the static fallback.
                var caption = n.Caption ?? CharCaptions.For(n.Key);
                if (!string.IsNullOrEmpty(caption))
                    AddLabel(list, CharCaptions.LabelName(n.Key), caption, TextureNaming.DyeNameHeight);
            }

            // Toggle options: just the caption — the on/off value is the uniform.
            foreach (var f in snap.Flags)
                AddCaption(list, f.Key, TextureNaming.DyeNameHeight);

            // Customize colours: an 8×8 RGB swatch (fill/sample) + a "C1R18" grid-position label
            // (what the player picked) + the caption. The cell [col,row] also rides a uniform.
            foreach (var col in snap.Colors)
            {
                list.Add(new PushTexture(col.Key, Swatch(col.R, col.G, col.B)));
                AddLabel(list, CharNaming.ColorPos(col.Key), $"C{col.Col}R{col.Row}", TextureNaming.DyeNameHeight);
                AddCaption(list, col.Key, TextureNaming.DyeNameHeight);
            }

            // Customize thumbnails — the creator icon per option (hairstyle/face-paint/face/…).
            foreach (var ic in snap.Icons)
                if (IconTexture.Read(ic.IconId) is { } tex)
                    list.Add(new PushTexture(ic.Key, tex));

            return list;
        }

        private static RawTexture Swatch(byte r, byte g, byte b)
            => new("rgba8", SwatchFactory.SwatchSize, SwatchFactory.SwatchSize,
                SwatchFactory.SolidRgba(SwatchFactory.SwatchSize, SwatchFactory.SwatchSize, r, g, b));

        /// <summary>Stages a value label plus its static option-name caption, both at 128px.</summary>
        private static void AddLabeled(List<PushTexture> list, string key, string value)
        {
            if (string.IsNullOrEmpty(value)) return; // no value → no row and no caption
            AddLabel(list, key, value, TextureNaming.NameHeight);
            AddCaption(list, key, TextureNaming.NameHeight);
        }

        /// <summary>Stages the <c>&lt;KEY&gt;_LABEL</c> caption text-texture for an option key
        /// (CHAR_FACE → "Face"), so a shader can show "caption: value" without rendering text.</summary>
        private static void AddCaption(List<PushTexture> list, string optionKey, int heightPx)
        {
            if (CharCaptions.For(optionKey) is { } caption)
                AddLabel(list, CharCaptions.LabelName(optionKey), caption, heightPx);
        }

        private static void AddLabel(List<PushTexture> list, string name, string text, int heightPx)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (GlamFonts.Get(TextureNaming.NameFontKeys[0]) is { } inter
                && NameTexture.Render(text, inter, heightPx) is { } tex)
                list.Add(new PushTexture(name, tex));
        }

        private static List<(string Name, float[] Values)> BuildUniforms(CharSnapshot snap)
        {
            var list = new List<(string, float[])>(snap.Numbers.Count + snap.Flags.Count + snap.Colors.Count);
            foreach (var n in snap.Numbers) list.Add((n.Key, new[] { (float)n.Value }));
            foreach (var f in snap.Flags) list.Add((f.Key, new[] { f.On ? 1f : 0f }));
            foreach (var col in snap.Colors) list.Add((CharNaming.ColorCell(col.Key), new[] { (float)col.Col, col.Row }));
            return list;
        }

        private async Task ClearAllAsync(string baseUrl, CancellationToken ct)
        {
            foreach (var n in _publishedNames.ToList())
                if (await _client.DeleteTextureAsync(baseUrl, n, ct)) _publishedNames.Remove(n);
            foreach (var k in _publishedUniforms.ToList())
                if (await _client.DeleteUniformAsync(baseUrl, k, ct)) _publishedUniforms.Remove(k);
            if (_publishedNames.Count == 0 && _publishedUniforms.Count == 0) _publishedSignature = "";
            _pushedCount = _publishedNames.Count + _publishedUniforms.Count;
        }

        /// <summary>Removes everything this publisher owns from the bus (disable/logout).</summary>
        public async Task FlushAsync()
        {
            try { await _gate.WaitAsync(_cts.Token); }
            catch (Exception) { return; }

            try
            {
                var baseUrl = await _client.DiscoverAsync(Plugin.Config.GearShadingwayPort, _cts.Token);
                if (baseUrl != null)
                {
                    foreach (var n in _publishedNames.ToList())
                        await _client.DeleteTextureAsync(baseUrl, n, _cts.Token);
                    foreach (var k in _publishedUniforms.ToList())
                        await _client.DeleteUniformAsync(baseUrl, k, _cts.Token);
                }
            }
            catch (Exception ex) { Plugin.Logger?.Debug($"Char flush failed: {ex.Message}"); }
            finally
            {
                _publishedNames.Clear();
                _publishedUniforms.Clear();
                _publishedSignature = "";
                _connectedPid = null;
                _wasConnected = false;
                _pushedCount = 0;
                _statusLine = "Cleared";
                _gate.Release();
            }
        }

        public void Dispose()
        {
            Plugin.Framework.Update -= OnUpdate;
            _cts.Cancel();
            _http.Dispose();
            _gate.Dispose();
            _cts.Dispose();
        }
    }
}
