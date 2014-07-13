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

        private readonly HashAlgorithm Hash = Murmur128.Create();

        public uint Filesize { get; private set; }
        public ulong Checksum { get { return _computeChecksum.Value; } }

        readonly Lazy<ulong> _computeChecksum;

        public FileValidation(byte[] data)
        {
            _computeChecksum = new Lazy<ulong>(() => Hash.ComputeHash(data).ToUInt64());
            Filesize = (uint)data.LongLength;
        }
        public FileValidation(Stream stream)
        {
            _computeChecksum = new Lazy<ulong>(() => Hash.ComputeHash(stream).ToUInt64());
            Filesize = (uint)stream.Length;
        }
        public FileValidation(ulong checksum, uint filesize)
        {
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

            if (Filesize == 0 && obj.Filesize == 0)
                return true;

            if (Filesize == obj.Filesize)
                return Checksum == obj.Checksum;

            return false;
        }

        public static FileValidation FromBSAFile(BSAFile file)
        {
            return new FileValidation(file.GetContents(true));
        }

        public static FileValidation FromFile(string path)
        {
            return new FileValidation(File.OpenRead(path));
        }

        public static bool IsEmpty(FileValidation fv)
        {
            return fv == null || fv.Filesize == 0;
        }
    }
}
