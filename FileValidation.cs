using BSAsharp;
using ICSharpCode.SharpZipLib.Checksums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace TaleOfTwoWastelands
{
    [Serializable]
    public class FileValidation
    {
        const int WINDOW = 0x1000;

        public uint InflatedFilesize { get; private set; }

        [NonSerialized]
        private Lazy<IEnumerable<long>> _inflatedChecksums;

        private long[] _writtenInflatedChecksums;
        public IEnumerable<long> InflatedChecksums
        {
            get
            {
                return _writtenInflatedChecksums ?? (_inflatedChecksums != null ? _inflatedChecksums.Value : null);
            }
            set
            {
                _writtenInflatedChecksums = value.ToArray();
            }
        }

        public uint DeflatedFilesize { get; private set; }

        [NonSerialized]
        private Lazy<IEnumerable<long>> _deflatedChecksums;

        private long[] _writtenDeflatedChecksums;
        public IEnumerable<long> DeflatedChecksums
        {
            get
            {
                return _writtenDeflatedChecksums ?? (_deflatedChecksums != null ? _deflatedChecksums.Value : null);
            }
            set
            {
                _writtenDeflatedChecksums = value.ToArray();
            }
        }

        private FileValidation()
        {
        }

        public static Dictionary<string, FileValidation> FromBSA(BSAWrapper BSA)
        {
            return BSA
                .SelectMany(folder => folder)
                .Select(file => new { file.Filename, val = FromBSAFile(file) })
                .ToDictionary(a => a.Filename, a => a.val);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            return Equals(obj as FileValidation);
        }

        public bool Equals(FileValidation obj)
        {
            if (obj == null)
                return false;

            if (DeflatedChecksums != null && obj.DeflatedChecksums != null)
            {
                if (DeflatedFilesize == obj.DeflatedFilesize)
                {
                    return DeflatedChecksums.SequenceEqual(obj.DeflatedChecksums);
                }
            }
            else if (InflatedChecksums != null && obj.InflatedChecksums != null)
            {
                if (InflatedFilesize == obj.InflatedFilesize)
                {
                    return InflatedChecksums.SequenceEqual(obj.InflatedChecksums);
                }
            }
            return false;
        }

        public static FileValidation FromBSAFile(BSAFile file)
        {
            var val = new FileValidation();

            if (file.Name.Contains("barkeep"))
                file.GetSaveData(true);

            if (file.IsCompressed)
            {
                val._deflatedChecksums = new Lazy<IEnumerable<long>>(() =>
                {
                    var gysd = file.GetYieldingSaveData(false);
                    var gsd = file.GetSaveData(false);
                    Trace.Assert(gysd.SequenceEqual(gsd));

                    return IncrementalChecksum(gysd, file.Size);
                });
                val.DeflatedFilesize = file.Size;
            }

            val._inflatedChecksums = new Lazy<IEnumerable<long>>(() => IncrementalChecksum(file.GetYieldingSaveData(true), file.OriginalSize));
            val.InflatedFilesize = file.OriginalSize;

            return val;
        }

        private static IEnumerable<long> IncrementalChecksum(IEnumerable<byte> data, uint size)
        {
            Trace.Assert(size > 0);

            IChecksum chk;
            if (size < WINDOW)
                chk = new Crc32();
            else
                chk = new Adler32();

            var windows = (size + WINDOW - 1) / WINDOW;
            var dataMover = data.GetEnumerator();
            for (int i = 0; i < windows; i++)
            {
                //chk.Update(data, i * WINDOW, Math.Min(i * WINDOW + WINDOW, (int)size) - (i * WINDOW));
                byte[] buf = new byte[WINDOW];
                for (int j = 0; j < WINDOW; j++)
                {
                    buf[j] = dataMover.Current;
                    if (!dataMover.MoveNext())
                        break;
                }
                chk.Update(buf);

                yield return chk.Value;
            }
            dataMover.Dispose();
        }
    }
}