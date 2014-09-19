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
using System.Diagnostics;
using BSAsharp;
using TaleOfTwoWastelands.ProgressTypes;
using TaleOfTwoWastelands.Patching;
using Microsoft;
using SevenZip;
using System.Net;

namespace TaleOfTwoWastelands
{
    public class Installer : IDisposable
    {
        #region Set-once fields (statics and constants)
        public const string
            MainDir = "Main Files",
            OptDir = "Optional Files",
            AssetsDir = "resources",
            MainFOMOD = "TaleOfTwoWastelands_Main.fomod",
            OptFOMOD = "TaleOfTwoWastelands_Options.fomod",
            NvseFile = "nvse_loader.exe",
            NvseLink = @"http://nvse.silverlock.org/",
            NvseSearch = @"http://nvse.silverlock.org/download/*.7z";

        public const CompressionStrategy FastStrategy = CompressionStrategy.Unsafe | CompressionStrategy.Speed;
        public const CompressionStrategy GoodStrategy = CompressionStrategy.Unsafe | CompressionStrategy.Size;

        public static readonly string[] CheckedESMs = { "Fallout3.esm", "Anchorage.esm", "ThePitt.esm", "BrokenSteel.esm", "PointLookout.esm", "Zeta.esm" };
        public static readonly Dictionary<string, string> VoicePaths = new Dictionary<string, string> {
            {Path.Combine("sound", "voice", "fallout3.esm", "playervoicemale"), Path.Combine("PlayerVoice", "sound", "voice", "falloutnv.esm", "playervoicemale")},
            {Path.Combine("sound", "voice", "fallout3.esm", "playervoicefemale"), Path.Combine("PlayerVoice", "sound", "voice", "falloutnv.esm", "playervoicefemale")}
        };
        public static readonly Dictionary<string, string[]> CheckedBSAs = new Dictionary<string, string[]> {
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
        public static readonly Dictionary<string, string> BuildableBSAs = new Dictionary<string, string>
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
        public static readonly Dictionary<string, CompressionOptions> BSAOptions = new Dictionary<string, CompressionOptions>
        {
            //example compression options
            //{"Fallout - Sound", new CompressionOptions(FastStrategy)},
            //{"Fallout - MenuVoices", new CompressionOptions(FastStrategy)},
            //{"Fallout - Voices", new CompressionOptions(FastStrategy)},
            //{"Anchorage - Sounds", new CompressionOptions(FastStrategy)},
            //{"ThePitt - Sounds", new CompressionOptions(FastStrategy)},
            //{"BrokenSteel - Sounds", new CompressionOptions(FastStrategy)},
            //{"PointLookout - Sounds", new CompressionOptions(FastStrategy)},
            //{"Zeta - Sounds", new CompressionOptions(FastStrategy)},
        };
        public static readonly CompressionOptions DefaultBSAOptions = new CompressionOptions
        {
            Strategy = GoodStrategy,
            ExtensionCompressionLevel = new Dictionary<string, int>
            {
                //TODO: bsa# is broken and converts these to uppercase, fix asap!
                {".OGG", -1},
                {".WAV", -1},
                {".MP3", -1},
                //{".lip", -1},
            }
        };

        public static readonly string PatchDir = Path.Combine(Installer.AssetsDir, "TTW Data", "TTW Patches");
        #endregion

        #region Instance private
        readonly string dirFO3Data, dirFNVData;

        private string dirTTWMain { get { return Path.Combine(TTWSavePath, MainDir); } }
        private string dirTTWOptional { get { return Path.Combine(TTWSavePath, OptDir); } }

        private CancellationTokenSource LinkedSource { get; set; }
        private CancellationToken Token { get; set; }

        private BSADiff bsaDiff;
        #endregion

        #region Instance public properties
        public string Fallout3Path { get; private set; }
        public string FalloutNVPath { get; private set; }
        public string TTWSavePath { get; private set; }

        /// <summary>
        /// Provides progress messages tailored for user display
        /// </summary>
        public IProgress<string> ProgressLog { get; private set; }
        /// <summary>
        /// Provides progress messages for debugging
        /// </summary>
        public IProgress<string> ProgressFile { get; private set; }
        /// <summary>
        /// Reports progress messages to both user display and debugging
        /// </summary>
        public IProgress<string> ProgressDual { get; set; }
        /// <summary>
        /// Provides progress updates for minor operations
        /// </summary>
        public IProgress<InstallOperation> ProgressMinorOperation { get; private set; }
        /// <summary>
        /// Provides progress updates for major operations
        /// </summary>
        public IProgress<InstallOperation> ProgressMajorOperation { get; private set; }
        #endregion

        public Installer(IProgress<string> progressLog, IProgress<InstallOperation> uiMinor, IProgress<InstallOperation> uiMajor, OpenFileDialog openDialog, SaveFileDialog saveDialog)
        {
            ProgressFile = new Progress<string>(msg => LogFile(msg));

            ProgressLog = progressLog;

            ProgressDual = new Progress<string>(msg => LogDual(msg));

            ProgressMinorOperation = uiMinor;
            ProgressMajorOperation = uiMajor;

            if (Environment.Is64BitOperatingSystem)
                LogFile("\t64-bit architecture found.");
            else
                LogFile("\t32-bit architecture found.");

            //create or retrieve FO3 path
            Fallout3Path = GetPathFromKey("Fallout3");
            dirFO3Data = Path.Combine(Fallout3Path, "Data");

            //create or retrieve FNV path
            FalloutNVPath = GetPathFromKey("FalloutNV");
            dirFNVData = Path.Combine(FalloutNVPath, "Data");

            //create or retrieve TTW path
            TTWSavePath = GetPathFromKey("TaleOfTwoWastelands");

            PromptPaths(openDialog, saveDialog);
        }
        ~Installer()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
        }

