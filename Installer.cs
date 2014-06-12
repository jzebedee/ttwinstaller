using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using BSAsharp;
using System.Diagnostics;

namespace TaleOfTwoWastelands
{
    class Installer
    {
        private const string MainDir = "Main Files", OptDir = "Optional Files";
        public const string AssetsDir = "resources";

        static readonly HashSet<string> CheckedESMs = new HashSet<string>(new[] { "Fallout3.esm", "Anchorage.esm", "ThePitt.esm", "BrokenSteel.esm", "PointLookout.esm", "Zeta.esm" });
        static readonly Dictionary<string, string> VoicePaths = new Dictionary<string, string> {
            {Path.Combine("sound", "voice", "fallout3.esm", "playervoicemale"), Path.Combine("PlayerVoice", "sound", "voice", "falloutnv.esm", "playervoicemale")},
            {Path.Combine("sound", "voice", "fallout3.esm", "playervoicefemale"), Path.Combine("PlayerVoice", "sound", "voice", "falloutnv.esm", "playervoicefemale")}
        };
        static readonly Dictionary<string, string[]> CheckedBSAs = new Dictionary<string, string[]> {
            {"Fallout3 - Main.bsa", new[] {"Fallout - Meshes.bsa", "Fallout - Misc.bsa", "Fallout - Textures.bsa"}},
            {"Fallout3 - Sounds.bsa", new[] {"Fallout - MenuVoices.bsa", "Fallout - Sound.bsa", "Fallout - Voices.bsa"}},
            {"Fallout3 - DLC.bsa",
                new[] {
                    "Anchorage - Main.bsa", "Anchorage - Sounds.bsa",
                    "ThePitt - Main.bsa", "ThePitt - Sounds.bsa",
                    "BrokenSteel - Main.bsa", "BrokenSteel - Sounds.bsa",
                    "PointLookout - Main.bsa", "PointLookout - Sounds.bsa",
                    "Zeta - Main.bsa", "Zeta - Sounds.bsa"
                }
            }
        };
        static readonly Dictionary<string, string> BuildableBSAs = new Dictionary<string, string>
        {
            {"Fallout - Meshes", "Fallout3 - Meshes"},
            {"Fallout - Misc", "Fallout3 - Misc"},
            {"Fallout - Sound", "Fallout3 - Sound"},
            {"Fallout - Textures", "Fallout3 - Textures"},
            {"Fallout - MenuVoices", "Fallout3 - MenuVoices"},
            {"Fallout - Voices", "Fallout3 - Voices"},
            {"Anchorage - Main", "Anchorage - Main"},
            {"Anchorage - Sounds", "Anchorage - Sounds"},
            {"ThePitt - Main", "ThePitt - Main"},
            {"ThePitt - Sounds", "ThePitt - Sounds"},
            {"BrokenSteel - Main", "BrokenSteel - Main"},
            {"BrokenSteel - Sounds", "BrokenSteel - Sounds"},
            {"PointLookout - Main", "PointLookout - Main"},
            {"PointLookout - Sounds", "PointLookout - Sounds"},
            {"Zeta - Main", "Zeta - Main"},
            {"Zeta - Sounds", "Zeta - Sounds"},
        };

        static readonly string TTWBase = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "TaleOfTwoWastelands");

        readonly private IProgress<string> progressLog;
        readonly private StreamWriter logFile;
        readonly private Action<string> WriteLog;

        readonly string dirFO3Data, dirFNVData;
        readonly Dictionary<string, string> CheckSums;
        readonly RegistryKey fo3Key, fnvKey, ttwKey;

        private string dirTTWMain { get { return Path.Combine(TTWSavePath, MainDir); } }
        private string dirTTWOptional { get { return Path.Combine(TTWSavePath, OptDir); } }

        private CancellationToken Token { get; set; }

        public string Fallout3Path { get; private set; }
        public string FalloutNVPath { get; private set; }
        public string TTWSavePath { get; private set; }

        public IProgress<string> Progress { get; private set; }

