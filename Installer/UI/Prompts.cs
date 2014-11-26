using System.IO;
using System.Windows.Forms;
using TaleOfTwoWastelands.Properties;

namespace TaleOfTwoWastelands.UI
{
    public class Prompts
    {
        private readonly FileDialog openDialog, saveDialog;

        public string Fallout3Path { get; private set; }
        public string FalloutNVPath { get; private set; }
        public string TTWSavePath { get; private set; }

        internal Prompts(OpenFileDialog openDialog, SaveFileDialog saveDialog)
        {
            this.openDialog = openDialog;
            this.saveDialog = saveDialog;

            if (Program.IsElevated)
            {
                Fallout3Path = RegistryHelper.GetPathFromKey("Fallout3");
                FalloutNVPath = RegistryHelper.GetPathFromKey("FalloutNV");
                TTWSavePath = RegistryHelper.GetPathFromKey("TaleOfTwoWastelands");
            }
        }

        public void PromptPaths()
        {
            Log.File("Looking for Fallout3.exe");
            if (File.Exists(Path.Combine(Fallout3Path, "Fallout3.exe")))
            {
                Log.File("\tFound.");
            }
            else
            {
                Fallout3Path = Fallout3Prompt();
            }

            Log.File("Looking for FalloutNV.exe");
            if (File.Exists(Path.Combine(FalloutNVPath, "FalloutNV.exe")))
            {
                Log.File("\tFound.");
            }
            else
            {
                FalloutNVPath = FalloutNVPrompt();
            }

            Log.File("Looking for Tale of Two Wastelands");
            if (TTWSavePath != null && TTWSavePath != "\\")
            {
                Log.File("\tDefault path found.");
            }
            else
            {
                TTWSavePath = TTWPrompt();
            }
        }

        private string FindByUserPrompt(FileDialog dialog, string name, string keyName, bool manual = false)
        {
            Log.File("Prompting user for {0}'s path.", name);
            MessageBox.Show(string.Format("Please select {0}'s location.", name));

            var dlgResult = dialog.ShowDialog();
            if (dlgResult == DialogResult.OK)
            {
                var path = Path.GetDirectoryName(dialog.FileName);
                Log.File("User {2}changed {0} directory to '{1}'", name, path, manual ? "manually " : " ");

                RegistryHelper.SetPathFromKey(keyName, path);

                return path;
            }

            return null;
        }

        public string Fallout3Prompt(bool manual = false)
        {
            openDialog.FilterIndex = 1;
            openDialog.Title = Resources.Fallout3;
            return FindByUserPrompt(openDialog, Resources.Fallout3, "Fallout3", manual);
        }

        public string FalloutNVPrompt(bool manual = false)
        {
            openDialog.FilterIndex = 2;
            openDialog.Title = Resources.FalloutNewVegas;
            return FindByUserPrompt(openDialog, Resources.FalloutNewVegas, "FalloutNV", manual);
        }

        public string TTWPrompt(bool manual = false)
        {
            return FindByUserPrompt(saveDialog, "Tale of Two Wastelands", "TaleOfTwoWastelands", manual);
        }
    }
}