        private void LogDisplay(string s)
        {
            ProgressLog.Report(s);
        }
        private void LogFile(string s)
        {
            Trace.WriteLine(string.Format("[{0}]\t{1}", DateTime.Now, s));
        }
        private void LogDual(string s)
        {
            LogDisplay(s);
            LogFile(s);
        }

        public static RegistryKey GetBethKey()
        {
            using (var bethKey =
                Registry.LocalMachine.OpenSubKey(
                //determine software reg path (depends on architecture)
                Environment.Is64BitOperatingSystem ? "Software\\Wow6432Node" : "Software", RegistryKeyPermissionCheck.ReadWriteSubTree))
                //create or retrieve BethSoft path
                return bethKey.CreateSubKey("Bethesda Softworks", RegistryKeyPermissionCheck.ReadWriteSubTree);
        }

        private string GetPathFromKey(string keyName)
        {
            using (var bethKey = GetBethKey())
            using (var subKey = bethKey.CreateSubKey(keyName))
                return subKey.GetValue("Installed Path", "").ToString();
        }

        private void SetPathFromKey(string keyName, string path)
        {
            using (var bethKey = GetBethKey())
            using (var subKey = bethKey.CreateSubKey(keyName))
                subKey.SetValue("Installed Path", path, RegistryValueKind.String);
        }

        public void Fallout3Prompt(FileDialog open, bool manual = false)
        {
            open.FilterIndex = 1;
            open.Title = "Fallout 3";
            Fallout3Path = FindByUserPrompt(open, "Fallout 3", "Fallout3", manual);
        }

        public void FalloutNVPrompt(FileDialog open, bool manual = false)
        {
            open.FilterIndex = 2;
            open.Title = "Fallout New Vegas";
            FalloutNVPath = FindByUserPrompt(open, "Fallout New Vegas", "FalloutNV", manual);
        }

        public void TTWPrompt(FileDialog save, bool manual = false)
        {
            TTWSavePath = FindByUserPrompt(save, "Tale of Two Wastelands", "TaleOfTwoWastelands", manual);
        }

