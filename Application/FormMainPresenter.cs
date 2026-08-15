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
    public sealed class FormMainPresenter
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

        private readonly IFormMainView _view;
        private readonly string _productName;
        private readonly string _executablePath;

        // Real default rather than a null! placeholder - LoadConfig (called from Initialize) replaces
        // it with the loaded configuration, but nothing reads it before that, so an empty one is fine.
        private AppConfiguration _configuration = new AppConfiguration();

        private Process? _proxifyreProcess;

        public FormMainPresenter(IFormMainView view, string productName, string executablePath)
        {
            _view = view;
            _productName = productName;
            _executablePath = executablePath;

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
            return _configuration.Proxies.FirstOrDefault(proxy => proxy.Id == proxyId);
        }

        /// <summary>Pushes a fresh snapshot to the view after a mutation, asking it to keep (or move to) the given proxy selected.</summary>
        private void RefreshView(Guid? selectedProxyId)
        {
            _view.SetConfiguration(_configuration.ToView(), selectedProxyId);
        }

        private void OnProxyAdd()
        {
            var proxy = ProxyConfiguration.CreateDefault();
            _configuration.Proxies.Add(proxy);
            RefreshView(proxy.Id);
        }

        private void OnProxyDelete(Guid proxyId)
        {
            var index = _configuration.Proxies.FindIndex(proxy => proxy.Id == proxyId);
            if (index < 0)
            {
                return;
            }
            _configuration.Proxies.RemoveAt(index);

            // Select whatever ends up at the same position, mirroring the old "nearest remaining item" behaviour.
            Guid? fallbackId = _configuration.Proxies.Count > 0
                ? _configuration.Proxies[Math.Min(index, _configuration.Proxies.Count - 1)].Id
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
            // The Endpoint setter itself rejects anything that isn't empty or a valid "host:port" pair,
            // keeping whatever was there before - so a mid-edit blank or portless field doesn't blank
            // out (and, on save, drop) an otherwise-valid proxy. No guard needed here.
            proxy.Endpoint = endpoint;
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
            _configuration.LogLevel = logLevel;
        }

        public void Initialize()
        {
            if (!File.Exists(ProgramPath))
            {
                _view.DisableAllControls();
                _view.AppendLine("Couldn't find " + ProgramName);
                _view.AppendLine("proxifyre-tray.exe must be inside the ProxiFyre directory to work");
                _view.ShowForm();
                return;
            }

            OnAbout();

            _view.SetRunningState(false);
            _view.SetLogLevels(LogLevels);

            LoadConfig();

            var startupRegistryKey = Registry.CurrentUser.OpenSubKey(StartupRegistryKeyPath, true);
            var startupRegistryValue = startupRegistryKey?.GetValue(_productName) as string;
            if (startupRegistryValue == null)
            {
                _view.StartupChecked = false;
            }
            else if (startupRegistryValue == _executablePath)
            {
                _view.StartupChecked = true;
                OnStart();
            }
            else
            {
                startupRegistryKey?.DeleteValue(_productName, false);
                _view.StartupChecked = false;
            }

            if (!_view.StartupChecked)
            {
                _view.ShowForm();
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
                    _view.AppendLine(ex.Message);
                }
                // Mirror Newtonsoft's old leniency: an empty/unreadable file yields an
                // empty configuration rather than a JsonException.
                var dto = string.IsNullOrEmpty(configContent)
                    ? null
                    : JsonSerializer.Deserialize<Configuration>(configContent, JsonOptions);
                _configuration = dto?.ToDomain() ?? new AppConfiguration();
            }
            else
            {
                _configuration = new AppConfiguration();
            }

            if (string.IsNullOrEmpty(_configuration.LogLevel))
            {
                _configuration.LogLevel = LogLevels[0];
            }

            RefreshView(null);
        }

        private void OnSave()
        {
            var dto = _configuration.ToDto();
            var content = JsonSerializer.Serialize(dto, JsonOptions);

            try
            {
                File.WriteAllText(ConfigPath, content);
                _view.AppendLine("Configuration file saved");
            }
            catch (Exception ex)
            {
                _view.AppendLine(ex.Message);
            }
        }

        private void OnStart()
        {
            OnSave();

            if (!File.Exists(ProgramPath))
            {
                return;
            }

            if (_proxifyreProcess == null)
            {
                _view.AppendLine("Starting ProxiFyre");
            }
            else
            {
                try
                {
                    // GetProcessById never returns null - it throws if the process isn't running,
                    // which is exactly what we're probing for here.
                    Process.GetProcessById(_proxifyreProcess.Id);
                    OnStop();
                }
                catch (Exception)
                {
                    _view.AppendLine("Starting ProxiFyre");
                }
            }

            _proxifyreProcess = new Process
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

            _proxifyreProcess.OutputDataReceived += (sender, e) => _view.AppendLine(e.Data ?? string.Empty);

            try
            {
                _proxifyreProcess.Start();
                _proxifyreProcess.BeginOutputReadLine();
                _proxifyreProcess.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                _view.AppendLine(ex.Message);
            }

            _view.SetRunningState(true);
        }

        private void OnStop()
        {
            if (_proxifyreProcess == null)
            {
                return;
            }

            _view.AppendLine("Stopping ProxiFyre");
            _proxifyreProcess.Kill();
            _proxifyreProcess.Dispose();

            _view.SetRunningState(false);
        }

        private void OnStartupToggle()
        {
            var startupRegistryKey = Registry.CurrentUser.OpenSubKey(StartupRegistryKeyPath, true);
            if (!_view.StartupChecked)
            {
                startupRegistryKey?.SetValue(_productName, _executablePath);
                _view.StartupChecked = true;
            }
            else
            {
                startupRegistryKey?.DeleteValue(_productName, false);
                _view.StartupChecked = false;
            }
        }

        private void OnAbout()
        {
            _view.AppendLine("ProxiFyre configuration utility and tray launcher thing");
            _view.AppendLine("proxifyre-tray by airenelias https://github.com/airenelias/proxifyre-tray");
            _view.AppendLine("Icons by Icons8 https://icons8.com");
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
            if (_proxifyreProcess == null)
            {
                return;
            }
            try
            {
                // GetProcessById never returns null - it throws if the process isn't running,
                // which is exactly what we're probing for here.
                Process.GetProcessById(_proxifyreProcess.Id);
                _proxifyreProcess.Kill();
                _proxifyreProcess.Dispose();
            }
            catch (Exception)
            {
                // Process already gone - nothing to clean up.
            }
        }
    }
}
