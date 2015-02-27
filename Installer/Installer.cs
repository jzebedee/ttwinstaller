using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using BSAsharp;
using TaleOfTwoWastelands.Install;
using TaleOfTwoWastelands.Progress;
using TaleOfTwoWastelands.Patching;
using TaleOfTwoWastelands.Properties;
using TaleOfTwoWastelands.UI;

namespace TaleOfTwoWastelands
{
	public class Installer : IInstaller
	{
		#region Statics
		public static readonly string PatchDir = Path.Combine(Paths.AssetsDir, "TTW Data", "TTW Patches");
		#endregion

		#region Instance private
		private BsaDiff _bsaDiff;
		private NVSE _nvse;

		private readonly ILog Log;
		private readonly IPrompts Prompts;
		#endregion

		#region Instance public properties
		public string DirFO3Data
		{
			get { return Path.Combine(Prompts.Fallout3Path, "Data"); }
		}

		public string DirFNVData
		{
			get { return Path.Combine(Prompts.FalloutNVPath, "Data"); }
		}

		public string DirTTWMain
		{
			get { return Path.Combine(Prompts.TTWSavePath, Paths.MainDir); }
		}

		public string DirTTWOptional
		{
			get { return Path.Combine(Prompts.TTWSavePath, Paths.OptDir); }
		}

		/// <summary>
		/// Provides progress updates for minor operations
		/// </summary>
		public IProgress<InstallStatus> ProgressMinorOperation { get; set; }
		/// <summary>
		/// Provides progress updates for major operations
		/// </summary>
		public IProgress<InstallStatus> ProgressMajorOperation { get; set; }

		public CancellationToken Token { get; private set; }
		#endregion

		public Installer(ILog log, IPrompts prompts)
		{
			Log = log;
			Prompts = prompts;

			Log.File("Version {0}", Application.ProductVersion);
			Log.File("{0}-bit architecture found.", Environment.Is64BitOperatingSystem ? "64" : "32");
		}

		public void Install(CancellationToken inToken)
		{
			var LinkedSource = CancellationTokenSource.CreateLinkedTokenSource(inToken);

			Token = LinkedSource.Token;

			Prompts.PromptPaths();

			_bsaDiff = DependencyRegistry.Container
				//.With("progress").EqualTo(ProgressMinorOperation)
				//.With("token").EqualTo(Token)
				.GetInstance<BsaDiff>();
			_nvse = DependencyRegistry.Container
				.With("FNVPath").EqualTo(Prompts.FalloutNVPath)
				.GetInstance<NVSE>();

			var opProg = new InstallStatus(ProgressMajorOperation, Token) { ItemsTotal = 7 + Game.BuildableBSAs.Count + Game.CheckedESMs.Length };
			try
			{
				try
				{
					opProg.CurrentOperation = "Checking for required files";

					if (CheckFiles())
					{
						Log.File("All files found.");
						Log.Display("All files found. Proceeding with installation.");
					}
					else
					{
						Log.File("Missing files detected. Aborting install.");
						Log.Display("The above files were not found. Make sure your Fallout 3 location is accurate and try again.\nInstallation failed.");
						return;
					}

					if (!_nvse.Check())
					{
						string err = null;
						//true : should download, continue install
						//false: should not download, continue install
						//null : should not download, abort install
						switch (_nvse.Prompt())
						{
							case true:
								if (_nvse.Install(out err))
									break;
								goto default;
							case false:
								break;
							default:
								Fail(err);
								return;
						}
					}
				}
				finally
				{
					//+1
					opProg.Step();
				}

				try
				{
					const string curOp = "Creating FOMOD foundation";
					opProg.CurrentOperation = curOp;

					Log.File(curOp);

					string
						srcFolder = Path.Combine(Paths.AssetsDir, "TTW Data", "TTW Files"),
						tarFolder = Prompts.TTWSavePath;

					Util.CopyFolder(srcFolder, tarFolder);
				}
				finally
				{
					//+1
					opProg.Step();
				}

				//count BuildableBSAs
				var buildBsaStep = DependencyRegistry.Container.GetInstance<BuildBsasStep>();
				buildBsaStep.Run(opProg, Token);

				try
				{
					opProg.CurrentOperation = "Building SFX";

					BuildSFX();
				}
				finally
				{
					//+1
					opProg.Step();
				}

				try
				{
					opProg.CurrentOperation = "Building Voices";

					BuildVoice();
				}
				finally
				{
					//+1
					opProg.Step();
				}

				try
				{
					const string ttwArchive = "TaleOfTwoWastelands.bsa";
					opProg.CurrentOperation = "Copying " + ttwArchive;

					if (!File.Exists(Path.Combine(DirTTWMain, ttwArchive)))
						File.Copy(Path.Combine(Paths.AssetsDir, "TTW Data", ttwArchive), Path.Combine(DirTTWMain, ttwArchive));
				}
				finally
				{
					//+1
					opProg.Step();
				}

				//count CheckedESMs
				if (!PatchMasters(opProg))
					return;

				//+2
				{
					string
						prefix = "Copying ",
						opA = "Fallout3 music files",
						opB = "Fallout3 video files";

					opProg.CurrentOperation = prefix + opA;
					FalloutLineCopy(opA, Path.Combine(Paths.AssetsDir, "TTW Data", "FO3_MusicCopy.txt"));
					opProg.Step();

					opProg.CurrentOperation = prefix + opB;
					FalloutLineCopy(opB, Path.Combine(Paths.AssetsDir, "TTW Data", "FO3_VideoCopy.txt"));
					opProg.Step();
				}

				if (MessageBox.Show(string.Format(Localization.BuildFOMODsPrompt, Localization.TTW, Localization.SuggestedModManager), Localization.BuildFOMODsQuestion, MessageBoxButtons.YesNo) == DialogResult.Yes)
				{
					//+1 (opt)
					try
					{
						opProg.ItemsTotal++;
						opProg.CurrentOperation = "Building FOMODs";

						BuildFOMODs();
					}
					finally
					{
						opProg.Step();
					}
				}

				opProg.Finish();

				Log.Display("Install completed successfully.");
				MessageBox.Show(string.Format(Localization.InstalledSuccessfully, Localization.TTW));
			}
			catch (OperationCanceledException)
			{
				//intentionally cancelled - swallow exception
				Log.Dual("Install was cancelled.");
			}
			catch (Exception ex)
			{
				Log.File(ex.ToString());
				Fail("An error interrupted the install!");
				MessageBox.Show(string.Format(Localization.ErrorWhileInstalling, ex.Message), Localization.Error);
			}
		}

