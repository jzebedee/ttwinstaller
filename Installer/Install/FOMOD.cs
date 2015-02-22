using System;
using System.IO;
using SevenZip;
using TaleOfTwoWastelands.Progress;
using TaleOfTwoWastelands.Properties;

namespace TaleOfTwoWastelands.Install
{
    internal class FOMOD
    {
        private readonly ILog Log;

        internal FOMOD(ILog log)
        {
            Log = log;
            SevenZipCompressor.SetLibraryPath(Path.Combine(Resources.AssetsDir, "7Zip", "7z" + (Environment.Is64BitProcess ? "64.dll" : ".dll")));
        }

        public void BuildAll(InstallStatus status, string mainBuildFolder, string optBuildFolder, string saveFolder)
        {
            Log.File("Building FOMODs.");
            Log.Display("Building FOMODs...");
            Log.Display("This can take some time.");
            Build(status, mainBuildFolder, Path.Combine(saveFolder, Resources.MainFOMOD));
            Build(status, optBuildFolder, Path.Combine(saveFolder, Resources.OptFOMOD));
            Log.File("\tDone.");
            Log.Display("\tFOMODs built.");
        }

        public static void Build(InstallStatus status, string path, string fomod)
        {
            var compressor = new SevenZipCompressor
            {
                ArchiveFormat = OutArchiveFormat.SevenZip,
                CompressionLevel = CompressionLevel.Fast,
                CompressionMethod = CompressionMethod.Lzma2,
                CompressionMode = CompressionMode.Create,
            };
            compressor.CustomParameters.Add("mt", "on"); //enable multithreading

            compressor.FilesFound += (sender, e) => status.ItemsTotal = e.Value;
            compressor.Compressing += (sender, e) => e.Cancel = status.Token.IsCancellationRequested;
            compressor.CompressionFinished += (sender, e) => status.Finish();
            compressor.FileCompressionStarted += (sender, e) => status.CurrentOperation = "Compressing " + e.FileName;
            compressor.FileCompressionFinished += (sender, e) => status.Step();

            compressor.CompressDirectory(path, fomod, true);
        }
    }
}
