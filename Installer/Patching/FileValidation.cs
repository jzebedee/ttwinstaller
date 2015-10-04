using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using BSAsharp;
using Patching.Murmur;

namespace TaleOfTwoWastelands.Patching
{
    public class FileValidation : IEquatable<FileValidation>
    {
        [DllImport("msvcrt", CallingConvention = CallingConvention.Cdecl)]
        private static extern int memcmp(byte[] b1, byte[] b2, UIntPtr count);

        public enum ChecksumType : byte
        {
            Murmur128,
            Md5
        }

        public long Filesize { get; }
        public byte[] Checksum { get; }
        public ChecksumType Type { get; }

        public FileValidation(byte[] data, ChecksumType type = ChecksumType.Murmur128)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            Filesize = data.LongLength;
            Type = type;
            using (var hash = GetHash())
                Checksum = hash.ComputeHash(data);
        }
        public FileValidation(Stream stream, ChecksumType type = ChecksumType.Murmur128)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            Filesize = stream.Length;
            Type = type;
            using (stream)
            using (var hash = GetHash())
                Checksum = hash.ComputeHash(stream);
        }
        public FileValidation(byte[] checksum, uint filesize, ChecksumType type = ChecksumType.Murmur128)
        {
            if (checksum == null)
                throw new ArgumentNullException(nameof(checksum));
            if (checksum.Length != 16)
                throw new ArgumentException("checksum must be 128bit");
            if (filesize == 0)
                filesize = uint.MaxValue;
            //throw new ArgumentException("filesize must have a value");

            Filesize = filesize;
            Type = type;
            Checksum = checksum;
        }
        public FileValidation(string path, ChecksumType type = ChecksumType.Murmur128) : this(File.OpenRead(path), type) { }
        private FileValidation(BinaryReader reader, byte typeByte)
        {
            Debug.Assert(typeByte != byte.MaxValue);

            Type = (ChecksumType)typeByte;
            Filesize = reader.ReadUInt32();
            Checksum = reader.ReadBytes(16);
        }

        private void WriteTo(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write((uint)Filesize);
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
                    throw new NotImplementedException($"Unknown checksum type: {Type}");
            }
        }

        public override string ToString()
        {
            return $"({BitConverter.ToString(Checksum)}, {Filesize} bytes, {Enum.GetName(typeof(ChecksumType), Type)})";
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

        public static FileValidation ReadFrom(BinaryReader reader)
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