		private bool ShowSkipDialog(string description)
		{
			switch (MessageBox.Show(string.Format(Localization.AlreadyExistOverwritePrompt, description), Localization.OverwriteFiles, MessageBoxButtons.YesNo))
			{
				case DialogResult.Yes:
					return false;
				default:
					return true;
			}
		}

		private void BuildSFX()
		{
			var fo3BsaPath = Path.Combine(DirFO3Data, "Fallout - Sound.bsa");

			var songsPath = Path.Combine("sound", "songs");

			bool skipSongs = false, skipSFX = false;
			if (Directory.Exists(Path.Combine(DirTTWMain, songsPath)))
				skipSongs = ShowSkipDialog("Fallout 3 songs");

			var outBsaPath = Path.Combine(DirTTWOptional, "Fallout3 Sound Effects", "TaleOfTwoWastelands - SFX.bsa");
			if (File.Exists(outBsaPath))
				skipSFX = ShowSkipDialog("Fallout 3 sound effects");

			if (skipSongs && skipSFX)
				return;

			var bsaInstaller = DependencyRegistry.Container.GetInstance<BsaInstaller>();
			using (BSA
			   inBsa = new BSA(fo3BsaPath),
			   outBsa = new BSA(inBsa.Settings))
			{
				if (!skipSongs)
				{
					Log.Display("Extracting songs");
					bsaInstaller.Extract(Token, inBsa.Where(folder => folder.Path.StartsWith(songsPath)), "Fallout - Sound", DirTTWMain, false);
				}

				if (skipSFX)
					return;

				Log.Display("Building optional TaleOfTwoWastelands - SFX.bsa...");

				var fxuiPath = Path.Combine("sound", "fx", "ui");

				var includedFilenames = new HashSet<string>(File.ReadLines(Path.Combine(Paths.AssetsDir, "TTW Data", "TTW_SFXCopy.txt")));

				var includedGroups =
					from folder in inBsa.Where(folder => folder.Path.StartsWith(fxuiPath))
					from file in folder
					where includedFilenames.Contains(file.Filename)
					group file by folder;

				foreach (var group in includedGroups)
				{
					//make folder only include files that matched includedFilenames
					@group.Key.IntersectWith(@group);

					//add folders back into output BSA
					outBsa.Add(@group.Key);
				}

				Log.File("Building TaleOfTwoWastelands - SFX.bsa.");
				outBsa.Save(outBsaPath);

				Log.Display("\tDone");
			}
		}

		private void BuildVoice()
		{
			var outBsaPath = Path.Combine(DirTTWOptional, "Fallout3 Player Voice", "TaleOfTwoWastelands - PlayerVoice.bsa");
			if (File.Exists(outBsaPath))
				return;

			var inBsaPath = Path.Combine(DirFO3Data, "Fallout - Voices.bsa");

			using (BSA
				inBsa = new BSA(inBsaPath),
				outBsa = new BSA(inBsa.Settings))
			{
				var includedFolders = inBsa
					.Where(folder => Game.VoicePaths.ContainsKey(folder.Path))
					.Select(folder => new BSAFolder(Game.VoicePaths[folder.Path], folder));

				foreach (var folder in includedFolders)
					outBsa.Add(folder);

				outBsa.Save(outBsaPath);
			}
		}

		private bool PatchMasters(InstallStatus opProg)
		{
			foreach (var esm in Game.CheckedESMs)
				try
				{
					opProg.CurrentOperation = "Patching " + esm;

					if (Token.IsCancellationRequested || !PatchMaster(esm))
						return false;
				}
				finally
				{
					opProg.Step();
				}

			return true;
		}