        public Installer(IProgress<string> progress, OpenFileDialog openDialog, SaveFileDialog saveDialog)
        {
            this.Progress = progress;

            //Create TTW log directory
            Directory.CreateDirectory(TTWBase);

            //Create and open TTW log file
            var logFilename = "Install Log " + DateTime.Now.ToString("MM_dd_yyyy - HH_mm_ss") + ".txt";
            var logFilepath = Path.Combine(TTWBase, logFilename);
            logFile = new StreamWriter(logFilepath, true) { AutoFlush = true };

            this.progressLog = new Progress<string>(log => logFile.WriteLine("[{0}]\t{1}", DateTime.Now, log));
            WriteLog = (s) => progressLog.Report(s);

            BSADiff.PatchDir = Path.Combine(AssetsDir, "TTW Data", "TTW Patches");

            RegistryKey bethKey;
            //determine software reg path (depends on architecture)
            if (Environment.Is64BitOperatingSystem) //64-bit
            {
                WriteLog("\t64-bit architecture found.");
                bethKey = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node", RegistryKeyPermissionCheck.ReadWriteSubTree);
            }
            else //32-bit
            {
                WriteLog("\t32-bit architecture found.");
                bethKey = Registry.LocalMachine.OpenSubKey("Software", RegistryKeyPermissionCheck.ReadWriteSubTree);
            }

            //create or retrieve BethSoft path
            bethKey = bethKey.CreateSubKey("Bethesda Softworks", RegistryKeyPermissionCheck.ReadWriteSubTree);

            //create or retrieve FO3 path
            fo3Key = bethKey.CreateSubKey("Fallout3");
            Fallout3Path = fo3Key.GetValue("Installed Path", "").ToString();
            dirFO3Data = Path.Combine(Fallout3Path, "Data");

            //create or retrieve FNV path
            fnvKey = bethKey.CreateSubKey("FalloutNV");
            FalloutNVPath = fnvKey.GetValue("Installed Path", "").ToString();
            dirFNVData = Path.Combine(FalloutNVPath, "Data");

            //create or retrieve TTW path
            ttwKey = bethKey.CreateSubKey("TaleOfTwoWastelands");
            TTWSavePath = ttwKey.GetValue("Installed Path", "").ToString();

            CheckSums = BuildChecksumDictionary(Path.Combine(AssetsDir, "TTW Data", "TTW Patches", "TTW_Checksums.txt"));

            InstallChecks(openDialog, saveDialog);
        }

        public void Fallout3Prompt(FileDialog open, bool manual = false)
        {
            open.FilterIndex = 1;
            open.Title = "Fallout 3";
            Fallout3Path = FindByUserPrompt(open, "Fallout 3", fo3Key, manual);
        }

        public void FalloutNVPrompt(FileDialog open, bool manual = false)
        {
            open.FilterIndex = 2;
            open.Title = "Fallout New Vegas";
            FalloutNVPath = FindByUserPrompt(open, "Fallout New Vegas", fnvKey, manual);
        }

        public void TTWPrompt(FileDialog save, bool manual = false)
        {
            TTWSavePath = FindByUserPrompt(save, "Tale of Two Wastelands", ttwKey, manual);
        }

        public void Install(CancellationToken inToken)
        {
            this.Token = inToken;

            try
            {
                if (CheckFiles())
                {
                    WriteLog("All files found.");
                    Progress.Report("All files found. Proceeding with installation.");
                }
                else
                {
                    WriteLog("Missing files detected. Aborting install.");
                    Progress.Report("The above files were not found. Make sure your Fallout 3 location is accurate and try again.\nInstallation failed.");
                    return;
                }

                WriteLog("Creating FOMOD foundation.");
                Util.CopyFolder(Path.Combine(AssetsDir, "TTW Data", "TTW Files"), TTWSavePath, (s) => WriteLog(s));

                BuildBSAs();

                BuildSFX();

                BuildVoice();

                if (!File.Exists(Path.Combine(dirTTWMain, "TaleOfTwoWastelands.bsa")))
                    File.Copy(Path.Combine(AssetsDir, "TTW Data", "TaleOfTwoWastelands.bsa"), Path.Combine(dirTTWMain, "TaleOfTwoWastelands.bsa"));

                if (!PatchMasters())
                    return;

                FalloutLineCopy("Fallout3 music files", Path.Combine(AssetsDir, "TTW Data", "FO3_MusicCopy.txt"));
                FalloutLineCopy("Fallout3 video files", Path.Combine(AssetsDir, "TTW Data", "FO3_VideoCopy.txt"));

                if (MessageBox.Show("Tale of Two Wastelands is easiest to install via a mod manager (such as Nexus Mod Manager). Manual installation is possible but not suggested.\n\nWould like the installer to automatically build FOMODs?", "Build FOMODs?", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    BuildFOMODs();
                }

                Progress.Report("Install completed successfully.");
                MessageBox.Show("Tale of Two Wastelands has been installed successfully.");
            }
            catch (OperationCanceledException)
            {
                //intentionally cancelled - swallow exception
                WriteLog("Install was cancelled.");
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message);
                Progress.Report(ex.Message);
                MessageBox.Show("An unhandled exception has occurred:\n" + ex.Message, "Exception");
            }
        }

