using BSAsharp;
using ICSharpCode.SharpZipLib.Checksums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace TaleOfTwoWastelands.Patching
{
    [Serializable]
    public class FileValidation : ISerializable
    {
        const int WINDOW = 0x1000;

        public uint InflatedFilesize { get; private set; }

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
                _writtenInflatedChecksums = value != null ? value.ToArray() : null;
            }
        }

        public uint DeflatedFilesize { get; private set; }

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
                _writtenDeflatedChecksums = value != null ? value.ToArray() : null;
            }
        }

        public FileValidation(SerializationInfo info, StreamingContext context)
        {
            InflatedChecksums = DeserializeInfo<IEnumerable<long>>(info, "InflatedChecksums");
            InflatedFilesize = DeserializeInfo<uint>(info, "InflatedFilesize");
            DeflatedChecksums = DeserializeInfo<IEnumerable<long>>(info, "DeflatedChecksums");
            DeflatedFilesize = DeserializeInfo<uint>(info, "DeflatedFilesize");
        }
        private FileValidation()
        {
        }

        private static T DeserializeInfo<T>(SerializationInfo info, string name)
        {
            return (T)info.GetValue(name, typeof(T));
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("InflatedChecksums", InflatedChecksums.ToArray());
            info.AddValue("InflatedFilesize", InflatedFilesize);
            info.AddValue("DeflatedChecksums", DeflatedChecksums != null ? DeflatedChecksums.ToArray() : null);
            info.AddValue("DeflatedFilesize", DeflatedFilesize);
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

            if (file.IsCompressed)
            {
                val._deflatedChecksums = new Lazy<IEnumerable<long>>(() => IncrementalChecksum(file.GetYieldingSaveData(false), file.Size));
                val.DeflatedFilesize = file.Size;
            }

            val._inflatedChecksums = new Lazy<IEnumerable<long>>(() => IncrementalChecksum(file.GetYieldingSaveData(true), file.OriginalSize));
            val.InflatedFilesize = file.OriginalSize;

            return val;
        }

        public static FileValidation FromFile(string path)
        {
            var val = new FileValidation();

            var fBytes = File.ReadAllBytes(path);
            val._inflatedChecksums = new Lazy<IEnumerable<long>>(() => IncrementalChecksum(fBytes, (uint)fBytes.Length));
            val.InflatedFilesize = (uint)fBytes.Length;

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
            dataMover.MoveNext();

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
