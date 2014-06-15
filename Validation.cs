using BSAsharp;
using ICSharpCode.SharpZipLib.Checksums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TaleOfTwoWastelands
{
    public class Validation
    {
        const int WINDOW = 0x1000;

        public uint InflatedFilesize { get; set; }
        public long[] InflatedChecksums { get; set; }

        public uint DeflatedFilesize { get; set; }
        public long[] DeflatedChecksums { get; set; }

        public static Dictionary<string, Validation> FromBSA(string bsaPath)
        {
            using (var BSA = new BSAWrapper(bsaPath))
                return BSA
                    .SelectMany(folder => folder)
                    .Select(file => new { file.Filename, val = FromBSAFile(file) })
                    .ToDictionary(a => a.Filename, a => a.val);
        }

        private static Validation FromBSAFile(BSAFile file)
        {
            var patch = new Validation();
            patch.InflatedFilesize = file.OriginalSize;
            patch.InflatedChecksums = IncrementalChecksum(file.GetSaveData(true));
            if (file.IsCompressed)
            {
                patch.DeflatedFilesize = file.Size;
                patch.DeflatedChecksums = IncrementalChecksum(file.GetSaveData(false));
            }

            return patch;
        }

        private static long[] IncrementalChecksum(byte[] data)
        {
            IChecksum chk;
            if (data.Length < WINDOW)
                chk = new Crc32();
            else
                chk = new Adler32();

            var windows = (data.Length + WINDOW - 1) / WINDOW;

            var payload = new long[windows];
            for (int i = 0; i < windows; i++)
            {
                chk.Update(data, i * WINDOW, Math.Min(i * WINDOW + WINDOW, data.Length) - (i * WINDOW));
                payload[i] = chk.Value;
            }

            return payload;
        }
    }
}