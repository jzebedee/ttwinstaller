using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Security.Principal;
using TaleOfTwoWastelands.ProgressTypes;

namespace TaleOfTwoWastelands.UI
{
    public partial class frm_Main : Form
    {
        private CancellationTokenSource _installCts;
        private Task _installTask;
        private Installer _install;

        public frm_Main()
        {
            InitializeComponent();
        }

        private void frm_Main_Load(object sender, EventArgs e)
        {
            //verify we are running as administrator
            VerifyElevation();

            //Progress<T> maintains SynchronizationContext
            var progressLog = new Progress<string>(UpdateLog);
            var uiMinor = new Progress<InstallOperation>(m => UpdateProgressBar(m, prgCurrent));
            var uiMajor = new Progress<InstallOperation>(m => UpdateProgressBar(m, prgOverall));
            _install = new Installer(progressLog, uiMinor, uiMajor, dlg_FindGame, dlg_SaveTTW);

            txt_FO3Location.Text = _install.Fallout3Path;
            txt_FNVLocation.Text = _install.FalloutNVPath;
            txt_TTWLocation.Text = _install.TTWSavePath;
        }

        private void VerifyElevation()
        {
            var identity = WindowsIdentity.GetCurrent();
            Debug.Assert(identity != null, "identity != null");

            var principal = new WindowsPrincipal(identity);
            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                return;

            MessageBox.Show(Application.ProductName + " must be run as Administrator.");
            Environment.Exit(2);
        }

        private void frm_Main_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (_installCts != null)
                _installCts.Dispose();
            if (_install != null)
                _install.Dispose();
        }

        private void UpdateProgressBar(InstallOperation opProg, TextProgressBar bar)
        {
            bar.Maximum = opProg.ItemsTotal;
            bar.Value = opProg.ItemsDone;
            bar.CustomText = opProg.CurrentOperation;
        }

        private void UpdateLog(string msg)
        {
            txt_Progress.AppendText("[" + DateTime.Now + "]\t" + msg + Environment.NewLine);
        }

        private void btn_FO3Browse_Click(object sender, EventArgs e)
        {
            _install.Fallout3Prompt(dlg_FindGame, true);
            txt_FO3Location.Text = _install.Fallout3Path;
        }

        private void btn_FNVBrowse_Click(object sender, EventArgs e)
        {
            _install.FalloutNVPrompt(dlg_FindGame, true);
            txt_FNVLocation.Text = _install.FalloutNVPath;
        }

        private void btn_TTWBrowse_Click(object sender, EventArgs e)
        {
            _install.TTWPrompt(dlg_SaveTTW, true);
            txt_TTWLocation.Text = _install.TTWSavePath;
        }

        private void btn_Install_Click(object sender, EventArgs e)
        {
            Action resetInstallBtn = () =>
            {
                btn_Install.Text = "Install";
                btn_Install.Enabled = true;
                _installCts.Dispose();
            };

            if (btn_Install.Text == "Install")
            {
                _installCts = new CancellationTokenSource();

                btn_Install.Text = "Cancel";
                _installTask = Task.Factory.StartNew(() => _install.Install(_installCts.Token));
                _installTask.ContinueWith(task =>
                {
                    if (btn_Install.InvokeRequired)
                    {
                        btn_Install.Invoke(resetInstallBtn);
                    }
                    else
                        resetInstallBtn();
                });
            }
            else
            {
                btn_Install.Text = "Canceling...";
                btn_Install.Enabled = false;
                _installCts.Cancel();
            }
        }

        private void chkYou_CheckedChanged(object sender, EventArgs e)
        {
            var checkbox = (sender as CheckBox);
            Debug.Assert(checkbox != null, "checkbox != null");

            if (!checkbox.Checked)
            {
                checkbox.Checked = true;
                MessageBox.Show("Impossible");
            }
        }
    }
}