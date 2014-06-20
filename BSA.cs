using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using BSAsharp;
using System.IO;

namespace TaleOfTwoWastelands
{
    public static class BSA
    {
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
    }
}