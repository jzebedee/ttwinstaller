using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using BSAsharp;
using TaleOfTwoWastelands.Patching.Murmur;

namespace TaleOfTwoWastelands.Patching
{
    public class FileValidation : IDisposable, IEquatable<FileValidation>
    {
        [DllImport("msvcrt", CallingConvention = CallingConvention.Cdecl)]
        private static extern int memcmp(byte[] b1, byte[] b2, UIntPtr count);

        public enum ChecksumType : byte
        {
            Murmur128,
            Md5
        }

        public uint Filesize { get; private set; }
        public byte[] Checksum { get { return _computeChecksum.Value; } }
        public ChecksumType Type { get; private set; }

        readonly Stream _stream;
        Lazy<byte[]> _computeChecksum;

        public FileValidation(byte[] data, ChecksumType type = ChecksumType.Murmur128)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            SetContents(() =>
            {
                using (var hash = GetHash())
                    return hash.ComputeHash(data);
            }, (uint)data.LongLength, type);
        }
        public FileValidation(Stream stream, ChecksumType type = ChecksumType.Murmur128)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            _stream = stream;
            SetContents(() =>
            {
                using (stream)
                using (var hash = GetHash())
                    return hash.ComputeHash(stream);
            }, (uint)stream.Length, type);
        }
        public FileValidation(byte[] checksum, uint filesize, ChecksumType type = ChecksumType.Murmur128)
        {
            if (checksum == null)
                throw new ArgumentNullException("checksum");
            if (checksum.Length != 16)
                throw new ArgumentException("checksum must be 128bit");
            if (filesize == 0)
                filesize = uint.MaxValue;
            //throw new ArgumentException("filesize must have a value");

            SetContents(() => checksum, filesize, type);
        }
        public FileValidation(string path, ChecksumType type = ChecksumType.Murmur128) : this(File.OpenRead(path), type) { }
        private FileValidation(BinaryReader reader, byte typeByte)
        {
            Debug.Assert(typeByte != byte.MaxValue);

            var type = (ChecksumType)typeByte;
            var filesize = reader.ReadUInt32();
            var checksum = reader.ReadBytes(16);

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

        private void SetContents(Func<byte[]> getChecksum, uint filesize, ChecksumType type)
        {
            _computeChecksum = new Lazy<byte[]>(getChecksum);
            Filesize = filesize;
            Type = type;
        }

        private void WriteTo(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(Filesize);
            writer.Write(Checksum);
        }

        private HashAlgorithm GetHash()
        {
            switch (Type)
            {
                case ChecksumType.Murmur128:
                    return Murmur128.CreateMurmur();
                case ChecksumType.Md5:
                    return MD5.Create();
                default:
                    throw new NotImplementedException("Unknown checksum type: " + Type);
            }
        }

        public override string ToString()
        {
            return string.Format("({0}, {1} bytes, {2})", BitConverter.ToString(Checksum), Filesize, Enum.GetName(typeof(ChecksumType), Type));
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

            if (Filesize != obj.Filesize && Filesize != uint.MaxValue && obj.Filesize != uint.MaxValue)
                return false;

            return Checksum.Length == obj.Checksum.Length && memcmp(Checksum, obj.Checksum, (UIntPtr)Checksum.Length) == 0;
        }

        public static bool operator ==(FileValidation a, FileValidation b)
        {
            bool
                nullA = ReferenceEquals(a, null),
                nullB = ReferenceEquals(b, null);
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

        public static Dictionary<string, FileValidation> FromBSA(BSA bsa)
        {
            return bsa
                .SelectMany(folder => folder)
                .ToDictionary(file => file.Filename, file => FromBSAFile(file));
        }

        public static FileValidation FromBSAFile(BSAFile file, ChecksumType asType = ChecksumType.Murmur128)
        {
            return new FileValidation(file.GetContents(true), asType);
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