        private bool ShowSkipDialog(string description)
        {
            switch (MessageBox.Show(description + " already exist. Would you like to overwrite them?", "Overwrite Files", MessageBoxButtons.YesNo))
            {
                case DialogResult.Yes:
                    return false;
                default:
                    return true;
            }
        }

        private void BuildBSAs()
        {
            foreach (var KVP in BuildableBSAs)
            {
                //inBSA - KVP.Key
                //outBSA - KVP.Value

                DialogResult buildResult;
                do
                {
                    buildResult = BuildBSA(Token, KVP.Key, KVP.Value);
                } while (!Token.IsCancellationRequested && buildResult == DialogResult.Retry);

                if (Token.IsCancellationRequested || buildResult == DialogResult.Abort)
                    return;
            }
        }

        private void BuildSFX()
        {
            var fo3BsaPath = Path.Combine(dirFO3Data, "Fallout - Sound.bsa");

            var inBsa = new BSAWrapper(fo3BsaPath);
            var outBsa = new BSAWrapper(inBsa.Settings);

            Progress.Report("Extracting songs");

            var songsPath = Path.Combine("sound", "songs");
            bool skipExisting = false;
            if (Directory.Exists(Path.Combine(dirTTWMain, songsPath)))
                skipExisting = ShowSkipDialog("Fallout 3 songs");
            BSA.ExtractBSA(progressLog, Token, inBsa.Where(folder => folder.Path.StartsWith(songsPath)), dirTTWMain, skipExisting, "Fallout - Sound");

            var outBsaPath = Path.Combine(dirTTWOptional, "Fallout3 Sound Effects", "TaleOfTwoWastelands - SFX.bsa");
            if (File.Exists(outBsaPath))
                return;

            Progress.Report("Building optional TaleOfTwoWastelands - SFX.bsa...");

            var fxuiPath = Path.Combine("sound", "fx", "ui");

            var includedFilenames = new HashSet<string>(File.ReadLines(Path.Combine(AssetsDir, "TTW Data", "TTW_SFXCopy.txt")));

            var includedGroups =
                from folder in inBsa.Where(folder => folder.Path.StartsWith(fxuiPath))
                from file in folder
                where includedFilenames.Contains(file.Filename)
                group file by folder;

            foreach (var group in includedGroups)
            {
                //make folder only include files that matched includedFilenames
                group.Key.IntersectWith(group);

                //add folders back into output BSA
                outBsa.Add(group.Key);
            }

            WriteLog("Building TaleOfTwoWastelands - SFX.bsa.");
            outBsa.Save(outBsaPath);

            Progress.Report("Done\n");
        }

        private void BuildVoice()
        {
            var outBsaPath = Path.Combine(dirTTWOptional, "Fallout3 Player Voice", "TaleOfTwoWastelands - PlayerVoice.bsa");
            if (File.Exists(outBsaPath))
                return;

            var inBsaPath = Path.Combine(dirFO3Data, "Fallout - Voices.bsa");

            var inBsa = new BSAWrapper(inBsaPath);
            var outBsa = new BSAWrapper(inBsa.Settings);

            var includedFolders = inBsa
                .Where(folder => VoicePaths.ContainsKey(folder.Path))
                .Select(folder => new BSAFolder(VoicePaths[folder.Path], folder));

            Trace.Assert(includedFolders.All(folder => outBsa.Add(folder)));
            outBsa.Save(outBsaPath);
        }

