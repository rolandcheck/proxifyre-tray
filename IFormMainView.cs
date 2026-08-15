using System;
using System.Collections.Generic;

namespace proxifyre_tray
{
    /// <summary>
    /// The contract the presenter uses to drive the main window. Implemented by
    /// <see cref="FormMain"/> as a passive view: it never references
    /// <see cref="FormMainPresenter"/> itself, and it never sees the domain model
    /// (<see cref="AppConfiguration"/>) at all - only the read-only <see cref="ConfigurationView"/>
    /// snapshot handed to it via <see cref="SetConfiguration"/>. Every user edit is raised as an
    /// event identifying the proxy involved by <see cref="ProxyView.Id"/> (never by holding the
    /// domain object itself) plus the new value(s); the presenter resolves the id and performs
    /// the actual mutation, then calls <see cref="SetConfiguration"/> again with a fresh snapshot
    /// for the view to re-render - the view never has to (and never does) infer what changed.
    /// </summary>
    internal interface IFormMainView
    {
        /// <summary>Hands the view a fresh snapshot to render, selecting the given proxy if it still exists
        /// (falling back to the first one otherwise). Called once for the initial load and again after every edit.</summary>
        void SetConfiguration(ConfigurationView configuration, Guid? selectedProxyId);

        bool StartupChecked { get; set; }

        void SetLogLevels(IReadOnlyList<string> levels);

        /// <summary>Toggles the tray icon / start button image between the running and stopped states,
        /// and enables/disables the Stop button to match.</summary>
        void SetRunningState(bool running);

        void DisableAllControls();

        /// <summary>Appends a line to the output log (adding the trailing newline itself), marshalling to the UI thread if needed.</summary>
        void AppendLine(string text);

        void ShowForm();

        event EventHandler SaveRequested;
        event EventHandler StartRequested;
        event EventHandler StopRequested;
        event EventHandler StartupToggleRequested;
        event EventHandler AboutRequested;
        event EventHandler<string> LinkClicked;

        /// <summary>Raised when the window is about to close. Set <see cref="ViewClosingEventArgs.Cancel"/> to turn it into a hide instead.
        /// Named ViewClosing (not Closing) to avoid colliding with Form's own legacy Closing event.</summary>
        event EventHandler<ViewClosingEventArgs> ViewClosing;

        /// <summary>Named ViewClosed (not Closed) to avoid colliding with Form's own legacy Closed event.</summary>
        event EventHandler ViewClosed;

        /// <summary>Appends a fresh default proxy.</summary>
        event EventHandler ProxyAddRequested;

        /// <summary>Removes the proxy with the given id.</summary>
        event EventHandler<Guid> ProxyDeleteRequested;

        /// <summary>Adds the given app path to the given proxy's app list.</summary>
        event EventHandler<(Guid ProxyId, string AppName)> AppAddRequested;

        /// <summary>Removes the given app path from the given proxy's app list.</summary>
        event EventHandler<(Guid ProxyId, string AppName)> AppDeleteRequested;

        /// <summary>Raised whenever any proxy detail field validates (endpoint, username, password, or either
        /// protocol checkbox) - all five carry the same shape of edit ("write these fields onto this proxy"), so
        /// they share one event rather than one apiece. Endpoint is only committed by the presenter when
        /// non-empty, so a mid-edit blank field doesn't blank out (and, on save, drop) the proxy.</summary>
        event EventHandler<(Guid ProxyId, string Endpoint, string Username, string Password, bool Tcp, bool Udp)> ProxyFieldsEditRequested;

        event EventHandler<string> LogLevelEditRequested;
    }

    /// <summary>
    /// Event data for <see cref="IFormMainView.ViewClosing"/> - deliberately not
    /// System.Windows.Forms.FormClosingEventArgs, so the presenter stays framework-agnostic.
    /// Public (unlike the rest of this file) only because FormMain's ViewClosing event, being
    /// on a public Form subclass, can't expose an internal type in its signature.
    /// </summary>
    public sealed class ViewClosingEventArgs : EventArgs
    {
        public ViewClosingEventArgs(bool userClosing)
        {
            UserClosing = userClosing;
        }

        public bool UserClosing { get; }
        public bool Cancel { get; set; }
    }
}
