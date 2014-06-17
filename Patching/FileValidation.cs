//#define INCLUDES_DEFLATED
using BSAsharp;
using BSAsharp.Extensions;
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
        public IEnumerable<long> InflatedChecksums { get; set; }

#if INCLUDES_DEFLATED
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
#endif

        public FileValidation(SerializationInfo info, StreamingContext context)
        {
            InflatedChecksums = DeserializeInfo<IEnumerable<long>>(info, "InflatedChecksums");
            InflatedFilesize = DeserializeInfo<uint>(info, "InflatedFilesize");
#if INCLUDES_DEFLATED
            DeflatedChecksums = DeserializeInfo<IEnumerable<long>>(info, "DeflatedChecksums");
            DeflatedFilesize = DeserializeInfo<uint>(info, "DeflatedFilesize");
#endif
        }
        public FileValidation(IEnumerable<byte[]> data, uint size)
        {
            InflatedChecksums = IncrementalChecksum(data, size);
            InflatedFilesize = size;
        }
        public FileValidation(byte[] data, uint size)
        {
            InflatedChecksums = IncrementalChecksum(data);
            InflatedFilesize = size;
        }
        private FileValidation() { }

        private static T DeserializeInfo<T>(SerializationInfo info, string name)
        {
            return (T)info.GetValue(name, typeof(T));
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("InflatedChecksums", InflatedChecksums.ToArray());
            info.AddValue("InflatedFilesize", InflatedFilesize);
#if INCLUDES_DEFLATED
            info.AddValue("DeflatedChecksums", DeflatedChecksums != null ? DeflatedChecksums.ToArray() : null);
            info.AddValue("DeflatedFilesize", DeflatedFilesize);
#endif
        }

        public static Dictionary<string, FileValidation> FromBSA(BSAWrapper BSA)
        {
            return BSA
                .SelectMany(folder => folder)
                .ToDictionary(file => file.Filename, file => FromBSAFile(file));
        }

        public override string ToString()
        {
            if (InflatedChecksums != null)
                return string.Format("({0}, {1} bytes)", InflatedChecksums.LastOrDefault(), InflatedFilesize);
            return base.ToString();
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

#if INCLUDES_DEFLATED
            if (DeflatedChecksums != null && obj.DeflatedChecksums != null)
            {
                if (DeflatedFilesize == obj.DeflatedFilesize)
                {
                    if (DeflatedChecksums.SequenceEqual(obj.DeflatedChecksums))
                        return true;
                }
            }
#endif
            if (InflatedChecksums != null && obj.InflatedChecksums != null)
            {
                if (InflatedFilesize == obj.InflatedFilesize)
                {
                    if (InflatedFilesize == 0)
                        return true;
                    return InflatedChecksums.SequenceEqual(obj.InflatedChecksums);
                }
            }

            return false;
        }

        public static FileValidation FromBSAFile(BSAFile file)
        {
            var val = new FileValidation(file.YieldContents(true, WINDOW), file.OriginalSize);

#if INCLUDES_DEFLATED
            if (file.IsCompressed)
            {
                val._deflatedChecksums = new Lazy<IEnumerable<long>>(() => IncrementalChecksum(file.GetYieldingFileData(false), file.DataSize));
                val.DeflatedFilesize = file.DataSize;
            }
#endif

            return val;
        }

        public static FileValidation FromFile(string path)
        {
            return new FileValidation(ReadWindow(File.OpenRead(path)), (uint)new FileInfo(path).Length);
        }

        private static IEnumerable<byte[]> ReadWindow(Stream readStream)
        {
            int bytesRead;
            byte[] buf = new byte[WINDOW];

            using (readStream)
            {
                while ((bytesRead = readStream.Read(buf, 0, WINDOW)) != 0)
                    yield return buf.TrimBuffer(0, bytesRead);
            }
        }

        //public static IEnumerable<long> IncrementalChecksumA(IEnumerable<byte> data, uint size)
        //{
        //    IChecksum chk;
        //    if (size < WINDOW)
        //        chk = new Crc32();
        //    else
        //        chk = new Adler32();

        //    int i = 0;
        //    foreach (var db in data)
        //    {
        //        chk.Update(db);
        //        if (++i % WINDOW == 0)
        //        {
        //            i = 0;
        //            yield return chk.Value;
        //        }
        //    }

        //    if (i > 0)
        //        yield return chk.Value;
        //}

        private static IEnumerable<long> IncrementalChecksum(byte[] data)
        {
            IChecksum chk;
            if (data.Length < WINDOW)
                chk = new Crc32();
            else
                chk = new Adler32();

            for (int i = 0; i < data.Length; i += WINDOW)
            {
                var len = i + WINDOW > data.Length ? data.Length - i : WINDOW;

                chk.Update(data, i, len);
                yield return chk.Value;
            }
        }

        private static IEnumerable<long> IncrementalChecksum(IEnumerable<byte[]> data, uint size)
        {
            //Debug.Assert(size > 0 || data.SelectMany(buf => buf).Count() == 0);
            if (size == 0) //still need a check that this isn't happening when data isn't blank
                yield break;

            IChecksum chk;
            if (size < WINDOW)
                chk = new Crc32();
            else
                chk = new Adler32();

            foreach (var buf in data)
            {
                chk.Update(buf);
                yield return chk.Value;
            }
        }
    }
}
