using System.Collections.Generic;

namespace proxifyre_tray
{
    /// <summary>
    /// The contract the presenter uses to drive the main window. Implemented by
    /// <see cref="FormMain"/> so the presenter never has to reference a WinForms
    /// control type directly.
    /// </summary>
    internal interface IFormMainView
    {
        IReadOnlyList<string> ProxyItems { get; }
        string ProxySelectedText { get; }
        int ProxySelectedIndex { get; set; }

        IReadOnlyList<string> AppItems { get; }
        string AppSelectedText { get; }
        int AppSelectedIndex { get; set; }

        string LogLevel { get; set; }
        string IpText { get; set; }
        string PortText { get; set; }
        string UsernameText { get; set; }
        string PasswordText { get; set; }
        bool TcpChecked { get; set; }
        bool UdpChecked { get; set; }
        string AppText { get; set; }

        bool StopEnabled { get; set; }
        bool StartupChecked { get; set; }

        void SetLogLevels(IReadOnlyList<string> levels);
        void SetProxyItems(IReadOnlyList<string> items);
        void SetAppItems(IReadOnlyList<string> items);
        void ReplaceProxyItem(int index, string text);

        /// <summary>Toggles the tray icon / start button image between the running and stopped states.</summary>
        void SetRunningState(bool running);

        void DisableAllControls();

        /// <summary>Appends text to the output log, marshalling to the UI thread if needed.</summary>
        void AppendOutput(string text);

        /// <summary>Shows the "browse for app" file dialog and returns the chosen path, or null if cancelled.</summary>
        string BrowseForAppFile();

        void ShowForm();
    }
}