        public void Install(CancellationToken inToken)
        {
            LinkedSource = CancellationTokenSource.CreateLinkedTokenSource(inToken);
            this.Token = LinkedSource.Token;

            bsaDiff = new BSADiff(this, Token);

            var opProg = new InstallOperation(ProgressMajorOperation, Token) { ItemsTotal = 7 + BuildableBSAs.Count + CheckedESMs.Length };
            try
            {
                try
                {
                    opProg.CurrentOperation = "Checking for required files";

                    if (CheckFiles() && CheckNVSE())
                    {
                        LogFile("All files found.");
                        LogDisplay("All files found. Proceeding with installation.");
                    }
                    else
                    {
                        LogFile("Missing files detected. Aborting install.");
                        LogDisplay("The above files were not found. Make sure your Fallout 3 location is accurate and try again.\nInstallation failed.");
                        return;
                    }
                }
                finally
                {
                    //+1
                    opProg.Step();
                }

                try
                {
                    var curOp = "Creating FOMOD foundation";
                    opProg.CurrentOperation = curOp;

                    LogFile(curOp);
                    Util.CopyFolder(Path.Combine(AssetsDir, "TTW Data", "TTW Files"), TTWSavePath, ProgressFile);
                }
                finally
                {
                    //+1
                    opProg.Step();
                }

                //count BuildableBSAs
                BuildBSAs(opProg);

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
                    var ttwArchive = "TaleOfTwoWastelands.bsa";
                    opProg.CurrentOperation = "Copying " + ttwArchive;

                    if (!File.Exists(Path.Combine(dirTTWMain, ttwArchive)))
                        File.Copy(Path.Combine(AssetsDir, "TTW Data", ttwArchive), Path.Combine(dirTTWMain, ttwArchive));
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
                    FalloutLineCopy(opA, Path.Combine(AssetsDir, "TTW Data", "FO3_MusicCopy.txt"));
                    opProg.Step();

                    opProg.CurrentOperation = prefix + opB;
                    FalloutLineCopy(opB, Path.Combine(AssetsDir, "TTW Data", "FO3_VideoCopy.txt"));
                    opProg.Step();
                }

                if (MessageBox.Show("Tale of Two Wastelands is easiest to install via a mod manager (such as Nexus Mod Manager). Manual installation is possible but not suggested.\n\nWould like the installer to automatically build FOMODs?", "Build FOMODs?", MessageBoxButtons.YesNo) == DialogResult.Yes)
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

                LogDisplay("Install completed successfully.");
                MessageBox.Show("Tale of Two Wastelands has been installed successfully.");
            }
            catch (OperationCanceledException)
            {
                //intentionally cancelled - swallow exception
                LogFile("Install was cancelled.");
            }
            catch (Exception ex)
            {
                LogFile(ex.Message);
                LogDisplay(ex.Message);
                MessageBox.Show("An error occurred while installing:\n" + ex.Message, "Exception");
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

        private void BuildBSAs(InstallOperation opProg)
        {
            foreach (var KVP in BuildableBSAs)
            {
                //inBSA - KVP.Key
                //outBSA - KVP.Value

                DialogResult buildResult;
                try
                {
                    opProg.CurrentOperation = "Building " + Path.GetFileName(KVP.Value);
                    do
                    {
                        CompressionOptions bsaOptions = null;
                        if (BSAOptions.TryGetValue(KVP.Key, out bsaOptions))
                        {
                            if (bsaOptions.ExtensionCompressionLevel.Count == 0)
                                bsaOptions.ExtensionCompressionLevel = DefaultBSAOptions.ExtensionCompressionLevel;
                            if (bsaOptions.Strategy == CompressionStrategy.Safe)
                                bsaOptions.Strategy = DefaultBSAOptions.Strategy;
                        }
                        else
                        {
                            bsaOptions = DefaultBSAOptions;
                        }

                        buildResult = BuildBSA(bsaOptions, KVP.Key, KVP.Value);
                    } while (!Token.IsCancellationRequested && buildResult == DialogResult.Retry);
                }
                finally
                {
                    opProg.Step();
                }

                if (buildResult == DialogResult.Abort)
                    LinkedSource.Cancel();

                if (Token.IsCancellationRequested)
                    return;
            }
        }

        private void BuildSFX()
        {
            var fo3BsaPath = Path.Combine(dirFO3Data, "Fallout - Sound.bsa");

            var songsPath = Path.Combine("sound", "songs");

            bool skipSongs = false, skipSFX = false;
            if (Directory.Exists(Path.Combine(dirTTWMain, songsPath)))
                skipSongs = ShowSkipDialog("Fallout 3 songs");

            var outBsaPath = Path.Combine(dirTTWOptional, "Fallout3 Sound Effects", "TaleOfTwoWastelands - SFX.bsa");
            if (File.Exists(outBsaPath))
                skipSFX = ShowSkipDialog("Fallout 3 sound effects");

            if (skipSongs && skipSFX)
                return;

            using (BSA
               inBsa = new BSA(fo3BsaPath),
               outBsa = new BSA(inBsa.Settings))
            {
                if (!skipSongs)
                {
                    LogDisplay("Extracting songs");
                    ExtractBSA(ProgressFile, Token, inBsa.Where(folder => folder.Path.StartsWith(songsPath)), dirTTWMain, skipSongs, "Fallout - Sound");
                }

                if (!skipSFX)
                {
                    LogDisplay("Building optional TaleOfTwoWastelands - SFX.bsa...");

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

                    LogFile("Building TaleOfTwoWastelands - SFX.bsa.");
                    outBsa.Save(outBsaPath);

                    LogDisplay("\tDone");
                }
            }
        }

        private void BuildVoice()
        {
            var outBsaPath = Path.Combine(dirTTWOptional, "Fallout3 Player Voice", "TaleOfTwoWastelands - PlayerVoice.bsa");
            if (File.Exists(outBsaPath))
                return;

            var inBsaPath = Path.Combine(dirFO3Data, "Fallout - Voices.bsa");

            using (BSA
                inBsa = new BSA(inBsaPath),
                outBsa = new BSA(inBsa.Settings))
            {
                var includedFolders = inBsa
                    .Where(folder => VoicePaths.ContainsKey(folder.Path))
                    .Select(folder => new BSAFolder(VoicePaths[folder.Path], folder));

                foreach (var folder in includedFolders)
                    outBsa.Add(folder);

                outBsa.Save(outBsaPath);
            }
        }

        private bool PatchMasters(InstallOperation opProg)
        {
            foreach (var ESM in CheckedESMs)
                try
                {
                    opProg.CurrentOperation = "Patching " + ESM;

                    if (Token.IsCancellationRequested || !PatchMaster(ESM))
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
            LogFile("Building FOMODs.");
            LogDisplay("Building FOMODs...");
            LogDisplay("This can take some time.");
            BuildFOMOD(dirTTWMain, Path.Combine(TTWSavePath, MainFOMOD));
            BuildFOMOD(dirTTWOptional, Path.Combine(TTWSavePath, OptFOMOD));
            LogFile("\tDone.");
            LogDisplay("\tFOMODs built.");
        }

        private void BuildFOMOD(string path, string fomod)
        {
            var opFomod = new InstallOperation(ProgressMinorOperation, Token);

            var compressor = new SevenZipCompressor()
            {
                ArchiveFormat = OutArchiveFormat.SevenZip,
                CompressionLevel = CompressionLevel.Fast,
                CompressionMethod = CompressionMethod.Lzma2,
                CompressionMode = CompressionMode.Create,
            };
            compressor.CustomParameters.Add("mt", "on"); //enable multithreading

            compressor.FilesFound += (sender, e) => opFomod.ItemsTotal = e.Value;
            compressor.Compressing += (sender, e) => e.Cancel = Token.IsCancellationRequested;
            compressor.CompressionFinished += (sender, e) => opFomod.Finish();
            compressor.FileCompressionStarted += (sender, e) => opFomod.CurrentOperation = "Compressing " + e.FileName;
            compressor.FileCompressionFinished += (sender, e) => opFomod.Step();

            compressor.CompressDirectory(path, fomod, true);
        }

        private void FalloutLineCopy(string name, string path)
        {
            bool skipExisting = false, asked = false;

            LogDual("Copying " + name + "...");
            foreach (var line in File.ReadLines(path))
            {
                var ttwLinePath = Path.Combine(dirTTWMain, line);
                var foLinePath = Path.Combine(dirFO3Data, line);

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
                        LogFile("\tERROR: " + line + " did not copy successfully due to: Unauthorized Access Exception " + error.Source + ".");
                    }
                }
                else
                    LogFile("\tFile not found:\t" + foLinePath);
            }
            LogDual("\tDone.");
        }

