using System.Collections.Generic;
using Riok.Mapperly.Abstractions;

namespace proxifyre_tray
{
    /// <summary>
    /// Mapperly-generated conversion between the on-disk JSON DTO (<see cref="Configuration"/>)
    /// and the runtime domain model (<see cref="AppConfiguration"/>). One thing Mapperly can't
    /// infer on its own - a TCP/UDP string list vs. two bools - so the per-proxy conversion is
    /// hand-written below as "user-implemented" methods; Mapperly detects them and uses them
    /// wherever a Proxy/ProxyConfiguration is needed, including inside the generated List&lt;&gt;
    /// mapping. Those same methods also fold the DTO's nullable strings down to the domain
    /// model's "always empty, never null" ones. For LogLevel, Mapperly generates a null-guarded
    /// copy on its own (only assigns when the source is non-null), which combined with
    /// AppConfiguration.LogLevel's "= string.Empty" default is enough - no hand-written help needed.
    /// </summary>
    [Mapper]
    public static partial class ConfigurationMapper
    {
        /// <summary>Maps the DTO to the runtime domain model.</summary>
        public static partial AppConfiguration ToDomain(this Configuration source);

        /// <summary>Maps the domain model back to the DTO for serialization. AppConfiguration.ValidLogLevels
        /// has no DTO counterpart - it's constant metadata about what LogLevel accepts, not persisted state.</summary>
        [MapperIgnoreSource(nameof(AppConfiguration.ValidLogLevels))]
        public static partial Configuration ToDto(this AppConfiguration source);

        private static ProxyConfiguration Map(Configuration.Proxy source)
        {
            return new ProxyConfiguration
            {
                Endpoint = source.Socks5ProxyEndpoint ?? string.Empty,
                Username = source.Username ?? string.Empty,
                Password = source.Password ?? string.Empty,
                Tcp = source.SupportedProtocols != null && source.SupportedProtocols.Contains("TCP"),
                Udp = source.SupportedProtocols != null && source.SupportedProtocols.Contains("UDP"),
                AppNames = source.AppNames != null ? new List<string>(source.AppNames) : new List<string>()
            };
        }

        private static Configuration.Proxy Map(ProxyConfiguration source)
        {
            var supportedProtocols = new List<string>();
            if (source.Tcp)
            {
                supportedProtocols.Add("TCP");
            }
            if (source.Udp)
            {
                supportedProtocols.Add("UDP");
            }
            return new Configuration.Proxy
            {
                Socks5ProxyEndpoint = source.Endpoint,
                Username = source.Username,
                Password = source.Password,
                SupportedProtocols = supportedProtocols,
                AppNames = new List<string>(source.AppNames)
            };
        }
    }
}
