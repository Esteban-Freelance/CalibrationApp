using CalibrationDevices.Interfaces;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace CalibrationDevices.Devices.Real
{
    public class MicrochipRelayCard : ISwitchDevice
    {
        private readonly RelayCardConfig _config;
        private readonly HttpClient _http;
        private DeviceStatus _status = DeviceStatus.Disconnected;
        private string? _indexPageSource;

        // ──────────────────────────────────────────────
        //  HTTP ENDPOINTS — update after running probe
        // ──────────────────────────────────────────────
        private string _statusEndpoint = "/status.xml";
        private string _relayOnEndpoint = "/forms.htm?rel1=1";
        
        private string _relayOffEndpoint = "/forms.htm?rel1=0";

        private static readonly string[] CandidateStatusEndpoints =
        [
            "/status.xml", "/vars.xml", "/measure.xml", "/status.cgi",
        "/vars.cgi", "/io.cgi", "/data.xml", "/adc.xml",
        "/index.htm", "/index.html"
        ];

        private static readonly string[] CandidateRelayEndpoints =
        [
            "/relay.cgi", "/io.cgi", "/control.cgi", "/output.cgi", "/toggle.cgi"
        ];

        // ──────────────────────────────────────────────
        //  IDevice properties
        // ──────────────────────────────────────────────

        public string Id { get; }
        public string Name { get; }
        public string DeviceType => "RelayCard";
        public DeviceStatus Status => _status;

        public MicrochipRelayCard(string id, string name, RelayCardConfig config)
        {
            Id = id;
            Name = name;
            _config = config;
            _http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(config.TimeoutMs) };
        }

        // ──────────────────────────────────────────────
        //  IDevice methods
        // ──────────────────────────────────────────────

        public async Task<bool> ConnectAsync()
        {
            _status = DeviceStatus.Connecting;
            try
            {
                var response = await _http.GetAsync($"{_config.BaseUrl}/");
                if (response.IsSuccessStatusCode)
                {
                    _indexPageSource = await response.Content.ReadAsStringAsync();
                    AutoDiscoverEndpoints(_indexPageSource);
                    _status = DeviceStatus.Connected;
                    return true;
                }
                _status = DeviceStatus.Error;
                return false;
            }
            catch
            {
                _status = DeviceStatus.Error;
                return false;
            }
        }

        public Task DisconnectAsync()
        {
            _status = DeviceStatus.Disconnected;
            return Task.CompletedTask;
        }

        public async Task ResetAsync()
        {
            await OpenAllChannelsAsync();
        }

        public async Task<string> GetIdentificationAsync()
        {
            try
            {
                string page = _indexPageSource ?? await _http.GetStringAsync($"{_config.BaseUrl}/");

                var titleMatch = Regex.Match(page, @"<title>(.*?)</title>", RegexOptions.IgnoreCase);
                var versionMatch = Regex.Match(page, @"(?:Stack|Software)\s*Version[:\s]*(v[\d.]+)", RegexOptions.IgnoreCase);
                var buildMatch = Regex.Match(page, @"Build\s*Date[:\s]*([A-Za-z]+\s+\d+\s+\d+)", RegexOptions.IgnoreCase);

                string title = titleMatch.Success ? titleMatch.Groups[1].Value : "Relay Card";
                string version = versionMatch.Success ? versionMatch.Groups[1].Value : "unknown";
                string build = buildMatch.Success ? buildMatch.Groups[1].Value : "unknown";

                return $"MICROCHIP,{title},FW:{version},Build:{build}";
            }
            catch (Exception ex)
            {
                return $"MICROCHIP,RelayCard,Error:{ex.Message}";
            }
        }

        // ──────────────────────────────────────────────
        //  ISwitchDevice methods
        // ──────────────────────────────────────────────

        public async Task SetChannelAsync(int channel)
        {
            if (_status != DeviceStatus.Connected)
                throw new InvalidOperationException("Device not connected");

            string endpoint = channel >= 1 ? _relayOnEndpoint : _relayOffEndpoint;
            var url = $"{_config.BaseUrl}{endpoint}";
            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();
        }

        public async Task SetChannelAsync(string routeName)
        {
            switch (routeName.ToUpper())
            {
                case "ON": case "CLOSED": case "1": await SetChannelAsync(1); break;
                case "OFF": case "OPEN": case "0": await SetChannelAsync(0); break;
                default: throw new ArgumentException($"Unknown route: {routeName}. Use ON/OFF.");
            }
        }

        public async Task<int> GetCurrentChannelAsync()
        {
            if (_status != DeviceStatus.Connected)
                throw new InvalidOperationException("Device not connected");

            try
            {
                string data = await _http.GetStringAsync($"{_config.BaseUrl}{_statusEndpoint}");

                var match = Regex.Match(data, @"[Rr]el[^<>]*[>:\s]*(ON|OFF|1|0|true|false)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string val = match.Groups[1].Value.ToUpper();
                    return val is "ON" or "1" or "TRUE" ? 1 : 0;
                }
            }
            catch { }

            return -1; // Unknown
        }

        public async Task OpenAllChannelsAsync()
        {
            await SetChannelAsync(0);
        }

        public Task<IEnumerable<SwitchRoute>> GetAvailableRoutesAsync()
        {
            return Task.FromResult<IEnumerable<SwitchRoute>>(new List<SwitchRoute>
        {
            new() { Name = "ON",  Channel = 1, Description = "Relay closed (ON)" },
            new() { Name = "OFF", Channel = 0, Description = "Relay open (OFF)" }
        });
        }

        // ──────────────────────────────────────────────
        //  Helpers (not part of the interface)
        // ──────────────────────────────────────────────

        /// <summary>Raw HTTP GET — useful for debugging.</summary>
        public async Task<string> RawGet(string path)
        {
            return await _http.GetStringAsync($"{_config.BaseUrl}{path}");
        }

        /// <summary>
        /// Probes common Microchip TCP/IP Stack endpoints to discover the API.
        /// Run this once to find the correct URLs for your firmware version.
        /// </summary>
        public async Task ProbeEndpoints()
        {
            Console.WriteLine("\n  Probing status endpoints:");
            foreach (var ep in CandidateStatusEndpoints)
            {
                try
                {
                    var r = await _http.GetAsync($"{_config.BaseUrl}{ep}");
                    if (r.IsSuccessStatusCode)
                    {
                        string body = await r.Content.ReadAsStringAsync();
                        string preview = body.Length > 100 ? body[..100].Replace("\r", "").Replace("\n", " ") + "..." : body.Replace("\r", "").Replace("\n", " ");
                        Console.WriteLine($"    ✓ {ep,-20} → {preview}");
                    }
                    else
                        Console.WriteLine($"    ✗ {ep,-20} → {r.StatusCode}");
                }
                catch { Console.WriteLine($"    ✗ {ep,-20} → timeout"); }
            }

            Console.WriteLine("\n  Probing relay endpoints (base path only — not toggling):");
            foreach (var ep in CandidateRelayEndpoints)
            {
                try
                {
                    var r = await _http.GetAsync($"{_config.BaseUrl}{ep}");
                    Console.WriteLine(r.IsSuccessStatusCode
                        ? $"    ✓ {ep,-20} → responds"
                        : $"    ? {ep,-20} → {r.StatusCode}");
                }
                catch { Console.WriteLine($"    ✗ {ep,-20} → timeout"); }
            }

            if (_indexPageSource != null)
            {
                Console.WriteLine("\n  URLs found in HTML source:");
                var matches = Regex.Matches(_indexPageSource,
                    @"(?:href|action|src)\s*=\s*[""']([^""']+\.(?:cgi|xml|htm)(?:\?[^""']*)?)[""']",
                    RegexOptions.IgnoreCase);
                foreach (Match m in matches)
                    Console.WriteLine($"    → {m.Groups[1].Value}");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// Auto-discovers endpoints by parsing the HTML source for .xml and .cgi references.
        /// </summary>
        private void AutoDiscoverEndpoints(string html)
        {
            var xmlMatch = Regex.Match(html, @"[""']([^""']+\.xml)[""']", RegexOptions.IgnoreCase);
            if (xmlMatch.Success)
            {
                _statusEndpoint = xmlMatch.Groups[1].Value;
                if (!_statusEndpoint.StartsWith("/")) _statusEndpoint = "/" + _statusEndpoint;
            }

            var onMatch = Regex.Match(html, @"[""']([^""']*(?:relay|io)[^""']*(?:=\s*(?:1|on|ON))[^""']*)[""']", RegexOptions.IgnoreCase);
            if (onMatch.Success)
            {
                _relayOnEndpoint = onMatch.Groups[1].Value;
                if (!_relayOnEndpoint.StartsWith("/")) _relayOnEndpoint = "/" + _relayOnEndpoint;
            }

            var offMatch = Regex.Match(html, @"[""']([^""']*(?:relay|io)[^""']*(?:=\s*(?:0|off|OFF))[^""']*)[""']", RegexOptions.IgnoreCase);
            if (offMatch.Success)
            {
                _relayOffEndpoint = offMatch.Groups[1].Value;
                if (!_relayOffEndpoint.StartsWith("/")) _relayOffEndpoint = "/" + _relayOffEndpoint;
            }
        }
    }
}

public class RelayCardConfig
{
    public string IpAddress { get; set; } = "";
    public int HttpPort { get; set; } = 80;
    public int TimeoutMs { get; set; } = 5000;
    public string BaseUrl => $"http://{IpAddress}:{HttpPort}";
}