        private static bool CheckExisting(string path, FileValidation newChk)
        {
            using (var existingChk = new FileValidation(path, newChk.Type))
                return newChk == existingChk;
        }

        private bool PatchMaster(string ESM)
        {
            LogDual("Patching " + ESM + "...");

            var patchPath = Path.Combine(PatchDir, Path.ChangeExtension(ESM, ".pat"));
            if (File.Exists(patchPath))
            {
                var patchDict = new PatchDict(patchPath);

                Debug.Assert(patchDict.ContainsKey(ESM));
                var patch = patchDict[ESM];
                var patches = patch.Item2;
                var newChk = patch.Item1;

                var finalPath = Path.Combine(dirTTWMain, ESM);
                bool finalExists;
                if ((finalExists = File.Exists(finalPath)))
                {
                    LogDual("\t" + ESM + " already exists");
                    if (CheckExisting(finalPath, newChk))
                    {
                        LogDual("\t" + ESM + " is up to date");
                        return true;
                    }
                    else
                    {
                        LogDual("\t" + ESM + " is out of date");
                    }
                }

                var dataPath = Path.Combine(dirFO3Data, ESM);
                //TODO: change to a user-friendly condition and message
                Trace.Assert(File.Exists(dataPath));

                Debug.Assert(patches.All(p => p.Metadata.Type == FileValidation.ChecksumType.Murmur128));
                using (var dataChk = new FileValidation(dataPath))
                {
                    var matchPatch = patches.SingleOrDefault(p => p.Metadata == dataChk);
                    if (matchPatch == null)
                    {
                        LogDisplay("\tA patch for your version of " + ESM + " could not be found");
                        LogFile("\tA patch for " + ESM + " version " + dataChk + " could not be found");
                    }
                    else
                    {
                        byte[]
                            dataBytes = File.ReadAllBytes(dataPath),
                            outputBytes;

                        FileValidation outputChk;

                        if (matchPatch.PatchBytes(dataBytes, newChk, out outputBytes, out outputChk))
                        {
                            File.WriteAllBytes(finalPath, outputBytes);
                            LogDual("\tPatch successful");
                            return true;
                        }
                        else
                        {
                            LogFile("\tPatch failed");
                        }
                    }
                }
            }
            else
                LogDual("\t" + ESM + " patch is missing from " + PatchDir);

            Fail("Your version of " + ESM + " cannot be patched. This is abnormal.");

            return false;
        }

