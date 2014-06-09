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
            throw new NotImplementedException();
        }

        public static void BuildBSA(IProgress<string> progress, CancellationToken token, string bsaPath, string bsaOutputDir)
        {
            throw new NotImplementedException();
        }

        private static IEnumerable<BSAFolder> GetLayout(string bsaPath)
        {
            using (var bsa = new BSAReader(File.OpenRead(bsaPath)))
            {
                return bsa.Read();
            }
        }
    }
}
