using System;
using System.Collections.Generic;

namespace proxifyre_tray
{
    /// <summary>
    /// Domain model for the app's proxy configuration - the shape the presenter works with
    /// at runtime. Deliberately decoupled from the on-disk JSON format (<see cref="Configuration"/>,
    /// a persistence DTO) and, just as deliberately, never handed to the view - the view only
    /// ever sees <see cref="ConfigurationView"/>. Internal (not public) because of that: nothing
    /// outside the presenter should reference it.
    /// </summary>
    internal sealed class AppConfiguration
    {
        public string LogLevel { get; set; } = string.Empty;

        public List<ProxyConfiguration> Proxies { get; set; } = new List<ProxyConfiguration>();
    }

    internal sealed class ProxyConfiguration
    {
        /// <summary>Stable per-instance identity, so the presenter can be told "edit/delete this one"
        /// by the view without the view ever holding this object itself - see <see cref="ProxyView.Id"/>.</summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>The combined "ip:port" SOCKS5 endpoint - same shape as the on-disk format. Splitting it into
        /// separate fields for editing (two text boxes) is a view concern, not a domain one. Empty (never null)
        /// when unset, matching every other string field here.</summary>
        public string Endpoint { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool Tcp { get; set; }
        public bool Udp { get; set; }
        public List<string> AppNames { get; set; } = new List<string>();

        /// <summary>Sensible defaults for a freshly added proxy entry.</summary>
        public static ProxyConfiguration CreateDefault()
        {
            return new ProxyConfiguration
            {
                Endpoint = "127.0.0.1:1080",
                Tcp = true,
                Udp = true
            };
        }
    }
}
