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

namespace TaleOfTwoWastelands
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

            var prog = new Progress<string>();
            prog.ProgressChanged += (in_sender, in_e) => txt_Progress.AppendText(string.Format("[{0}]\t{1}{2}", DateTime.Now, in_e, Environment.NewLine));

            _install = new Installer(prog, dlg_FindGame, dlg_SaveTTW);
            txt_FO3Location.Text = _install.Fallout3Path;
            txt_FNVLocation.Text = _install.FalloutNVPath;
            txt_TTWLocation.Text = _install.TTWSavePath;
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
    }
}