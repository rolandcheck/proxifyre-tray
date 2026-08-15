using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace proxifyre_tray
{
    /// <summary>
    /// The main window. Pure "view" in the MVP sense: it forwards user input to
    /// <see cref="FormMainPresenter"/> and exposes control state through
    /// <see cref="IFormMainView"/>; it holds no configuration or process logic itself.
    /// </summary>
    public partial class FormMain : Form, IFormMainView
    {
        private readonly FormMainPresenter presenter;

        private bool formVisible;

        protected override void SetVisibleCore(bool value)
        {
            if (!formVisible)
            {
                value = false;
            }
            base.SetVisibleCore(value);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.C))
            {
                presenter.OnCtrlC();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public FormMain()
        {
            InitializeComponent();
            Icon = Properties.Resources.icons8_sorting_arrows_32;

            presenter = new FormMainPresenter(this, Application.ProductName, Application.ExecutablePath);
            presenter.Initialize();
        }

        #region IFormMainView

        public IReadOnlyList<string> ProxyItems => listBoxProxies.Items.Cast<string>().ToList();

        public string ProxySelectedText => listBoxProxies.Text;

        public int ProxySelectedIndex
        {
            get => listBoxProxies.SelectedIndex;
            set => listBoxProxies.SelectedIndex = value;
        }

        public IReadOnlyList<string> AppItems => listBoxApps.Items.Cast<string>().ToList();

        public string AppSelectedText => listBoxApps.Text;

        public int AppSelectedIndex
        {
            get => listBoxApps.SelectedIndex;
            set => listBoxApps.SelectedIndex = value;
        }

        public string LogLevel
        {
            get => comboBoxLogLevel.Text;
            set => comboBoxLogLevel.Text = value;
        }

        public string IpText
        {
            get => textBoxIp.Text;
            set => textBoxIp.Text = value;
        }

        public string PortText
        {
            get => textBoxPort.Text;
            set => textBoxPort.Text = value;
        }

        public string UsernameText
        {
            get => textBoxUsername.Text;
            set => textBoxUsername.Text = value;
        }

        public string PasswordText
        {
            get => textBoxPassword.Text;
            set => textBoxPassword.Text = value;
        }

        public bool TcpChecked
        {
            get => checkBoxTcp.Checked;
            set => checkBoxTcp.Checked = value;
        }

        public bool UdpChecked
        {
            get => checkBoxUdp.Checked;
            set => checkBoxUdp.Checked = value;
        }

        public string AppText
        {
            get => textBoxApp.Text;
            set => textBoxApp.Text = value;
        }

        public bool StopEnabled
        {
            get => buttonStop.Enabled;
            set => buttonStop.Enabled = value;
        }

        public bool StartupChecked
        {
            get => toolStripMenuItemStartup.Checked;
            set => toolStripMenuItemStartup.Checked = value;
        }

        public void SetLogLevels(IReadOnlyList<string> levels)
        {
            comboBoxLogLevel.DataSource = levels.ToList();
        }

        public void SetProxyItems(IReadOnlyList<string> items)
        {
            listBoxProxies.Items.Clear();
            foreach (var item in items)
            {
                listBoxProxies.Items.Add(item);
            }
        }

        public void SetAppItems(IReadOnlyList<string> items)
        {
            listBoxApps.Items.Clear();
            foreach (var item in items)
            {
                listBoxApps.Items.Add(item);
            }
        }

        public void ReplaceProxyItem(int index, string text)
        {
            listBoxProxies.Items[index] = text;
        }

        public void SetRunningState(bool running)
        {
            buttonStart.Image = running ? Properties.Resources.icons8_start_16 : Properties.Resources.icons8_next_16;
            notifyIconTray.Icon = running ? Properties.Resources.icons8_sorting_arrows_32 : Properties.Resources.icons8_sorting_arrows_grayed_32;
        }

        public void DisableAllControls()
        {
            foreach (Control control in Controls)
            {
                control.Enabled = false;
            }
        }

        public void AppendOutput(string text)
        {
            if (richTextBoxOutput.InvokeRequired)
            {
                richTextBoxOutput.Invoke((MethodInvoker)(() => AppendOutput(text)));
                return;
            }
            richTextBoxOutput.AppendText(text);
        }

        public string BrowseForAppFile()
        {
            return openFileDialogApp.ShowDialog() == DialogResult.OK ? openFileDialogApp.FileName : null;
        }

        public void ShowForm()
        {
            formVisible = true;
            Show();
        }

        #endregion

        private void listBoxProxies_SelectedIndexChanged(object sender, EventArgs e)
        {
            presenter.OnProxySelectionChanged();
        }

        private void listBoxApps_SelectedIndexChanged(object sender, EventArgs e)
        {
            presenter.OnAppSelectionChanged();
        }

        private void buttonProxiesAdd_Click(object sender, EventArgs e)
        {
            presenter.OnProxyAdd();
        }

        private void buttonProxiesDel_Click(object sender, EventArgs e)
        {
            presenter.OnProxyDelete();
        }

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            presenter.OnAppBrowse();
        }

        private void buttonAppsAdd_Click(object sender, EventArgs e)
        {
            presenter.OnAppAdd();
        }

        private void buttonAppsDel_Click(object sender, EventArgs e)
        {
            presenter.OnAppDelete();
        }

        private void textBoxIp_Validated(object sender, EventArgs e)
        {
            presenter.OnIpValidated();
        }

        private void textBoxPort_Validated(object sender, EventArgs e)
        {
            presenter.OnPortValidated();
        }

        private void textBoxUsername_Validated(object sender, EventArgs e)
        {
            presenter.OnUsernameValidated();
        }

        private void textBoxPassword_Validated(object sender, EventArgs e)
        {
            presenter.OnPasswordValidated();
        }

        private void checkBoxTcp_Validated(object sender, EventArgs e)
        {
            presenter.OnTcpValidated();
        }

        private void checkBoxUdp_Validated(object sender, EventArgs e)
        {
            presenter.OnUdpValidated();
        }

        private void comboBoxLogLevel_Validated(object sender, EventArgs e)
        {
            presenter.OnLogLevelValidated();
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            presenter.OnSave();
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            presenter.OnStart();
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            presenter.OnStop();
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            ShowForm();
        }

        private void toolStripMenuItemStartup_Click(object sender, EventArgs e)
        {
            presenter.OnStartupToggle();
        }

        private void toolStripMenuItemExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (presenter.ShouldCancelClose(e.CloseReason == CloseReason.UserClosing))
            {
                Hide();
                e.Cancel = true;
            }
        }

        private void FormMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            presenter.OnFormClosed();
        }

        private void buttonAbout_Click(object sender, EventArgs e)
        {
            presenter.OnAbout();
        }

        private void richTextBoxOutput_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            presenter.OnLinkClicked(e.LinkText);
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
