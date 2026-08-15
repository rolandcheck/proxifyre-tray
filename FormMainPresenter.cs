using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace proxifyre_tray
{
    /// <summary>
    /// Owns all mutation of the domain model (<see cref="AppConfiguration"/>): loading/saving
    /// the configuration, editing proxies and apps, launching and stopping the ProxiFyre
    /// process, and the "launch on startup" registry entry. The domain model itself never
    /// leaves this class - the view only ever sees a <see cref="ConfigurationView"/> snapshot,
    /// pushed via <see cref="IFormMainView.SetConfiguration"/> after every edit, and identifies
    /// which proxy an edit targets by <see cref="ProxyView.Id"/> rather than by reference. Talks
    /// to the window exclusively through <see cref="IFormMainView"/>, so it never references
    /// System.Windows.Forms.
    /// </summary>
    internal sealed class FormMainPresenter
    {
        private static readonly string ProgramName = "ProxiFyre.exe";
        private static readonly string ProgramPath = AppContext.BaseDirectory + ProgramName;
        private static readonly string ConfigPath = AppContext.BaseDirectory + "app-config.json";
        private static readonly string[] LogLevels = { "None", "Info", "Deb", "All" };
        private const string StartupRegistryKeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly IFormMainView view;
        private readonly string productName;
        private readonly string executablePath;

        // Assigned in LoadConfig, called from Initialize (not the constructor) - always non-null by
        // the time any event handler below can run, since nothing can fire before Initialize completes.
        private AppConfiguration configuration = null!;

        private Process? proxifyreProcess;

        public FormMainPresenter(IFormMainView view, string productName, string executablePath)
        {
            this.view = view;
            this.productName = productName;
            this.executablePath = executablePath;

            view.SaveRequested += (sender, e) => OnSave();
            view.StartRequested += (sender, e) => OnStart();
            view.StopRequested += (sender, e) => OnStop();
            view.StartupToggleRequested += (sender, e) => OnStartupToggle();
            view.AboutRequested += (sender, e) => OnAbout();
            view.LinkClicked += (sender, link) => OnLinkClicked(link);
            view.ViewClosing += (sender, e) => e.Cancel = ShouldCancelClose(e.UserClosing);
            view.ViewClosed += (sender, e) => OnFormClosed();

            view.ProxyAddRequested += (sender, e) => OnProxyAdd();
            view.ProxyDeleteRequested += (sender, proxyId) => OnProxyDelete(proxyId);
            view.AppAddRequested += (sender, e) => OnAppAdd(e.ProxyId, e.AppName);
            view.AppDeleteRequested += (sender, e) => OnAppDelete(e.ProxyId, e.AppName);
            view.ProxyFieldsEditRequested += (sender, e) => OnProxyFieldsEdit(e.ProxyId, e.Endpoint, e.Username, e.Password, e.Tcp, e.Udp);
            view.LogLevelEditRequested += (sender, logLevel) => OnLogLevelEdit(logLevel);
        }

        private ProxyConfiguration? FindProxy(Guid proxyId)
        {
            return configuration.Proxies.FirstOrDefault(proxy => proxy.Id == proxyId);
        }

        /// <summary>Pushes a fresh snapshot to the view after a mutation, asking it to keep (or move to) the given proxy selected.</summary>
        private void RefreshView(Guid? selectedProxyId)
        {
            view.SetConfiguration(configuration.ToView(), selectedProxyId);
        }

        private void OnProxyAdd()
        {
            var proxy = ProxyConfiguration.CreateDefault();
            configuration.Proxies.Add(proxy);
            RefreshView(proxy.Id);
        }

        private void OnProxyDelete(Guid proxyId)
        {
            var index = configuration.Proxies.FindIndex(proxy => proxy.Id == proxyId);
            if (index < 0)
            {
                return;
            }
            configuration.Proxies.RemoveAt(index);

            // Select whatever ends up at the same position, mirroring the old "nearest remaining item" behaviour.
            Guid? fallbackId = configuration.Proxies.Count > 0
                ? configuration.Proxies[Math.Min(index, configuration.Proxies.Count - 1)].Id
                : null;
            RefreshView(fallbackId);
        }

        private void OnAppAdd(Guid proxyId, string appName)
        {
            var proxy = FindProxy(proxyId);
            if (proxy == null || proxy.AppNames.Contains(appName))
            {
                return;
            }
            proxy.AppNames.Add(appName);
            RefreshView(proxyId);
        }

        private void OnAppDelete(Guid proxyId, string appName)
        {
            var proxy = FindProxy(proxyId);
            if (proxy == null)
            {
                return;
            }
            proxy.AppNames.Remove(appName);
            RefreshView(proxyId);
        }

        private void OnProxyFieldsEdit(Guid proxyId, string endpoint, string username, string password, bool tcp, bool udp)
        {
            var proxy = FindProxy(proxyId);
            if (proxy == null)
            {
                return;
            }
            // Skip the endpoint commit while it's blank - otherwise a mid-edit empty field would
            // blank out (and, on save, drop) an otherwise-valid proxy.
            if (!string.IsNullOrEmpty(endpoint))
            {
                proxy.Endpoint = endpoint;
            }
            proxy.Username = username;
            proxy.Password = password;
            proxy.Tcp = tcp;
            proxy.Udp = udp;
            RefreshView(proxyId);
        }

        private void OnLogLevelEdit(string logLevel)
        {
            // Doesn't touch any proxy, and the combo box already shows what the user just typed -
            // no need to push a fresh snapshot back.
            configuration.LogLevel = logLevel;
        }

        public void Initialize()
        {
            if (!File.Exists(ProgramPath))
            {
                view.DisableAllControls();
                view.AppendLine("Couldn't find " + ProgramName);
                view.AppendLine("proxifyre-tray.exe must be inside the ProxiFyre directory to work");
                view.ShowForm();
                return;
            }

            OnAbout();

            view.SetRunningState(false);
            view.SetLogLevels(LogLevels);

            LoadConfig();

            var startupRegistryKey = Registry.CurrentUser.OpenSubKey(StartupRegistryKeyPath, true);
            var startupRegistryValue = startupRegistryKey?.GetValue(productName) as string;
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
                startupRegistryKey?.DeleteValue(productName, false);
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
                    view.AppendLine(ex.Message);
                }
                // Mirror Newtonsoft's old leniency: an empty/unreadable file yields an
                // empty configuration rather than a JsonException.
                var dto = string.IsNullOrEmpty(configContent)
                    ? null
                    : JsonSerializer.Deserialize<Configuration>(configContent, JsonOptions);
                configuration = dto?.ToDomain() ?? new AppConfiguration();
            }
            else
            {
                configuration = new AppConfiguration();
            }

            if (string.IsNullOrEmpty(configuration.LogLevel))
            {
                configuration.LogLevel = LogLevels[0];
            }

            RefreshView(null);
        }

        private void OnSave()
        {
            var dto = configuration.ToDto();
            var content = JsonSerializer.Serialize(dto, JsonOptions);

            try
            {
                File.WriteAllText(ConfigPath, content);
                view.AppendLine("Configuration file saved");
            }
            catch (Exception ex)
            {
                view.AppendLine(ex.Message);
            }
        }

        private void OnStart()
        {
            OnSave();

            if (!File.Exists(ProgramPath))
            {
                return;
            }

            if (proxifyreProcess == null)
            {
                view.AppendLine("Starting ProxiFyre");
            }
            else
            {
                try
                {
                    // GetProcessById never returns null - it throws if the process isn't running,
                    // which is exactly what we're probing for here.
                    Process.GetProcessById(proxifyreProcess.Id);
                    OnStop();
                }
                catch (Exception)
                {
                    view.AppendLine("Starting ProxiFyre");
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

            proxifyreProcess.OutputDataReceived += (sender, e) => view.AppendLine(e.Data ?? string.Empty);

            try
            {
                proxifyreProcess.Start();
                proxifyreProcess.BeginOutputReadLine();
                proxifyreProcess.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                view.AppendLine(ex.Message);
            }

            view.SetRunningState(true);
        }

        private void OnStop()
        {
            if (proxifyreProcess == null)
            {
                return;
            }

            view.AppendLine("Stopping ProxiFyre");
            proxifyreProcess.Kill();
            proxifyreProcess.Dispose();

            view.SetRunningState(false);
        }

        private void OnStartupToggle()
        {
            var startupRegistryKey = Registry.CurrentUser.OpenSubKey(StartupRegistryKeyPath, true);
            if (!view.StartupChecked)
            {
                startupRegistryKey?.SetValue(productName, executablePath);
                view.StartupChecked = true;
            }
            else
            {
                startupRegistryKey?.DeleteValue(productName, false);
                view.StartupChecked = false;
            }
        }

        private void OnAbout()
        {
            view.AppendLine("ProxiFyre configuration utility and tray launcher thing");
            view.AppendLine("proxifyre-tray by airenelias https://github.com/airenelias/proxifyre-tray");
            view.AppendLine("Icons by Icons8 https://icons8.com");
        }

        private void OnLinkClicked(string link)
        {
            Process.Start(link);
        }

        /// <summary>Whether a user-initiated close should be turned into a hide instead.</summary>
        private bool ShouldCancelClose(bool userClosing)
        {
            return userClosing && File.Exists(ProgramPath);
        }

        private void OnFormClosed()
        {
            if (proxifyreProcess == null)
            {
                return;
            }
            try
            {
                // GetProcessById never returns null - it throws if the process isn't running,
                // which is exactly what we're probing for here.
                Process.GetProcessById(proxifyreProcess.Id);
                proxifyreProcess.Kill();
                proxifyreProcess.Dispose();
            }
            catch (Exception)
            {
                // Process already gone - nothing to clean up.
            }
        }
    }
}