		private void BuildFOMODs()
		{
			var status = new InstallStatus(ProgressMinorOperation, Token);

			var fomod = DependencyRegistry.Container.GetInstance<FOMOD>();
			fomod.BuildAll(status, DirTTWMain, DirTTWOptional, Prompts.TTWSavePath);
		}

		private void FalloutLineCopy(string name, string path)
		{
			bool skipExisting = false, asked = false;

			Log.Dual("Copying " + name + "...");
			foreach (var line in File.ReadLines(path))
			{
				var ttwLinePath = Path.Combine(DirTTWMain, line);
				var foLinePath = Path.Combine(DirFO3Data, line);

				var newDirectory = Path.GetDirectoryName(ttwLinePath);
				Directory.CreateDirectory(newDirectory);

				if (File.Exists(foLinePath))
				{
					if (File.Exists(ttwLinePath) && !asked)
					{
						if (skipExisting)
							continue;
						else
						{
							asked = true;
							skipExisting = ShowSkipDialog(name);
						}
					}

					try
					{
						File.Copy(foLinePath, ttwLinePath, true);
					}
					catch (UnauthorizedAccessException error)
					{
						Log.File("\tERROR: " + line + " did not copy successfully due to: Unauthorized Access Exception " + error.Source + ".");
					}
				}
				else
					Log.File("\tFile not found:\t" + foLinePath);
			}
			Log.Dual("\tDone.");
		}

		private static bool CheckExisting(string path, FileValidation newChk)
		{
			using (var existingChk = new FileValidation(path, newChk.Type))
				return newChk == existingChk;
		}

		private bool PatchMaster(string esm)
		{
			Log.Dual("Patching " + esm + "...");

			var patchPath = Path.Combine(PatchDir, Path.ChangeExtension(esm, ".pat"));
			if (File.Exists(patchPath))
			{
				var patchDict = new PatchDict(patchPath);

				Debug.Assert(patchDict.ContainsKey(esm));
				var patch = patchDict[esm];
				var patches = patch.Item2;
				var newChk = patch.Item1;

				var finalPath = Path.Combine(DirTTWMain, esm);
				if (File.Exists(finalPath))
				{
					Log.Dual("\t" + esm + " already exists");
					if (CheckExisting(finalPath, newChk))
					{
						Log.Dual("\t" + esm + " is up to date");
						return true;
					}

					Log.Dual("\t" + esm + " is out of date");
				}

				var dataPath = Path.Combine(DirFO3Data, esm);
				//TODO: change to a user-friendly condition and message
				Trace.Assert(File.Exists(dataPath));

				//make sure we didn't include old patches by mistake
				Debug.Assert(patches.All(p => p.Metadata.Type == FileValidation.ChecksumType.Murmur128));

				using (var dataChk = new FileValidation(dataPath))
				{
					var matchPatch = patches.SingleOrDefault(p => p.Metadata == dataChk);
					if (matchPatch == null)
					{
						Log.Display("\tA patch for your version of " + esm + " could not be found");
						Log.File("\tA patch for " + esm + " version " + dataChk + " could not be found");
					}
					else
					{
						using (FileStream
							dataStream = File.OpenRead(dataPath),
							outputStream = File.Open(finalPath, FileMode.Create, FileAccess.ReadWrite))
						{
							FileValidation outputChk;
							if (matchPatch.PatchStream(dataStream, newChk, outputStream, out outputChk))
							{
								Log.Dual("\tPatch successful");
								return true;
							}

							Log.File("\tPatch failed - " + outputChk);
						}
					}
				}
			}
			else
				Log.Dual("\t" + esm + " patch is missing from " + PatchDir);

			Fail("Your version of " + esm + " cannot be patched. This is abnormal.");

			return false;
		}

		private void Fail(string msg = null)
		{
			if (msg != null)
				Log.Dual(msg);
			Log.Dual("Install aborted.");
		}

		private bool CheckFiles()
		{
			const string errFileNotFound = "{0} could not be found.";
			bool fileCheck = true;

			Log.Dual("Checking for required files...");

			foreach (var esm in Game.CheckedESMs)
			{
				var ttwESM = Path.Combine(DirTTWMain, esm);
				var dataESM = Path.Combine(DirFO3Data, esm);
				if (!File.Exists(ttwESM) && !File.Exists(dataESM))
				{
					var errMsg = string.Format(errFileNotFound, esm);

					Log.Dual(errMsg);

					fileCheck = false;
				}
			}

			foreach (var kvp in Game.CheckedBSAs)
			{
				//Key = TTW BSA
				//Value = string[] of FO3 sub-BSAs
				if (!File.Exists(kvp.Key))
				{
					foreach (var subBSA in kvp.Value)
					{
						var pathedSubBSA = Path.Combine(DirFO3Data, subBSA);
						if (!File.Exists(pathedSubBSA))
						{
							var errMsg = string.Format(errFileNotFound, subBSA);

							Log.Dual(errMsg);

							fileCheck = false;
						}
					}
				}
			}

			return fileCheck;
		}
	}
}
