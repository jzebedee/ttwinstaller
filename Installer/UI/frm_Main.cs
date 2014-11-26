using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using TaleOfTwoWastelands.Progress;
using TaleOfTwoWastelands.Properties;

namespace TaleOfTwoWastelands.UI
{
    public partial class frm_Main : Form
    {
        private CancellationTokenSource _installCts;
        private Task _installTask;
        private Installer _install;
        private Prompts _prompts;

        public frm_Main()
        {
            InitializeComponent();
        }

        private void frm_Main_Load(object sender, EventArgs e)
        {
            Trace.Assert(Program.IsElevated, string.Format(Resources.MustBeElevated, Resources.TTW));

            //Progress<T> maintains SynchronizationContext
            Log.DisplayMessage = new Progress<string>(UpdateLog);
            var uiMinor = new Progress<InstallStatus>(m => UpdateProgressBar(m, prgCurrent));
            var uiMajor = new Progress<InstallStatus>(m => UpdateProgressBar(m, prgOverall));

            _prompts = new Prompts(dlg_FindGame, dlg_SaveTTW);
            _install = new Installer(uiMinor, uiMajor, _prompts);

            txt_FO3Location.Text = _prompts.Fallout3Path;
            txt_FNVLocation.Text = _prompts.FalloutNVPath;
            txt_TTWLocation.Text = _prompts.TTWSavePath;
        }

        private void UpdateProgressBar(InstallStatus opProg, TextProgressBar bar)
        {
            bar.Maximum = opProg.ItemsTotal;
            bar.Value = opProg.ItemsDone;
            bar.CustomText = opProg.CurrentOperation;
        }

        private void UpdateLog(string msg)
        {
            txt_Progress.AppendText(msg);
            txt_Progress.AppendText(Environment.NewLine);
        }

        private void btn_FO3Browse_Click(object sender, EventArgs e)
        {
            _prompts.Fallout3Prompt(true);
            txt_FO3Location.Text = _prompts.Fallout3Path;
        }

        private void btn_FNVBrowse_Click(object sender, EventArgs e)
        {
            _prompts.FalloutNVPrompt(true);
            txt_FNVLocation.Text = _prompts.FalloutNVPath;
        }

        private void btn_TTWBrowse_Click(object sender, EventArgs e)
        {
            _prompts.TTWPrompt(true);
            txt_TTWLocation.Text = _prompts.TTWSavePath;
        }

        private void btn_Install_Click(object sender, EventArgs e)
        {
            Action resetInstallBtn = () =>
            {
                btn_Install.Text = Resources.Install;
                btn_Install.Enabled = true;
                _installCts.Dispose();
            };

            if (btn_Install.Text == Resources.Install)
            {
                _installCts = new CancellationTokenSource();

                btn_Install.Text = Resources.Cancel;
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
                btn_Install.Text = Resources.CancelingWait;
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
                MessageBox.Show(Resources.RightSaidFred);
            }
        }
    }
}