        private bool PatchMasters()
        {
            foreach (var ESM in CheckedESMs)
                if (Token.IsCancellationRequested || !PatchFile(ESM))
                    return false;

            return true;
        }

        private void BuildFOMODs()
        {
            WriteLog("Building FOMODs.");
            Progress.Report("Building FOMODs...\n\tThis can take some time.");
            Util.BuildFOMOD(dirTTWMain, Path.Combine(TTWSavePath, "TaleOfTwoWastelands_Main.fomod"));
            Util.BuildFOMOD(dirTTWOptional, Path.Combine(TTWSavePath, "TaleOfTwoWastelands_Options.fomod"));
            WriteLog("Done.");
            Progress.Report("FOMODs built.");
        }

        private void FalloutLineCopy(string name, string path)
        {
            bool skipExisting = false, asked = false;

            WriteLog("Copying " + name);
            Progress.Report("Copying " + name + "...");
            foreach (var line in File.ReadLines(path))
            {
                var linePath = Path.Combine(dirTTWMain, line);
                var foLinePath = Path.Combine(dirFO3Data, line);

                var newDirectory = Path.GetDirectoryName(linePath);

                if (Directory.Exists(newDirectory) && !asked)
                {
                    asked = true;
                    skipExisting = ShowSkipDialog(name);
                }

                Directory.CreateDirectory(newDirectory);
                if (File.Exists(foLinePath))
                {
                    if (File.Exists(linePath) && skipExisting)
                        continue;

                    try
                    {
                        File.Copy(foLinePath, linePath, true);
                    }
                    catch (System.UnauthorizedAccessException error)
                    {
                        WriteLog("ERROR: " + line + " did not copy successfully due to: Unauthorized Access Exception " + error.Source + ".");
                    }
                }
                else
                    WriteLog("File Not Found:\t" + foLinePath);
            }
            WriteLog("Done.");
            Progress.Report("Done.");
        }

