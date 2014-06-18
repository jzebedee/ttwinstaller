using BSAsharp;
using BSAsharp.Extensions;
using ICSharpCode.SharpZipLib.Checksums;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace TaleOfTwoWastelands.Patching
{
    [ProtoContract]
    public class FileValidation : IDisposable
    {
        const int WINDOW = 0x1000;

        [ProtoMember(1)]
        public uint InflatedFilesize { get; private set; }
        [ProtoMember(2)]
        private long[] _writtenInflatedChecksums;
        public IEnumerable<long> InflatedChecksums
        {
            get
            {
                return _writtenInflatedChecksums ?? (_lazyInflatedChecksums != null ? (InflatedChecksums = _lazyInflatedChecksums.Value) : null);
            }
            private set
            {
                if (_writtenInflatedChecksums != value)
                {
                    _writtenInflatedChecksums = value != null ? value.ToArray() : null;
                }
            }
        }

        private readonly Stream _inStream;
        private readonly Lazy<IEnumerable<long>> _lazyInflatedChecksums;

        public FileValidation(IEnumerable<byte[]> data, uint size)
        {
            _lazyInflatedChecksums = new Lazy<IEnumerable<long>>(() => IncrementalChecksum(data, size));
            InflatedFilesize = size;
        }
        public FileValidation(byte[] data, uint size)
        {
            _lazyInflatedChecksums = new Lazy<IEnumerable<long>>(() => IncrementalChecksum(data));
            InflatedFilesize = size;
        }
        public FileValidation(Stream stream, uint? size = null)
        {
            _inStream = stream;
            _lazyInflatedChecksums = new Lazy<IEnumerable<long>>(() => IncrementalChecksum(ReadWindow(stream), size ?? (uint)stream.Length));
            InflatedFilesize = size ?? (uint)stream.Length;
        }
        private FileValidation() { }
        ~FileValidation()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_inStream != null)
                    _inStream.Dispose();
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public static Dictionary<string, Lazy<FileValidation>> FromBSA(BSAWrapper BSA)
        {
            return BSA
                .SelectMany(folder => folder)
                .ToDictionary(file => file.Filename, file => new Lazy<FileValidation>(() => FromBSAFile(file)));
        }

        public override string ToString()
        {
            //if you stringify ones of these in a debugger window, you're going to get
            //an exception from re-reading the stream after it's finished enumerating
            //NOTE: Lazy<T> makes this not true any more
            if (InflatedChecksums != null)
                return string.Format("({0}, {1} bytes)", InflatedChecksums.LastOrDefault(), InflatedFilesize);
            return base.ToString();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FileValidation);
        }

        public bool Equals(FileValidation obj)
        {
            if (obj == null)
                return false;

            if (InflatedFilesize == 0 && obj.InflatedFilesize == 0)
                return true;

            if (InflatedChecksums != null && obj.InflatedChecksums != null)
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
            return new FileValidation(file.GetContentStream(true), file.OriginalSize);
        }

        public static FileValidation FromFile(string path)
        {
            return new FileValidation(File.OpenRead(path));
        }

        private static uint WindowCount(uint size)
        {
            return (size + WINDOW - 1) / WINDOW;
        }

        private static IEnumerable<byte[]> ReadWindow(Stream readStream)
        {
            int bytesRead;
            byte[] buf = new byte[WINDOW];

            using (readStream)
                while ((bytesRead = readStream.Read(buf, 0, WINDOW)) != 0)
                    yield return buf.TrimBuffer(0, bytesRead);
        }

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