        private DialogResult BuildBSA(CompressionOptions bsaOptions, string inBSA, string outBSA)
        {
            string outBSAFile = Path.ChangeExtension(outBSA, ".bsa");
            string outBSAPath = Path.Combine(dirTTWMain, outBSAFile);

            if (File.Exists(outBSAPath))
            {
                switch (MessageBox.Show(outBSAFile + " already exists. Rebuild?", "File Already Exists", MessageBoxButtons.YesNo))
                {
                    case System.Windows.Forms.DialogResult.Yes:
                        File.Delete(outBSAPath);
                        LogDual("Rebuilding " + outBSA);
                        break;
                    case System.Windows.Forms.DialogResult.No:
                        LogDual(outBSA + " has already been built. Skipping.");
                        return DialogResult.No;
                }
            }
            else
            {
                LogDual("Building " + outBSA);
            }

            string inBSAFile = Path.ChangeExtension(inBSA, ".bsa");
            string inBSAPath = Path.Combine(dirFO3Data, inBSAFile);

            bool patchSuccess;

#if DEBUG
            var watch = new Stopwatch();
            try
            {
                watch.Start();
#endif
                patchSuccess = bsaDiff.PatchBSA(bsaOptions, inBSAPath, outBSAPath);
                if (!patchSuccess)
                    ProgressDual.Report(string.Format("Patching BSA {0} failed", inBSA));
#if DEBUG
            }
            finally
            {
                watch.Stop();
                Debug.WriteLine("PatchBSA for {0} finished in {1}", inBSA, watch.Elapsed);
            }
#endif

            if (!patchSuccess)
            {
                switch (MessageBox.Show("Errors occurred while patching " + inBSA, "Error Warning", MessageBoxButtons.AbortRetryIgnore))
                {
                    case System.Windows.Forms.DialogResult.Abort:   //Quit install
                        Fail();
                        return System.Windows.Forms.DialogResult.Abort;
                    case System.Windows.Forms.DialogResult.Retry:   //Start over from scratch
                        LogDual("Retrying build.");
                        return System.Windows.Forms.DialogResult.Retry;
                    case System.Windows.Forms.DialogResult.Ignore:  //Ignore errors and move on
                        LogDual("Ignoring errors.");
                        return System.Windows.Forms.DialogResult.Ignore;
                }
            }

            LogDual("Build successful.");
            return System.Windows.Forms.DialogResult.OK;
        }

