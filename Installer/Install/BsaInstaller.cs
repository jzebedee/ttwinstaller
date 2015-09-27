using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using BSAsharp;
using TaleOfTwoWastelands.Patching;
using TaleOfTwoWastelands.UI;

namespace TaleOfTwoWastelands.Install
{
	public class BsaInstaller
    {
        const CompressionStrategy
            FastStrategy = CompressionStrategy.Unsafe | CompressionStrategy.Speed,
            GoodStrategy = CompressionStrategy.Unsafe | CompressionStrategy.Size;

        static readonly Dictionary<string, CompressionOptions> BSAOptions = new Dictionary<string, CompressionOptions>();
        static readonly CompressionOptions DefaultBSAOptions = new CompressionOptions
        {
            Strategy = GoodStrategy,
            ExtensionCompressionLevel = new Dictionary<string, int>
            {
                {".ogg", -1},
                {".wav", -1},
                {".mp3", -1},
                {".lip", -1}
            }
        };

        private readonly ILog Log;
		private readonly IPrompts Prompts;
		private readonly IBsaDiff _bsaDiff;

        public BsaInstaller(ILog log, IPrompts prompts, IBsaDiff bsaDiff)
        {
            Log = log;
			Prompts = prompts;
			_bsaDiff = bsaDiff;
        }

        public CompressionOptions GetOptionsOrDefault(string inBsa)
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

        public ErrorPromptResult Patch(CompressionOptions options, string inBsaFile, string inBsaPath, string outBsaPath)
        {
            bool patchSuccess;

#if DEBUG
            var watch = new Stopwatch();
            try
            {
                watch.Start();
#endif
                patchSuccess = _bsaDiff.PatchBsa(options, inBsaPath, outBsaPath);
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
                return ErrorPromptResult.Continue;
            }

            return Prompts.PatchingErrorPrompt(inBsaFile);
        }

        public void Extract(CancellationToken token, IEnumerable<BSAFolder> folders, string outBsa, string outBsaPath, bool skipExisting)
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
