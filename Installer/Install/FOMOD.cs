using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SevenZip;
using TaleOfTwoWastelands.ProgressTypes;

namespace TaleOfTwoWastelands.Install
{
    internal static class FOMOD
    {
        static FOMOD()
        {
            SevenZipCompressor.SetLibraryPath(Path.Combine(Installer.AssetsDir, "7Zip", "7z" + (Environment.Is64BitProcess ? "64.dll" : ".dll")));
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
