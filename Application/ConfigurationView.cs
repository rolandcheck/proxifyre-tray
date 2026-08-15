using System;
using System.Collections.Generic;

namespace proxifyre_tray
{
    /// <summary>
    /// What the presenter hands the view to render - a read-only snapshot, never the
    /// domain model (<see cref="AppConfiguration"/>) itself. The view treats every edit
    /// as an intent to raise, identifying the proxy involved by <see cref="ProxyView.Id"/>
    /// rather than holding any domain object; the presenter resolves the id back to the
    /// real <see cref="ProxyConfiguration"/> and performs the actual mutation.
    /// </summary>
    public sealed class ConfigurationView
    {
        public string LogLevel { get; set; } = string.Empty;

        public List<ProxyView> Proxies { get; set; } = new List<ProxyView>();
    }

    public sealed class ProxyView
    {
        public Guid Id { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool Tcp { get; set; }
        public bool Udp { get; set; }
        public List<string> AppNames { get; set; } = new List<string>();
    }
}
