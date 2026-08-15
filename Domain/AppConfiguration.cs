using System;
using System.Collections.Generic;
using System.Linq;

namespace proxifyre_tray
{
    /// <summary>
    /// Domain model for the app's proxy configuration - the shape the presenter works with
    /// at runtime. Deliberately decoupled from the on-disk JSON format (<see cref="Configuration"/>,
    /// a persistence DTO) and, just as deliberately, never handed to the view - the view only
    /// ever sees <see cref="ConfigurationView"/>. Nothing outside the Infrastructure/Application
    /// projects should reference it; that's enforced by neither of the other projects (View,
    /// proxifyre-tray) referencing this one, not by access modifiers.
    /// </summary>
    public sealed class AppConfiguration
    {
        /// <summary>Values ProxiFyre.exe itself understands for its log level. Instance (not static)
        /// property so it flows through to <see cref="ConfigurationView.ValidLogLevels"/> as a plain
        /// generated property copy, rather than needing hand-written mapping code for one static member.</summary>
        public IReadOnlyList<string> ValidLogLevels { get; } = new[] { "None", "Info", "Deb", "All" };

        /// <summary>Assigning anything not in <see cref="ValidLogLevels"/> - including blank - is silently
        /// ignored, leaving the previous value in place, the same enforcement <see cref="ProxyConfiguration.Endpoint"/>
        /// applies to itself. Starts empty (never null) via the property initializer, which sets the
        /// compiler-backed field directly and so bypasses this setter.</summary>
        public string LogLevel
        {
            get;
            set
            {
                if (ValidLogLevels.Contains(value))
                {
                    field = value;
                }
            }
        } = string.Empty;

        public List<ProxyConfiguration> Proxies { get; set; } = new List<ProxyConfiguration>();
    }

    public sealed class ProxyConfiguration
    {
        /// <summary>Stable per-instance identity, so the presenter can be told "edit/delete this one"
        /// by the view without the view ever holding this object itself - see <see cref="ProxyView.Id"/>.</summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>The combined "ip:port" SOCKS5 endpoint - same shape as the on-disk format. Splitting it into
        /// separate fields for editing (two text boxes) is a view concern, not a domain one. Assigning anything
        /// that isn't a well-formed "host:port" pair (see <see cref="IsValidEndpoint"/>) - including blank - is
        /// silently ignored, leaving the previous value in place; so an invalid Endpoint can't be produced through
        /// this property at all, not just avoided by callers that remember to check first, and a mid-edit blank
        /// field can't wipe out an otherwise-good value. Starts empty (never null) via the property initializer,
        /// which sets the compiler-backed field directly and so bypasses this setter.</summary>
        public string Endpoint
        {
            get;
            set
            {
                if (IsValidEndpoint(value))
                {
                    field = value;
                }
            }
        } = string.Empty;

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

        /// <summary>Whether <paramref name="endpoint"/> is a well-formed "host:port" SOCKS5 endpoint -
        /// a colon separating a non-empty host from a valid port number (0-65535). Used by the
        /// <see cref="Endpoint"/> setter itself to reject invalid values; exposed publicly too so
        /// callers (e.g. the view, before it even raises an edit) can pre-check without needing to
        /// attempt an assignment first.</summary>
        public static bool IsValidEndpoint(string endpoint)
        {
            var separatorIndex = endpoint.IndexOf(':');
            if (separatorIndex <= 0 || separatorIndex == endpoint.Length - 1)
            {
                return false;
            }
            return ushort.TryParse(endpoint.Substring(separatorIndex + 1), out _);
        }
    }
}
