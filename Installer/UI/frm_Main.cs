using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Win32;
using System.Threading;
using System.Security.Principal;
using System.Diagnostics;
using TaleOfTwoWastelands.ProgressTypes;
using Microsoft;
using System.Collections.Concurrent;

namespace TaleOfTwoWastelands.UI
{
    public partial class frm_Main : Form
    {
        private CancellationTokenSource _install_cts = null;
        private Task _install_task;
        private Installer _install;

        public frm_Main()
        {
            InitializeComponent();
        }

        private void frm_Main_Load(object sender, EventArgs e)
        {
            //verify we are running as administrator
            Trace.Assert(new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator));

            _logUpdate = new System.Windows.Forms.Timer() { Interval = 500 };
            _logUpdate.Tick += (tsender, te) =>
            {
                var sb = new StringBuilder();
                var sortedKvps = new SortedList<int, StringBuilder>(_pendingMessages);

                StringBuilder dummy;
                foreach (var kvp in sortedKvps)
                {
                    _pendingMessages.TryRemove(kvp.Key, out dummy);
                    sb.Append(kvp.Value);
                }

                txt_Progress.AppendText(sb.ToString());
            };
            _logUpdate.Start();

            //Progress<T> maintains SynchronizationContext
            var progressLog = new Progress<string>(s => UpdateLog(s));
            var uiMinor = new Progress<InstallOperation>(m => UpdateProgressBar(m, prgCurrent));
            var uiMajor = new Progress<InstallOperation>(m => UpdateProgressBar(m, prgOverall));
            _install = new Installer(progressLog, uiMinor, uiMajor, dlg_FindGame, dlg_SaveTTW);

            txt_FO3Location.Text = _install.Fallout3Path;
            txt_FNVLocation.Text = _install.FalloutNVPath;
            txt_TTWLocation.Text = _install.TTWSavePath;
        }

        private void UpdateProgressBar(InstallOperation opProg, TextProgressBar bar)
        {
            bar.Maximum = opProg.ItemsTotal;
            bar.Value = opProg.ItemsDone;
            bar.CustomText = opProg.CurrentOperation;
        }

        private System.Windows.Forms.Timer _logUpdate;

        private ConcurrentDictionary<int, StringBuilder> _pendingMessages = new ConcurrentDictionary<int, StringBuilder>();
        private volatile int _messageID;
        private void UpdateLog(string msg, int pumpCheck = -1)
        {
            Debug.Assert(_pendingMessages.TryAdd(++_messageID, new StringBuilder().Append("[").Append(DateTime.Now).Append("]").Append("\t").AppendLine(msg)));
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
            Action reset_install_btn = () =>
            {
                btn_Install.Text = "Install";
                _install_cts.Dispose();
            };

            if (btn_Install.Text == "Install")
            {
                _install_cts = new CancellationTokenSource();

                btn_Install.Text = "Cancel";
                _install_task = Task.Factory.StartNew(() => _install.Install(_install_cts.Token));
                _install_task.ContinueWith((task) =>
                {
                    if (btn_Install.InvokeRequired)
                    {
                        btn_Install.Invoke(reset_install_btn);
                    }
                    else
                        reset_install_btn();
                });
            }
            else
            {
                _install_cts.Cancel();
                _install_task.Wait();
            }
        }

        private void chkYou_CheckedChanged(object sender, EventArgs e)
        {
            var checkbox = (sender as CheckBox);
            if (!checkbox.Checked)
            {
                checkbox.Checked = true;
                MessageBox.Show("Impossible");
            }
        }
    }
}