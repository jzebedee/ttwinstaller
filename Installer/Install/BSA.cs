using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using BSAsharp;
using TaleOfTwoWastelands.Patching;
using TaleOfTwoWastelands.Properties;

namespace TaleOfTwoWastelands.Install
{
    public static class BSA
    {
        public enum BuildResult
        {
            Continue,
            Retry,
            Abort
        }

        const CompressionStrategy
            FastStrategy = CompressionStrategy.Unsafe | CompressionStrategy.Speed,
            GoodStrategy = CompressionStrategy.Unsafe | CompressionStrategy.Size;

        static readonly Dictionary<string, CompressionOptions> BSAOptions = new Dictionary<string, CompressionOptions>
        {
            //{"example",new CompressionOptions()}
        };
        static readonly CompressionOptions DefaultBSAOptions = new CompressionOptions
        {
            Strategy = GoodStrategy,
            ExtensionCompressionLevel = new Dictionary<string, int>
            {
                {".ogg", -1},
                {".wav", -1},
                {".mp3", -1},
                {".lip", -1},
            }
        };

        private static bool OverwritePrompt(string bsaName, string bsaPath)
        {
            if (!File.Exists(bsaPath))
                return true;

            Log.File("BSA \"{0}\" does not exist", bsaPath);

            var promptResult = MessageBox.Show(String.Format(Resources.RebuildPrompt, bsaName), Resources.FileAlreadyExists, MessageBoxButtons.YesNo);
            switch (promptResult)
            {
                case DialogResult.Yes:
                    File.Delete(bsaPath);
                    Log.File("Rebuilding {0}", bsaName);
                    return true;
                case DialogResult.No:
                    Log.Dual("{0} has already been built. Skipping.", bsaName);
                    break;
            }

            return false;
        }

        public static bool BuildPrompt(string bsaName, string bsaPath)
        {
            if (!OverwritePrompt(bsaName, bsaPath))
                return false;

            Log.Dual("Building {0}", bsaName);
            return true;
        }

        private static BuildResult ErrorPrompt(string bsaFile)
        {
            var promptResult = MessageBox.Show(String.Format(Resources.ErrorWhilePatching, bsaFile), Resources.Error, MessageBoxButtons.AbortRetryIgnore);
            switch (promptResult)
            {
                //case DialogResult.Abort: //Quit install
                case DialogResult.Retry:   //Start over from scratch
                    Log.Dual("Retrying build.");
                    return BuildResult.Retry;
                case DialogResult.Ignore:  //Ignore errors and move on
                    Log.Dual("Ignoring errors.");
                    return BuildResult.Continue;
            }

            return BuildResult.Abort;
        }

        public static CompressionOptions GetOptionsOrDefault(string inBsa)
        {
            CompressionOptions bsaOptions;
            if (BSAOptions.TryGetValue(inBsa, out bsaOptions))
            {
                if (bsaOptions.ExtensionCompressionLevel.Count == 0)
                    bsaOptions.ExtensionCompressionLevel = DefaultBSAOptions.ExtensionCompressionLevel;
                if (bsaOptions.Strategy == CompressionStrategy.Safe)
                    bsaOptions.Strategy = DefaultBSAOptions.Strategy;
            }

            return bsaOptions ?? DefaultBSAOptions;
        }

        public static BuildResult Patch(BSADiff diff, CompressionOptions options, string inBsaFile, string inBsaPath, string outBsaPath)
        {
            bool patchSuccess;

#if DEBUG
            var watch = new Stopwatch();
            try
            {
                watch.Start();
#endif
                patchSuccess = diff.PatchBSA(options, inBsaPath, outBsaPath);
                if (!patchSuccess)
                    Log.Dual("Patching BSA {0} failed", inBsaFile);
#if DEBUG
            }
            finally
            {
                watch.Stop();
                Debug.WriteLine("PatchBSA for {0} finished in {1}", inBsaFile, watch.Elapsed);
            }
#endif

            if (patchSuccess)
            {
                Log.Dual("Build successful.");
                return BuildResult.Continue;
            }

            return ErrorPrompt(inBsaFile);
        }

        public static void Extract(CancellationToken token, IEnumerable<BSAFolder> folders, string outBsa, string outBsaPath, bool skipExisting)
        {
            foreach (var folder in folders)
            {
                Directory.CreateDirectory(Path.Combine(outBsaPath, folder.Path));
                Log.File("Created " + folder.Path);

                foreach (var file in folder)
                {
                    token.ThrowIfCancellationRequested();

                    var filePath = Path.Combine(outBsaPath, file.Filename);
                    if (File.Exists(filePath) && skipExisting)
                    {
                        Log.File("Skipped (already exists) " + file.Filename);
                        continue;
                    }

                    File.WriteAllBytes(filePath, file.GetContents(true));
                    Log.File("Extracted " + file.Filename);
                }
            }

            Log.File("Extract from {0} done!", outBsa);
        }
    }
}