        private bool PatchFile(string filePatch, bool bSearchFO3 = true)
        {
            string newChecksum, curChecksum;
            WriteLog("Patching " + filePatch + "...");
            Progress.Report("Patching " + filePatch + "...");

            var patchPath = Path.Combine(dirTTWMain, filePatch);
            var patchPathNew = Path.Combine(dirTTWMain, Path.ChangeExtension(filePatch, ".new"));

            if (File.Exists(patchPath))
            {
                CheckSums.TryGetValue(filePatch, out newChecksum);
                curChecksum = Util.GetChecksum(Path.Combine(dirTTWMain, filePatch));

                var diffPath = Path.Combine(AssetsDir, "TTW Data", "TTW Patches", filePatch + "." + curChecksum + "." + newChecksum + ".diff");

                if (curChecksum == newChecksum)
                {
                    Progress.Report(filePatch + " is up to date.");
                    WriteLog(patchPath + " is already up to date.");
                    return true;
                }
                else if (File.Exists(diffPath))
                {
                    WriteLog("\tApplying patch " + diffPath);
                    if (Util.ApplyPatch(CheckSums, patchPath, diffPath, patchPathNew))
                    {
                        File.Replace(patchPathNew, patchPath, null);
                        WriteLog("Patch successful.");
                        Progress.Report("Patch successful.");
                        return true;
                    }
                    else
                        WriteLog("Patch failed.");
                }
                else
                {
                    WriteLog("No patch exists for " + patchPath + ", deleting.");
                    File.Delete(patchPath);
                }
            }
            else
                WriteLog("\t" + filePatch + " not found in " + dirTTWMain);

            var fo3PatchPath = Path.Combine(dirFO3Data, filePatch);
            if (File.Exists(fo3PatchPath) && bSearchFO3)
            {
                WriteLog("\tChecking " + Fallout3Path);

                CheckSums.TryGetValue(filePatch, out newChecksum);
                curChecksum = Util.GetChecksum(fo3PatchPath);

                var diffPath = Path.Combine(AssetsDir, "TTW Data", "TTW Patches", filePatch + "." + curChecksum + "." + newChecksum + ".diff");

                if (curChecksum == newChecksum)
                {
                    Progress.Report(filePatch + " is up to date.");
                    WriteLog(fo3PatchPath + " is already up to date. Moving to " + dirTTWMain);
                    File.Copy(fo3PatchPath, patchPath);
                    return true;
                }
                else if (File.Exists(diffPath))
                {
                    WriteLog("\tApplying patch " + diffPath);
                    if (Util.ApplyPatch(CheckSums, fo3PatchPath, diffPath, patchPath))
                    {
                        WriteLog("Patch successful.");
                        Progress.Report("Patch successful.");
                        return true;
                    }
                    else
                    {
                        WriteLog("Patch failed.");
                        Progress.Report("Patch failed for an unknown reason, try to install again. If this problem persists, please report it.");
                        return false;
                    }
                }
                else
                {
                    WriteLog("No patch for this version of " + fo3PatchPath + " Exists. Install aborted.");
                    Progress.Report("Your version of " + filePatch + " cannot be patched.\n" +
                        "\tCurrently Tale of Two Wastelands only works on legal, fully patched versions of Fallout3.\n" +
                        "Install aborted.");
                    return false;
                }
            }
            else if (bSearchFO3)
            {
                WriteLog(filePatch + " could not be found. Install aborted.");
                Progress.Report("The installer could not find " + filePatch + " in " + dirTTWMain + " or " + dirFO3Data +
                    "\t Make sure you have selected the proper paths.\n" +
                    "\nInstall aborted.");
                return false;
            }
            else
            {
                WriteLog(patchPath + " cannot be patched. Install aborted.");
                Progress.Report("Your version of " + filePatch + " cannot be patched. This is abnormal.");
                return false;
            }
        }

        private DialogResult BuildBSA(CancellationToken token, string inBSA, string outBSA)
        {
            string outBSAFile = Path.ChangeExtension(outBSA, ".bsa");
            string outBSAPath = Path.Combine(dirTTWMain, outBSAFile);

            if (File.Exists(outBSAPath))
            {
                switch (MessageBox.Show(outBSAFile + " already exists. Rebuild?", "File Already Exists", MessageBoxButtons.YesNo))
                {
                    case System.Windows.Forms.DialogResult.Yes:
                        File.Delete(outBSAPath);
                        WriteLog("Rebuilding " + outBSA);
                        Progress.Report("Rebuilding " + outBSA);
                        break;
                    case System.Windows.Forms.DialogResult.No:
                        WriteLog(outBSA + " has already been built. Skipping.");
                        Progress.Report(outBSA + " has already been built. Skipping.");
                        return DialogResult.No;
                }
            }
            else
            {
                WriteLog("Building " + outBSA);
                Progress.Report("Building " + outBSA);
            }

            string inBSAFile = Path.ChangeExtension(inBSA, ".bsa");
            string inBSAPath = Path.Combine(dirFO3Data, inBSAFile);

            var errors = BSADiff.PatchBSA(progressLog, token, inBSAPath, outBSAPath);

            WriteLog(errors);
            Progress.Report(errors);

            if (errors.Length > 0)
            {
                var errPeek = string.Join(Environment.NewLine, errors.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Take(10));
                switch (MessageBox.Show("The following errors have occurred:\r\n" + errPeek, "Error Log", MessageBoxButtons.AbortRetryIgnore))
                {
                    case System.Windows.Forms.DialogResult.Abort:   //Quit install
                        WriteLog("Install Aborted.");
                        Progress.Report("Install Aborted.");
                        return System.Windows.Forms.DialogResult.Abort;
                    case System.Windows.Forms.DialogResult.Retry:   //Start over from scratch
                        WriteLog("Retrying build.");
                        Progress.Report("Retrying build.");
                        return System.Windows.Forms.DialogResult.Retry;
                    case System.Windows.Forms.DialogResult.Ignore:  //Ignore errors and move on
                        WriteLog("Ignoring errors.");
                        Progress.Report("Ignoring errors.");
                        return System.Windows.Forms.DialogResult.Ignore;
                }
            }

            WriteLog("Build successful.");
            Progress.Report("Build successful.");
            return System.Windows.Forms.DialogResult.OK;
        }

