using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using BSAsharp;
using BSAsharp.Extensions;
using TaleOfTwoWastelands.Patching.Murmur;

namespace TaleOfTwoWastelands.Patching
{
    public class FileValidation : IDisposable, IEquatable<FileValidation>
    {
        public enum ChecksumType : byte
        {
            Murmur128,
            Md5
        }

        public uint Filesize { get; private set; }
        public BigInteger Checksum { get { return _computeChecksum.Value; } }
        public ChecksumType Type { get; private set; }

        readonly Stream _stream;
        Lazy<BigInteger> _computeChecksum;

        public FileValidation(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("data must have contents");

            SetContents(() =>
            {
                using (var Hash = GetHash())
                    return Hash.ComputeHash(data).ToBigInteger();
            }, (uint)data.LongLength);
        }
        public FileValidation(Stream stream)
        {
            if (stream == null || stream.Length == 0)
                throw new ArgumentException("stream must have contents");

            _stream = stream;
            SetContents(() =>
            {
                using (stream)
                using (var Hash = GetHash())
                    return Hash.ComputeHash(stream).ToBigInteger();
            }, (uint)stream.Length);
        }
        public FileValidation(BigInteger checksum, uint filesize, ChecksumType type = ChecksumType.Murmur128)
        {
            if (checksum == BigInteger.Zero)
                throw new ArgumentException("checksum must have a value");
            if (filesize == 0)
                throw new ArgumentException("filesize must have a value");

            SetContents(() => checksum, filesize, type);
        }
        private FileValidation(BinaryReader reader, byte typeByte)
        {
            Debug.Assert(typeByte != byte.MaxValue);

            var type = (FileValidation.ChecksumType)typeByte;
            var filesize = reader.ReadUInt32();

            var chkLength = reader.ReadByte();
            var chkBytes = reader.ReadBytes(chkLength);
            var checksum = chkBytes.ToBigInteger();

            Debug.Assert(filesize != 0 && checksum != BigInteger.Zero);
            SetContents(() => checksum, filesize, type);
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
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void SetContents(Func<BigInteger> getChecksum, uint filesize, ChecksumType type = ChecksumType.Murmur128)
        {
            if (filesize == 0)
                throw new ArgumentException("filesize must have a value");

            _computeChecksum = new Lazy<BigInteger>(getChecksum);
            Filesize = filesize;
            Type = type;
        }

        private void WriteTo(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(Filesize);

            var chkBytes = Checksum.ToByteArray();
            writer.Write((byte)chkBytes.Length);
            writer.Write(chkBytes);
        }

        private HashAlgorithm GetHash()
        {
            return Murmur128.CreateMurmur();
        }

        public override string ToString()
        {
            return string.Format("({0:x32}, {1} bytes, {2})", Checksum, Filesize, Enum.GetName(typeof(ChecksumType), Type));
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FileValidation);
        }

        public bool Equals(FileValidation obj)
        {
            if (obj == null)
                return false;

            Debug.Assert(Type == obj.Type);

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

        public static Dictionary<string, FileValidation> FromBSA(BSAWrapper BSA)
        {
            return BSA
                .SelectMany(folder => folder)
                .ToDictionary(file => file.Filename, file => FromBSAFile(file));
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
                throw new FileNotFoundException("path", path);

            var fileInfo = new FileInfo(path);
            if (fileInfo.Length == 0)
                return null;

            return new FileValidation(File.OpenRead(path));
        }

        public static FileValidation FromMd5(BigInteger md5)
        {
            return new FileValidation(md5, uint.MaxValue, FileValidation.ChecksumType.Md5);
        }

        internal static FileValidation ReadFrom(BinaryReader reader)
        {
            var typeByte = reader.ReadByte();
            if (typeByte != byte.MaxValue)
                return new FileValidation(reader, typeByte);

            return null;
        }

        internal static void WriteTo(BinaryWriter writer, FileValidation fv)
        {
            if (fv != null)
                fv.WriteTo(writer);
            else
                writer.Write(byte.MaxValue);
        }
    }
}
