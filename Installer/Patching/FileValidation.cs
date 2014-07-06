using BSAsharp;
using BSAsharp.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Murmur;

namespace TaleOfTwoWastelands.Patching
{
    public class FileValidation : IDisposable
    {
        const int WINDOW = 0x1000;

        private readonly Murmur32 Hash32 = MurmurHash.Create32(managed: false);

        public uint Filesize { get; private set; }
        private uint[] _writtenChecksums;

        public IEnumerable<uint> Checksums
        {
            get
            {
                return _writtenChecksums ?? (_lazyChecksums != null ? (Checksums = _lazyChecksums.Value) : null);
            }
            private set
            {
                if (_writtenChecksums != value)
                {
                    _writtenChecksums = value != null ? value.ToArray() : null;
                }
            }
        }

        private readonly Stream _inStream;
        private readonly Lazy<IEnumerable<uint>> _lazyChecksums;

        public FileValidation(byte[] data, uint size)
        {
            _lazyChecksums = new Lazy<IEnumerable<uint>>(() => getHashes(data));
            Filesize = size;
        }
        public FileValidation(Stream stream, uint? size = null)
        {
            _inStream = stream;
            _lazyChecksums = new Lazy<IEnumerable<uint>>(() => getHashes(stream));
            Filesize = size ?? (uint)stream.Length;
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
            if (Checksums != null)
                return string.Format("({0}, {1} bytes)", Checksums.LastOrDefault(), Filesize);
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

            if (Filesize == 0 && obj.Filesize == 0)
                return true;

            if (Checksums != null && obj.Checksums != null)
            {
                if (Filesize == obj.Filesize)
                {
                    return Checksums.SequenceEqual(obj.Checksums);
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

        internal static FileValidation FromMap(uint Filesize, uint[] Checksums)
        {
            return new FileValidation()
            {
                Filesize = Filesize,
                _writtenChecksums = Checksums
            };
        }

        public static bool IsEmpty(FileValidation fv)
        {
            return fv == null || (fv.Filesize == 0 && fv.Checksums.Count() == 0);
        }

        private static IEnumerable<byte[]> ReadWindow(Stream readStream)
        {
            int bytesRead;
            byte[] buf = new byte[WINDOW];

            while ((bytesRead = readStream.Read(buf, 0, WINDOW)) != 0)
                yield return buf.TrimBuffer(0, bytesRead);
        }

        private uint getHash(byte[] buf)
        {
            return BitConverter.ToUInt32(Hash32.ComputeHash(buf), 0);
        }

        private IEnumerable<uint> getHashes(byte[] buf)
        {
            return getHashes(new MemoryStream(buf));
        }

        private IEnumerable<uint> getHashes(Stream stream)
        {
            using (stream)
                foreach (var buf in ReadWindow(stream))
                    yield return getHash(buf);
        }
    }
}
