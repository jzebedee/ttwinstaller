using System.IO;
using System.Windows.Forms;
using TaleOfTwoWastelands.Properties;

namespace TaleOfTwoWastelands.UI
{
    public class Prompts : IPrompts
	{
        private readonly FileDialog openDialog, saveDialog;
        private readonly ILog Log;

        public string Fallout3Path { get; private set; }
        public string FalloutNVPath { get; private set; }
        public string TTWSavePath { get; private set; }

        public Prompts(OpenFileDialog openDialog, SaveFileDialog saveDialog, ILog log)
        {
            Log = log;

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
            openDialog.Title = Localization.Fallout3;
            return (Fallout3Path = FindByUserPrompt(openDialog, Localization.Fallout3, "Fallout3", manual));
        }

        public string FalloutNVPrompt(bool manual = false)
        {
            openDialog.FilterIndex = 2;
            openDialog.Title = Localization.FalloutNewVegas;
            return (FalloutNVPath = FindByUserPrompt(openDialog, Localization.FalloutNewVegas, "FalloutNV", manual));
        }

        public string TTWPrompt(bool manual = false)
        {
            return (TTWSavePath = FindByUserPrompt(saveDialog, "Tale of Two Wastelands", "TaleOfTwoWastelands", manual));
        }

		public bool OverwritePrompt(string name, string path)
		{
			if (!File.Exists(path))
				return true;

			Log.File("File \"{0}\" does not exist", path);

			var promptResult = MessageBox.Show(string.Format(Localization.RebuildPrompt, name), Localization.FileAlreadyExists, MessageBoxButtons.YesNo);
			switch (promptResult)
			{
				case DialogResult.Yes:
					File.Delete(path);
					Log.File("Rebuilding {0}", name);
					return true;
				case DialogResult.No:
					Log.Dual("{0} has already been built. Skipping.", name);
					break;
			}

			return false;
		}

		public bool BuildPrompt(string name, string path)
		{
			if (!OverwritePrompt(name, path))
				return false;

			Log.Dual("Building {0}", name);
			return true;
		}

		public ErrorPromptResult PatchingErrorPrompt(string file)
		{
			var promptResult = MessageBox.Show(string.Format(Localization.ErrorWhilePatching, file), Localization.Error, MessageBoxButtons.AbortRetryIgnore);
			switch (promptResult)
			{
				//case DialogResult.Abort: //Quit install
				case DialogResult.Retry:   //Start over from scratch
					Log.Dual("Retrying build.");
					return ErrorPromptResult.Retry;
				case DialogResult.Ignore:  //Ignore errors and move on
					Log.Dual("Ignoring errors.");
					return ErrorPromptResult.Continue;
			}

			return ErrorPromptResult.Abort;
		}
	}
}
