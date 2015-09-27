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

        public FOMOD(ILog log)
        {
            Log = log;
            SevenZipCompressor.SetLibraryPath(Path.Combine(Paths.AssetsDir, Paths.SevenZipBinaries, Environment.Is64BitProcess ? Paths.SevenZipX64 : Paths.SevenZipX32));
        }

        public void BuildAll(InstallStatus status, string mainBuildFolder, string optBuildFolder, string saveFolder)
        {
            Log.Dual("Building FOMODs...");
            Log.Display("This can take some time.");
            Build(status, mainBuildFolder, Path.Combine(saveFolder, Paths.MainFOMOD));
            Build(status, optBuildFolder, Path.Combine(saveFolder, Paths.OptFOMOD));
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
                CompressionMode = CompressionMode.Create
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
