#define MMAP
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
        public static void ExtractBSA(IProgress<string> progress, CancellationToken token, string bsaPath, string bsaOutputDir)
        {
#if MMAP
            var bsaInfo = new FileInfo(bsaPath);
            using (var bsa = new MemoryMappedBSAReader(bsaPath, bsaInfo.Length))
#else
            using (var bsa = new BSAReader(File.OpenRead(bsaPath)))
#endif
            {
                var layout = bsa.Read();
                foreach (var folder in layout)
                {
                    Directory.CreateDirectory(Path.Combine(bsaOutputDir, folder.Path));
                    progress.Report("Created " + folder.Path);

                    foreach (var file in folder.Children)
                    {
                        token.ThrowIfCancellationRequested();

                        var filePath = Path.Combine(bsaOutputDir, file.Filename);
                        File.WriteAllBytes(filePath, file.Data);

                        progress.Report("Extracted " + file.Filename);
                    }
                }
                progress.Report("ExtractBSA " + Path.GetFileNameWithoutExtension(bsaPath) + " done!");
            }
        }

        public static void BuildBSA(IProgress<string> progress, CancellationToken token, string bsaPath, string bsaOutputDir)
        {
            throw new NotImplementedException();
        }
    }
}
