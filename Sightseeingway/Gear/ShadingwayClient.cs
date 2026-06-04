using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Sightseeingway.Gear
{
    /// <summary>
    /// Talks to Shadingway's loopback HTTP texture bus: discovery via <c>/hello</c>
    /// and POST/DELETE of named textures. Holds only a brief discovery cache.
    /// </summary>
    public sealed class ShadingwayClient
    {
        private readonly HttpClient _http;
        private int? _cachedPort;
        private string _api = "/api/v1";

        public ShadingwayClient(HttpClient http) => _http = http;

        public bool LastDiscoveryOk { get; private set; }
        public int? DiscoveredPort => _cachedPort;

        /// <summary>PID reported by the last successful /hello — changes if Shadingway restarts.</summary>
        public int? DiscoveredPid { get; private set; }

        private sealed class HelloResponse
        {
            [JsonProperty("pid")] public int Pid { get; set; }
            [JsonProperty("port")] public int Port { get; set; }
            [JsonProperty("api")] public string? Api { get; set; }
            [JsonProperty("capabilities")] public List<string>? Capabilities { get; set; }
        }

        /// <summary>
        /// Confirms a Shadingway in THIS game process that supports texture publishing.
        /// Returns its API base URL, or null if absent/unsuitable. Scans a small port
        /// window because Shadingway walks up when its default port is taken.
        /// </summary>
        public async Task<string?> DiscoverAsync(int startPort, CancellationToken ct)
        {
            var ports = new List<int>(9);
            if (_cachedPort is { } cp) ports.Add(cp);
            for (var p = startPort; p <= startPort + 7; p++)
                if (!ports.Contains(p)) ports.Add(p);

            foreach (var port in ports)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var resp = await _http.GetAsync($"http://127.0.0.1:{port}/hello", ct);
                    if (!resp.IsSuccessStatusCode) continue;

                    var json = await resp.Content.ReadAsStringAsync(ct);
                    var hello = JsonConvert.DeserializeObject<HelloResponse>(json);
                    if (hello == null) continue;
                    if (hello.Pid != Environment.ProcessId) continue;                       // a different client
                    if (hello.Capabilities?.Contains("textures.publish") != true) continue; // not a publisher

                    _cachedPort = hello.Port != 0 ? hello.Port : port;
                    _api = string.IsNullOrEmpty(hello.Api) ? "/api/v1" : hello.Api!;
                    DiscoveredPid = hello.Pid;
                    LastDiscoveryOk = true;
                    return $"http://127.0.0.1:{_cachedPort}{_api}";
                }
                catch (OperationCanceledException) { throw; }
                catch (ObjectDisposedException) { break; } // client disposed mid-scan
                catch (Exception ex)
                {
                    // connection refused / timeout / proxy error → try the next port
                    Plugin.Logger?.Debug($"/hello probe on :{port} failed: {ex.Message}");
                }
            }

            _cachedPort = null;
            DiscoveredPid = null;
            LastDiscoveryOk = false;
            return null;
        }

        public async Task<bool> PostTextureAsync(string baseUrl, PushTexture tex, CancellationToken ct)
        {
            try
            {
                var body = JsonConvert.SerializeObject(new
                {
                    name = tex.Name,
                    format = tex.Texture.Format,
                    width = tex.Texture.Width,
                    height = tex.Texture.Height,
                    data = Convert.ToBase64String(tex.Texture.Data),
                });
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var resp = await _http.PostAsync($"{baseUrl}/textures", content, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync(ct);
                    if (err.Length > 200) err = err.Substring(0, 200);
                    Plugin.Logger?.Debug($"POST {tex.Name} → {(int)resp.StatusCode}: {err}");
                    return false;
                }

                Plugin.Logger?.Debug(
                    $"POST {tex.Name} ({tex.Texture.Format} {tex.Texture.Width}x{tex.Texture.Height}, " +
                    $"{tex.Texture.Data.Length}B) → OK");
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"POST {tex.Name} failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Deletes a texture. Returns true only when the bus confirms it's gone — a
        /// 2xx, or a 404 (already absent). A transient failure returns false so the caller can
        /// keep the name tracked and retry, rather than stranding a stale texture on the bus.</summary>
        public async Task<bool> DeleteTextureAsync(string baseUrl, string name, CancellationToken ct)
        {
            try
            {
                using var resp = await _http.DeleteAsync($"{baseUrl}/textures/{name}", ct);
                if (resp.IsSuccessStatusCode || resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return true;
                Plugin.Logger?.Debug($"DELETE {name} → {(int)resp.StatusCode} (will retry)");
                return false;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"DELETE {name} failed: {ex.Message} (will retry)");
                return false;
            }
        }
    }
}
