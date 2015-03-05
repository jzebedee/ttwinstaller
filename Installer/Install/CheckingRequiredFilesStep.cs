using System.IO;
using System.Threading;
using TaleOfTwoWastelands.Progress;
using TaleOfTwoWastelands.Properties;
using System.Linq;
using TaleOfTwoWastelands.UI;

namespace TaleOfTwoWastelands.Install
{
	class CheckingRequiredFilesStep : IInstallStep
	{
		private readonly IInstaller _installer;
		private readonly ILog Log;

		public CheckingRequiredFilesStep(IInstaller installer, ILog log)
		{
			Log = log;
			_installer = installer;
		}

		public bool? Run(IInstallStatusUpdate status, CancellationToken token)
		{
			status.CurrentOperation = Localization.CheckingRequiredFiles;

			if (CheckFiles())
			{
				Log.File("All files found.");
				Log.Display("All files found. Proceeding with installation.");
				return true;
			}
			else
			{
				Log.File("Missing files detected. Aborting install.");
				Log.Display("The above files were not found. Make sure your Fallout 3 location is accurate and try again.\nInstallation failed.");
				return false;
			}
		}

		private bool CheckFiles()
		{
			bool fileCheck = true;

			Log.Dual(Localization.CheckingRequiredFiles);

			foreach (var esm in Game.CheckedESMs)
			{
				var ttwESM = Path.Combine(_installer.DirTTWMain, esm);
				var dataESM = Path.Combine(_installer.DirFO3Data, esm);
				if (!File.Exists(ttwESM) && !File.Exists(dataESM))
				{
					Log.Dual(Localization.FileDoesNotExist, esm);
					fileCheck = false;
				}
			}

			foreach (var subBSA in (from kvp in Game.CheckedBSAs
									where !File.Exists(kvp.Key)
									from sb in kvp.Value
									select sb))
			{
				//Key = TTW BSA
				//Value = string[] of FO3 sub-BSAs
				var pathedSubBSA = Path.Combine(_installer.DirFO3Data, subBSA);
				if (!File.Exists(pathedSubBSA))
				{
					Log.Dual(Localization.FileDoesNotExist, subBSA);
					fileCheck = false;
				}
			}

			return fileCheck;
		}
	}
}
