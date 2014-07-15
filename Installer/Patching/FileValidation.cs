using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using BSAsharp;
using BSAsharp.Extensions;
using TaleOfTwoWastelands.Patching.Murmur;

namespace TaleOfTwoWastelands.Patching
{
    public class FileValidation : IDisposable
    {
        const int WINDOW = 0x1000;

        private readonly HashAlgorithm Hash = Murmur128.CreateMurmur();

        public uint Filesize { get; private set; }
        public ulong Checksum { get { return _computeChecksum.Value; } }

        readonly Lazy<ulong> _computeChecksum;
        readonly Stream _stream;

        public FileValidation(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("data must have contents");

            _computeChecksum = new Lazy<ulong>(() => Hash.ComputeHash(data).ToUInt64());
            Filesize = (uint)data.LongLength;
        }
        public FileValidation(Stream stream)
        {
            if (stream == null || stream.Length == 0)
                throw new ArgumentException("stream must have contents");

            _stream = stream;
            _computeChecksum = new Lazy<ulong>(() =>
            {
                using (stream)
                    return Hash.ComputeHash(stream).ToUInt64();
            });
            Filesize = (uint)stream.Length;
        }
        public FileValidation(ulong checksum, uint filesize)
        {
            if (checksum == 0)
                throw new ArgumentException("checksum must have a value");
            if (filesize == 0)
                throw new ArgumentException("filesize must have a value");

            _computeChecksum = new Lazy<ulong>(() => checksum);
            Filesize = filesize;
        }

        ~FileValidation()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_stream != null)
                    _stream.Dispose();
                Hash.Dispose();
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public static Dictionary<string, FileValidation> FromBSA(BSAWrapper BSA)
        {
            return BSA
                .SelectMany(folder => folder)
                .ToDictionary(file => file.Filename, file => FromBSAFile(file));
        }

        public override string ToString()
        {
            return string.Format("({0:x16}, {1} bytes)", Checksum, Filesize);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FileValidation);
        }

        public bool Equals(FileValidation obj)
        {
            if (obj == null)
                return false;

            if (Filesize != obj.Filesize)
                return false;

            return Checksum == obj.Checksum;
        }

        public static bool operator ==(FileValidation a, FileValidation b)
        {
            bool
                nullA = object.ReferenceEquals(a, null),
                nullB = object.ReferenceEquals(b, null);
            if (nullA || nullB)
            {
                return nullA && nullB;
            }

            return a.Equals(b);
        }

        public static bool operator !=(FileValidation a, FileValidation b)
        {
            return !(a == b);
        }

        public static FileValidation FromBSAFile(BSAFile file)
        {
            var contents = file.GetContents(true);
            if (contents == null || contents.Length == 0)
                return null;

            return new FileValidation(contents);
        }

        public static FileValidation FromFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("path");

            var fileInfo = new FileInfo(path);
            if (fileInfo.Length == 0)
                return null;

            return new FileValidation(File.OpenRead(path));
        }
    }
}
