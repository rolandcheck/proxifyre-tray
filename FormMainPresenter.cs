using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace proxifyre_tray
{
    /// <summary>
    /// Owns all the behaviour that used to live in FormMain: loading/saving the
    /// configuration, editing proxies and apps, launching and stopping the
    /// ProxiFyre process, and the "launch on startup" registry entry. It talks to
    /// the window exclusively through <see cref="IFormMainView"/>, so it never
    /// references System.Windows.Forms.
    /// </summary>
    internal sealed class FormMainPresenter
    {
        private static readonly string ProgramName = "ProxiFyre.exe";
        private static readonly string ProgramPath = AppContext.BaseDirectory + ProgramName;
        private static readonly string ConfigPath = AppContext.BaseDirectory + "app-config.json";
        private static readonly string[] LogLevels = { "None", "Info", "Deb", "All" };
        private const string StartupRegistryKeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

        // Configuration uses public fields rather than properties, so IncludeFields is required.
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly IFormMainView view;
        private readonly string productName;
        private readonly string executablePath;

        private Configuration configuration;
        private Configuration.Proxy proxy;
        private Process proxifyreProcess;

        public FormMainPresenter(IFormMainView view, string productName, string executablePath)
        {
            this.view = view;
            this.productName = productName;
            this.executablePath = executablePath;
        }

        public void Initialize()
        {
            if (!File.Exists(ProgramPath))
            {
                view.DisableAllControls();
                view.AppendOutput("Couldn't find " + ProgramName + Environment.NewLine + "proxifyre-tray.exe must be inside the ProxiFyre directory to work");
                view.ShowForm();
                return;
            }

            OnAbout();

            view.SetRunningState(false);
            view.SetLogLevels(LogLevels);
            view.StopEnabled = false;

            LoadConfig();

            var startupRegistryKey = Registry.CurrentUser.OpenSubKey(StartupRegistryKeyPath, true);
            var startupRegistryValue = (string)startupRegistryKey.GetValue(productName);
            if (startupRegistryValue == null)
            {
                view.StartupChecked = false;
            }
            else if (startupRegistryValue == executablePath)
            {
                view.StartupChecked = true;
                OnStart();
            }
            else
            {
                startupRegistryKey.DeleteValue(productName, false);
                view.StartupChecked = false;
            }

            if (!view.StartupChecked)
            {
                view.ShowForm();
            }
        }

        private void LoadConfig()
        {
            if (File.Exists(ConfigPath))
            {
                var configContent = string.Empty;
                try
                {
                    configContent = File.ReadAllText(ConfigPath);
                }
                catch (Exception ex)
                {
                    view.AppendOutput(Environment.NewLine + ex.Message);
                }
                // Mirror Newtonsoft's old leniency: an empty/unreadable file yields no configuration
                // rather than a JsonException, matching what happened when File.ReadAllText failed above.
                configuration = string.IsNullOrEmpty(configContent)
                    ? null
                    : JsonSerializer.Deserialize<Configuration>(configContent, JsonOptions);
                UpdateAll();
            }
            else
            {
                configuration = new Configuration { logLevel = LogLevels[0] };
            }
        }

        private void UpdateAll()
        {
            if (!string.IsNullOrEmpty(configuration.logLevel))
            {
                view.LogLevel = configuration.logLevel;
            }
            UpdateProxies(0);
        }

        private void UpdateProxies(int selectedIndex)
        {
            proxy = null;
            var items = new List<string>();

            if (configuration.proxies != null)
            {
                for (var index = 0; index < configuration.proxies.Count; index++)
                {
                    proxy = configuration.proxies[index];
                    if (!string.IsNullOrEmpty(proxy.socks5ProxyEndpoint))
                    {
                        items.Add(proxy.socks5ProxyEndpoint);
                    }
                    else
                    {
                        configuration.proxies.RemoveAt(index);
                        index--;
                    }
                }
            }

            view.SetProxyItems(items);

            if (items.Count > 0)
            {
                view.ProxySelectedIndex = items.Count <= selectedIndex ? items.Count - 1 : selectedIndex;
            }
            else
            {
                UpdateApps(0);
            }
        }

        private void UpdateApps(int selectedIndex)
        {
            var items = new List<string>();
            view.AppText = string.Empty;

            if (proxy != null && proxy.appNames != null)
            {
                items.AddRange(proxy.appNames);
            }

            view.SetAppItems(items);

            if (items.Count > 0)
            {
                view.AppSelectedIndex = items.Count <= selectedIndex ? items.Count - 1 : selectedIndex;
            }
        }

        public void OnProxySelectionChanged()
        {
            var items = view.ProxyItems;
            var selectedIndex = view.ProxySelectedIndex;
            var selectedText = view.ProxySelectedText;

            var skipDuplicateEntries = 0;
            for (var index = 0; index < selectedIndex; index++)
            {
                if (items[index] == selectedText)
                {
                    skipDuplicateEntries++;
                }
            }

            foreach (var candidate in configuration.proxies)
            {
                if (candidate.socks5ProxyEndpoint == selectedText)
                {
                    if (skipDuplicateEntries > 0)
                    {
                        skipDuplicateEntries--;
                        continue;
                    }
                    proxy = candidate;
                    break;
                }
            }

            UpdateApps(0);

            if (proxy.socks5ProxyEndpoint.Contains(":"))
            {
                var separatorIndex = proxy.socks5ProxyEndpoint.IndexOf(":");
                view.IpText = proxy.socks5ProxyEndpoint.Substring(0, separatorIndex);
                view.PortText = proxy.socks5ProxyEndpoint.Substring(separatorIndex + 1);
            }
            view.UsernameText = proxy.username;
            view.PasswordText = proxy.password;
            view.TcpChecked = view.UdpChecked = false;
            if (proxy.supportedProtocols != null)
            {
                foreach (var supportedProtocol in proxy.supportedProtocols)
                {
                    if (supportedProtocol == "TCP")
                    {
                        view.TcpChecked = true;
                    }
                    if (supportedProtocol == "UDP")
                    {
                        view.UdpChecked = true;
                    }
                }
            }
        }

        public void OnAppSelectionChanged()
        {
            if (proxy != null)
            {
                view.AppText = proxy.appNames[view.AppSelectedIndex];
            }
        }

        public void OnProxyAdd()
        {
            if (configuration.proxies == null)
            {
                configuration.proxies = new List<Configuration.Proxy>();
            }
            configuration.proxies.Add(new Configuration.Proxy
            {
                socks5ProxyEndpoint = "127.0.0.1:1080",
                appNames = new List<string>(),
                supportedProtocols = new List<string> { "TCP", "UDP" }
            });
            UpdateProxies(configuration.proxies.Count - 1);
        }

        public void OnProxyDelete()
        {
            configuration.proxies.Remove(proxy);
            UpdateProxies(view.ProxySelectedIndex);
        }

        public void OnAppBrowse()
        {
            var fileName = view.BrowseForAppFile();
            if (proxy == null || string.IsNullOrEmpty(fileName) || view.AppItems.Contains(fileName))
            {
                return;
            }
            if (proxy.appNames == null)
            {
                proxy.appNames = new List<string>();
            }
            proxy.appNames.Add(fileName);
            UpdateApps(proxy.appNames.Count - 1);
        }

        public void OnAppAdd()
        {
            var appText = view.AppText;
            if (proxy == null || string.IsNullOrEmpty(appText) || view.AppItems.Contains(appText))
            {
                return;
            }
            if (proxy.appNames == null)
            {
                proxy.appNames = new List<string>();
            }
            proxy.appNames.Add(appText);
            UpdateApps(proxy.appNames.Count - 1);
        }

        public void OnAppDelete()
        {
            if (proxy == null)
            {
                return;
            }
            proxy.appNames.Remove(view.AppSelectedText);
            UpdateApps(view.AppSelectedIndex);
        }

        private void SetSocks5ProxyEndpoint()
        {
            if (!string.IsNullOrEmpty(view.IpText) && !string.IsNullOrEmpty(view.PortText))
            {
                proxy.socks5ProxyEndpoint = view.IpText + ":" + view.PortText;
                view.ReplaceProxyItem(view.ProxySelectedIndex, proxy.socks5ProxyEndpoint);
            }
        }

        public void OnIpValidated()
        {
            if (proxy != null)
            {
                SetSocks5ProxyEndpoint();
            }
        }

        public void OnPortValidated()
        {
            if (proxy != null)
            {
                SetSocks5ProxyEndpoint();
            }
        }

        public void OnUsernameValidated()
        {
            if (proxy != null)
            {
                proxy.username = view.UsernameText;
            }
        }

        public void OnPasswordValidated()
        {
            if (proxy != null)
            {
                proxy.password = view.PasswordText;
            }
        }

        private void SetSupportedProtocols(bool isChecked, string protocol)
        {
            if (proxy.supportedProtocols == null)
            {
                proxy.supportedProtocols = new List<string>();
            }
            if (isChecked)
            {
                if (!proxy.supportedProtocols.Contains(protocol))
                {
                    proxy.supportedProtocols.Add(protocol);
                    proxy.supportedProtocols.Sort();
                }
            }
            else
            {
                proxy.supportedProtocols.Remove(protocol);
            }
        }

        public void OnTcpValidated()
        {
            if (proxy != null)
            {
                SetSupportedProtocols(view.TcpChecked, "TCP");
            }
        }

        public void OnUdpValidated()
        {
            if (proxy != null)
            {
                SetSupportedProtocols(view.UdpChecked, "UDP");
            }
        }

        public void OnLogLevelValidated()
        {
            configuration.logLevel = view.LogLevel;
        }

        public void OnSave()
        {
            var content = JsonSerializer.Serialize(configuration, JsonOptions);

            try
            {
                File.WriteAllText(ConfigPath, content);
                view.AppendOutput(Environment.NewLine + "Configuration file saved");
            }
            catch (Exception ex)
            {
                view.AppendOutput(Environment.NewLine + ex.Message);
            }
        }

        public void OnStart()
        {
            OnSave();

            if (!File.Exists(ProgramPath))
            {
                return;
            }

            if (proxifyreProcess == null)
            {
                view.AppendOutput(Environment.NewLine + "Starting ProxiFyre");
            }
            else
            {
                try
                {
                    if (Process.GetProcessById(proxifyreProcess.Id) != null)
                    {
                        OnStop();
                    }
                }
                catch (Exception)
                {
                    view.AppendOutput(Environment.NewLine + "Starting ProxiFyre");
                }
            }

            proxifyreProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ProgramPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            proxifyreProcess.OutputDataReceived += (sender, e) => view.AppendOutput(Environment.NewLine + e.Data);

            try
            {
                proxifyreProcess.Start();
                proxifyreProcess.BeginOutputReadLine();
                proxifyreProcess.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                view.AppendOutput(Environment.NewLine + ex.Message);
            }

            view.SetRunningState(true);
            view.StopEnabled = true;
        }

        public void OnStop()
        {
            if (proxifyreProcess == null)
            {
                return;
            }

            view.AppendOutput(Environment.NewLine + "Stopping ProxiFyre");
            proxifyreProcess.Kill();
            proxifyreProcess.Dispose();

            view.SetRunningState(false);
            view.StopEnabled = false;
        }

        public void OnCtrlC()
        {
            if (view.StopEnabled)
            {
                OnStop();
            }
        }

        public void OnStartupToggle()
        {
            var startupRegistryKey = Registry.CurrentUser.OpenSubKey(StartupRegistryKeyPath, true);
            if (!view.StartupChecked)
            {
                startupRegistryKey.SetValue(productName, executablePath);
                view.StartupChecked = true;
            }
            else
            {
                startupRegistryKey.DeleteValue(productName, false);
                view.StartupChecked = false;
            }
        }

        public void OnAbout()
        {
            view.AppendOutput(Environment.NewLine + "ProxiFyre configuration utility and tray launcher thing" + Environment.NewLine
                + "proxifyre-tray by airenelias https://github.com/airenelias/proxifyre-tray" + Environment.NewLine
                + "Icons by Icons8 https://icons8.com");
        }

        public void OnLinkClicked(string link)
        {
            Process.Start(link);
        }

        /// <summary>Whether a user-initiated close should be turned into a hide instead.</summary>
        public bool ShouldCancelClose(bool userClosing)
        {
            return userClosing && File.Exists(ProgramPath);
        }

        public void OnFormClosed()
        {
            if (proxifyreProcess == null)
            {
                return;
            }
            try
            {
                if (Process.GetProcessById(proxifyreProcess.Id) != null)
                {
                    proxifyreProcess.Kill();
                    proxifyreProcess.Dispose();
                }
            }
            catch (Exception)
            {
                // Process already gone - nothing to clean up.
            }
        }
    }
}
