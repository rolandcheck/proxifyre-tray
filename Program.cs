using System;
using System.Windows.Forms;

namespace proxifyre_tray
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Composition root: the view is a passive component with no knowledge of the
            // presenter, so it's built first with no dependencies. The presenter is then
            // constructed with the view, wiring up its event subscriptions.
            var view = new FormMain();
            // ProductName is nullable per WinForms' own annotations (unset assembly metadata), though
            // in practice it always resolves for a built app; fall back to the assembly name just in case.
            var presenter = new FormMainPresenter(view, Application.ProductName ?? "proxifyre-tray", Application.ExecutablePath);
            presenter.Initialize();

            Application.Run(view);
        }
    }
}
