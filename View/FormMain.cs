using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace proxifyre_tray
{
    /// <summary>
    /// The main window. A passive view in the MVP sense: it never references
    /// <see cref="FormMainPresenter"/> or the domain model (<see cref="AppConfiguration"/>) -
    /// only the read-only <see cref="ConfigurationView"/> snapshot handed to it via
    /// <see cref="SetConfiguration"/>. Every user edit is raised as an event identifying the
    /// proxy involved by <see cref="ProxyView.Id"/> plus the new value(s); the presenter
    /// (wired up by <see cref="Program"/>, the composition root) resolves the id, performs the
    /// actual mutation, and calls <see cref="SetConfiguration"/> again with a fresh snapshot -
    /// the view never has to infer what changed, it just re-renders whatever it's given.
    /// </summary>
    public partial class FormMain : Form, IFormMainView
    {
        private bool _formVisible;
        private ConfigurationView? _configuration;

        protected override void SetVisibleCore(bool value)
        {
            if (!_formVisible)
            {
                value = false;
            }
            base.SetVisibleCore(value);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.C))
            {
                // Mirrors what a real click on the (possibly disabled) Stop button would do.
                if (buttonStop.Enabled)
                {
                    StopRequested?.Invoke(this, EventArgs.Empty);
                }
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public FormMain()
        {
            InitializeComponent();
            Icon = Properties.Resources.icons8_sorting_arrows_32;
        }

        private ProxyView? SelectedProxy
        {
            get
            {
                var index = listBoxProxies.SelectedIndex;
                return _configuration != null && index >= 0 && index < _configuration.Proxies.Count
                    ? _configuration.Proxies[index]
                    : null;
            }
        }

        #region IFormMainView

        public void SetConfiguration(ConfigurationView configurationView, Guid? selectedProxyId)
        {
            _configuration = configurationView;
            comboBoxLogLevel.DataSource = configurationView.ValidLogLevels.ToList();
            comboBoxLogLevel.Text = _configuration.LogLevel;
            RefreshProxyList(selectedProxyId);
        }

        public bool StartupChecked
        {
            get => toolStripMenuItemStartup.Checked;
            set => toolStripMenuItemStartup.Checked = value;
        }

        public void SetRunningState(bool running)
        {
            buttonStart.Image = running ? Properties.Resources.icons8_start_16 : Properties.Resources.icons8_next_16;
            notifyIconTray.Icon = running ? Properties.Resources.icons8_sorting_arrows_32 : Properties.Resources.icons8_sorting_arrows_grayed_32;
            buttonStop.Enabled = running;
        }

        public void DisableAllControls()
        {
            foreach (Control control in Controls)
            {
                control.Enabled = false;
            }
        }

        public void AppendLine(string text)
        {
            if (richTextBoxOutput.InvokeRequired)
            {
                richTextBoxOutput.Invoke((MethodInvoker)(() => AppendLine(text)));
                return;
            }
            richTextBoxOutput.AppendText(text + Environment.NewLine);
        }

        public void ShowForm()
        {
            _formVisible = true;
            Show();
        }

        public event EventHandler? SaveRequested;
        public event EventHandler? StartRequested;
        public event EventHandler? StopRequested;
        public event EventHandler? StartupToggleRequested;
        public event EventHandler? AboutRequested;
        public event EventHandler<ViewClosingEventArgs>? ViewClosing;
        public event EventHandler? ViewClosed;

        public event EventHandler? ProxyAddRequested;
        public event EventHandler<Guid>? ProxyDeleteRequested;
        public event EventHandler<(Guid ProxyId, string AppName)>? AppAddRequested;
        public event EventHandler<(Guid ProxyId, string AppName)>? AppDeleteRequested;
        public event EventHandler<(Guid ProxyId, string Endpoint, string Username, string Password, bool Tcp, bool Udp)>? ProxyFieldsEditRequested;
        public event EventHandler<string>? LogLevelEditRequested;

        #endregion

        #region Proxy / app list rendering and selection (view-owned reads; edits go through the presenter)

        private void RefreshProxyList(Guid? selectedProxyId)
        {
            // Only ever called right after SetConfiguration assigns _configuration, so it's never null here.
            listBoxProxies.Items.Clear();
            foreach (var proxy in _configuration!.Proxies)
            {
                listBoxProxies.Items.Add(proxy.Endpoint);
            }

            if (listBoxProxies.Items.Count > 0)
            {
                var index = selectedProxyId.HasValue
                    ? _configuration.Proxies.FindIndex(proxy => proxy.Id == selectedProxyId.Value)
                    : -1;
                listBoxProxies.SelectedIndex = index >= 0 ? index : 0;
            }
            else
            {
                RefreshAppList(0);
            }
        }

        private void RefreshAppList(int selectedIndex)
        {
            listBoxApps.Items.Clear();
            textBoxApp.Text = string.Empty;

            var proxy = SelectedProxy;
            if (proxy?.AppNames != null)
            {
                foreach (var app in proxy.AppNames)
                {
                    listBoxApps.Items.Add(app);
                }
            }

            if (listBoxApps.Items.Count > 0)
            {
                listBoxApps.SelectedIndex = listBoxApps.Items.Count <= selectedIndex
                    ? listBoxApps.Items.Count - 1
                    : selectedIndex;
            }
        }

        private void RenderSelectedProxy()
        {
            RefreshAppList(0);

            var proxy = SelectedProxy;
            if (proxy == null)
            {
                return;
            }

            textBoxEndpoint.Text = proxy.Endpoint;
            textBoxUsername.Text = proxy.Username;
            textBoxPassword.Text = proxy.Password;
            checkBoxTcp.Checked = proxy.Tcp;
            checkBoxUdp.Checked = proxy.Udp;
        }

        private void listBoxProxies_SelectedIndexChanged(object sender, EventArgs e)
        {
            RenderSelectedProxy();
        }

        private void listBoxApps_SelectedIndexChanged(object sender, EventArgs e)
        {
            var proxy = SelectedProxy;
            if (proxy != null && listBoxApps.SelectedIndex >= 0)
            {
                textBoxApp.Text = proxy.AppNames[listBoxApps.SelectedIndex];
            }
        }

        private void buttonProxiesAdd_Click(object sender, EventArgs e)
        {
            ProxyAddRequested?.Invoke(this, EventArgs.Empty);
        }

        private void buttonProxiesDel_Click(object sender, EventArgs e)
        {
            var proxy = SelectedProxy;
            if (proxy != null)
            {
                ProxyDeleteRequested?.Invoke(this, proxy.Id);
            }
        }

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            var proxy = SelectedProxy;
            var fileName = openFileDialogApp.ShowDialog() == DialogResult.OK ? openFileDialogApp.FileName : null;
            if (proxy == null || string.IsNullOrEmpty(fileName) || listBoxApps.Items.Contains(fileName))
            {
                return;
            }
            AppAddRequested?.Invoke(this, (proxy.Id, fileName));
        }

        private void buttonAppsAdd_Click(object sender, EventArgs e)
        {
            var proxy = SelectedProxy;
            var appText = textBoxApp.Text;
            if (proxy == null || string.IsNullOrEmpty(appText) || listBoxApps.Items.Contains(appText))
            {
                return;
            }
            AppAddRequested?.Invoke(this, (proxy.Id, appText));
        }

        private void buttonAppsDel_Click(object sender, EventArgs e)
        {
            var proxy = SelectedProxy;
            if (proxy == null)
            {
                return;
            }
            AppDeleteRequested?.Invoke(this, (proxy.Id, listBoxApps.Text));
        }

        private void textBoxEndpoint_Validated(object sender, EventArgs e)
        {
            RaiseProxyFieldsEdit();
        }

        private void textBoxUsername_Validated(object sender, EventArgs e)
        {
            RaiseProxyFieldsEdit();
        }

        private void textBoxPassword_Validated(object sender, EventArgs e)
        {
            RaiseProxyFieldsEdit();
        }

        private void checkBoxTcp_Validated(object sender, EventArgs e)
        {
            RaiseProxyFieldsEdit();
        }

        private void checkBoxUdp_Validated(object sender, EventArgs e)
        {
            RaiseProxyFieldsEdit();
        }

        private void RaiseProxyFieldsEdit()
        {
            var proxy = SelectedProxy;
            if (proxy == null)
            {
                return;
            }
            ProxyFieldsEditRequested?.Invoke(this, (proxy.Id, textBoxEndpoint.Text, textBoxUsername.Text, textBoxPassword.Text, checkBoxTcp.Checked, checkBoxUdp.Checked));
        }

        private void comboBoxLogLevel_Validated(object sender, EventArgs e)
        {
            if (_configuration != null)
            {
                LogLevelEditRequested?.Invoke(this, comboBoxLogLevel.Text);
            }
        }

        #endregion

        private void buttonSave_Click(object sender, EventArgs e)
        {
            SaveRequested?.Invoke(this, EventArgs.Empty);
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            StartRequested?.Invoke(this, EventArgs.Empty);
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            StopRequested?.Invoke(this, EventArgs.Empty);
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            ShowForm();
        }

        private void toolStripMenuItemStartup_Click(object sender, EventArgs e)
        {
            StartupToggleRequested?.Invoke(this, EventArgs.Empty);
        }

        private void toolStripMenuItemExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            var args = new ViewClosingEventArgs(e.CloseReason == CloseReason.UserClosing);
            ViewClosing?.Invoke(this, args);
            if (args.Cancel)
            {
                Hide();
                e.Cancel = true;
            }
        }

        private void FormMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            ViewClosed?.Invoke(this, EventArgs.Empty);
        }

        private void buttonAbout_Click(object sender, EventArgs e)
        {
            AboutRequested?.Invoke(this, EventArgs.Empty);
        }

        private void textBoxApp_Resize(object sender, EventArgs e)
        {
            buttonAppsAdd.Visible = textBoxApp.Width > 0;
        }

        private void listBoxApps_MouseHover(object sender, EventArgs e)
        {
            Point point = listBoxApps.PointToClient(Cursor.Position);
            int index = listBoxApps.IndexFromPoint(point);
            if (index >= 0)
            {
                toolTipApp.Show(listBoxApps.Items[index].ToString(), listBoxApps);
            }
        }
    }
}
