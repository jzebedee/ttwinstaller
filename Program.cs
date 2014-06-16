using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TaleOfTwoWastelands
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
#if BUILD_PATCHDB
            var result = MessageBox.Show("Building PatchDB from 'BuildDB' folder. Existing files are skipped. OK?", "", MessageBoxButtons.OKCancel);
            if (result != DialogResult.OK)
                return;

            TaleOfTwoWastelands.Patching.BuildPatchDB.Build();

            return;
#endif

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UI.frm_Main());
        }
    }
}