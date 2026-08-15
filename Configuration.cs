using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace proxifyre_tray
{
    /// <summary>
    /// Persistence DTO matching app-config.json's on-disk shape exactly (including its
    /// combined "ip:port" endpoint string and TCP/UDP-as-string-list encoding). Converts
    /// to/from the runtime domain model, <see cref="AppConfiguration"/>, via
    /// <see cref="ConfigurationMapper"/>; nothing outside that mapper and the presenter's
    /// load/save code should need to touch this class.
    /// </summary>
    public class Configuration
    {
        public class Proxy
        {
            [JsonPropertyName("appNames")]
            public List<string>? AppNames { get; set; }

            [JsonPropertyName("socks5ProxyEndpoint")]
            public string? Socks5ProxyEndpoint { get; set; }

            [JsonPropertyName("username")]
            public string? Username { get; set; }

            [JsonPropertyName("password")]
            public string? Password { get; set; }

            [JsonPropertyName("supportedProtocols")]
            public List<string>? SupportedProtocols { get; set; }
        }

        [JsonPropertyName("logLevel")]
        public string? LogLevel { get; set; }

        [JsonPropertyName("proxies")]
        public List<Proxy>? Proxies { get; set; }
    }
}