        private void InstallChecks(FileDialog open, FileDialog save)
        {
            WriteLog("Looking for Fallout3.exe");
            if (File.Exists(Path.Combine(Fallout3Path, "Fallout3.exe")))
            {
                WriteLog("\tFound.");
            }
            else
            {
                Fallout3Prompt(open);
            }

            WriteLog("Looking for FalloutNV.exe");
            if (File.Exists(Path.Combine(FalloutNVPath, "FalloutNV.exe")))
            {
                WriteLog("\tFound.");
            }
            else
            {
                FalloutNVPrompt(open);
            }

            WriteLog("Looking for Tale of Two Wastelands");
            if (TTWSavePath != null && TTWSavePath != "\\")
            {
                WriteLog("\tDefault path found.");
            }
            else
            {
                TTWPrompt(save);
            }
        }

        private string FindByUserPrompt(FileDialog dlg, string name, RegistryKey regKey, bool manual = false)
        {
            WriteLog(string.Format("\t{0} not found, prompting user.", name));
            MessageBox.Show(string.Format("Could not automatically find {0}'s location, please manually indicate its location.", name));

            DialogResult dlgResult;
            do
            {
                dlgResult = dlg.ShowDialog();
                if (dlgResult == DialogResult.OK)
                {
                    var path = Path.GetDirectoryName(dlg.FileName);
                    if (manual)
                        WriteLog(string.Format("User manually changed {0} directory to: {1}", name, path));
                    else
                        WriteLog("User selected: " + path);
                    regKey.SetValue("Installed Path", path, RegistryValueKind.String);

                    return path;
                }
                else
                {
                    if (MessageBox.Show(string.Format("You cannot continue without indicating the location of {0}.", name), "Error", MessageBoxButtons.RetryCancel) == DialogResult.Cancel)
                    {
                        break;
                    }
                }
            }
            while (dlgResult != DialogResult.OK);

            return null;
        }

        private Dictionary<string, string> BuildChecksumDictionary(string listFile)
        {
            var dictList = new Dictionary<string, string>();
            WriteLog("Building checksum dictionary using " + listFile);

            foreach (var line in File.ReadLines(listFile))
            {
                var s = line.Split(',');
                if (s.Length > 2)
                {
                    WriteLog("Invalid checksum data found: \n\r" + s);
                    continue;
                }

                WriteLog("\t" + s[0] + " = " + s[1]);
                dictList.Add(s[0], s[1]);
            }

            WriteLog("Done.");
            return dictList;
        }

        private bool CheckFiles()
        {
            string errFileNotFound = "{0} could not be found.";
            bool fileCheck = true;

            WriteLog("Checking for required files.");
            Progress.Report("Checking for required files...");

            foreach (var ESM in CheckedESMs)
            {
                var ttwESM = Path.Combine(dirTTWMain, ESM);
                var dataESM = Path.Combine(dirFO3Data, ESM);
                if (!File.Exists(ttwESM) && !File.Exists(dataESM))
                {
                    var errMsg = string.Format(errFileNotFound, ESM);

                    WriteLog(errMsg);
                    Progress.Report(errMsg);

                    fileCheck = false;
                }
            }

            foreach (var KVP in CheckedBSAs)
            {
                //Key = TTW BSA
                //Value = string[] of FO3 sub-BSAs
                if (!File.Exists(KVP.Key))
                {
                    foreach (var subBSA in KVP.Value)
                    {
                        var pathedSubBSA = Path.Combine(dirFO3Data, subBSA);
                        if (!File.Exists(pathedSubBSA))
                        {
                            var errMsg = string.Format(errFileNotFound, subBSA);

                            WriteLog(errMsg);
                            Progress.Report(errMsg);

                            fileCheck = false;
                        }
                    }
                }
            }

            return fileCheck;
        }
    }
}