        private bool CheckNVSE()
        {
            var nvseLoader = Path.Combine(FalloutNVPath, NvseFile);
            if (!File.Exists(nvseLoader))
            {
                LogFile("NVSE missing");

                var dlgResult = MessageBox.Show(@"New Vegas Script Extender (NVSE) was not found, but is required to play A Tale of Two Wastelands.

Would you like to install NVSE?", "NVSE missing", MessageBoxButtons.YesNoCancel);

                switch (dlgResult)
                {
                    case DialogResult.Yes:
                        return InstallNVSE(FalloutNVPath);
                    case DialogResult.No:
                        LogDual("Proceeding without NVSE.");
                        LogDisplay("NVSE must be installed before playing!");
                        return true;
                    case DialogResult.Cancel:
                        LogFile("Install cancelled due to NVSE requirement");
                        Fail();
                        return false;
                }
            }
            else LogFile("NVSE found");

            return true;
        }

        //where's my async?
        private bool InstallNVSE(string dlPath)
        {
            using (var wc = new WebClient())
            {
                LogFile("Requesting NVSE page at " + NvseLink);

                string dlLink;
                using (var resStream = wc.OpenRead(NvseLink))
                {
                    if (!Util.PatternSearch(resStream, NvseSearch, out dlLink))
                    {
                        Fail("Failed to download NVSE.");
                        return false;
                    }
                }

                LogFile("Parsed NVSE link: " + dlLink.Truncate(100));

                var archiveName = Path.GetFileName(dlLink);
                var tmpPath = Path.Combine(Path.GetTempPath(), archiveName);
                wc.DownloadFile(dlLink, tmpPath);

                using (var lzExtract = new SevenZipExtractor(tmpPath))
                {
                    if (!lzExtract.Check())
                    {
                        Fail(archiveName + " is an invalid 7z archive.");
                        return false;
                    }

                    var wantedFiles = (from file in lzExtract.ArchiveFileNames
                                       let filename = Path.GetFileName(file)
                                       let ext = Path.GetExtension(filename).ToUpperInvariant()
                                       where ext == ".EXE" || ext == ".DLL"
                                       select new { file, filename }).ToArray();

                    foreach (var a in wantedFiles)
                    {
                        var savePath = Path.Combine(dlPath, a.filename);
                        LogFile("Extracting " + a.filename);

                        using (var fsStream = File.OpenWrite(savePath))
                        {
                            try
                            {
                                lzExtract.ExtractFile(a.file, fsStream);
                            }
                            catch
                            {
                                Fail("Failed to extract NVSE.");
                                throw;
                            }
                        }
                    }
                }
            }

            LogDual("NVSE was installed successfully.");
            return true;
        }

        private void Fail(string msg = null)
        {
            if (msg != null)
                LogDual(msg);
            LogDual("Install aborted.");
        }

        private void PromptPaths(FileDialog open, FileDialog save)
        {
            SevenZipCompressor.SetLibraryPath(Path.Combine(AssetsDir, "7Zip", "7z" + (Environment.Is64BitProcess ? "64.dll" : ".dll")));

            LogFile("Looking for Fallout3.exe");
            if (File.Exists(Path.Combine(Fallout3Path, "Fallout3.exe")))
            {
                LogFile("\tFound.");
            }
            else
            {
                Fallout3Prompt(open);
            }

            LogFile("Looking for FalloutNV.exe");
            if (File.Exists(Path.Combine(FalloutNVPath, "FalloutNV.exe")))
            {
                LogFile("\tFound.");
            }
            else
            {
                FalloutNVPrompt(open);
            }

            LogFile("Looking for Tale of Two Wastelands");
            if (TTWSavePath != null && TTWSavePath != "\\")
            {
                LogFile("\tDefault path found.");
            }
            else
            {
                TTWPrompt(save);
            }
        }

        private string FindByUserPrompt(FileDialog dlg, string name, string keyName, bool manual = false)
        {
            LogFile(string.Format("\t{0} not found, prompting user.", name));
            MessageBox.Show(string.Format("Could not automatically find {0}'s location, please manually indicate its location.", name));

            DialogResult dlgResult;
            do
            {
                dlgResult = dlg.ShowDialog();
                if (dlgResult == DialogResult.OK)
                {
                    var path = Path.GetDirectoryName(dlg.FileName);
                    if (manual)
                        LogFile(string.Format("User manually changed {0} directory to: {1}", name, path));
                    else
                        LogFile("User selected: " + path);

                    SetPathFromKey(keyName, path);

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

        public static void ExtractBSA(IProgress<string> progress, CancellationToken token, IEnumerable<BSAFolder> folders, string bsaOutputDir, bool skipExisting, string bsaName = null)
        {
            foreach (var folder in folders)
            {
                Directory.CreateDirectory(Path.Combine(bsaOutputDir, folder.Path));
                progress.Report("Created " + folder.Path);

                foreach (var file in folder)
                {
                    token.ThrowIfCancellationRequested();

                    var filePath = Path.Combine(bsaOutputDir, file.Filename);
                    if (File.Exists(filePath) && skipExisting)
                    {
                        progress.Report("Skipped (already exists) " + file.Filename);
                        continue;
                    }

                    File.WriteAllBytes(filePath, file.GetContents(true));
                    progress.Report("Extracted " + file.Filename);
                }
            }
            progress.Report("Extract from " + bsaName ?? bsaOutputDir.Replace(Path.GetDirectoryName(bsaOutputDir), "").TrimEnd(Path.DirectorySeparatorChar) + " done!");
        }

        private bool CheckFiles()
        {
            string errFileNotFound = "{0} could not be found.";
            bool fileCheck = true;

            LogDual("Checking for required files...");

            foreach (var ESM in CheckedESMs)
            {
                var ttwESM = Path.Combine(dirTTWMain, ESM);
                var dataESM = Path.Combine(dirFO3Data, ESM);
                if (!File.Exists(ttwESM) && !File.Exists(dataESM))
                {
                    var errMsg = string.Format(errFileNotFound, ESM);

                    LogDual(errMsg);

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

                            LogDual(errMsg);

                            fileCheck = false;
                        }
                    }
                }
            }

            return fileCheck;
        }
    }
}
