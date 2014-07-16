using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ICSharpCode.SharpZipLib.BZip2;
using SevenZip;

namespace TaleOfTwoWastelands.Patching
{
    public class PatchInfo
    {
        public FileValidation Metadata { get; set; }
        public byte[] Data { get; set; }

        public PatchInfo() { }
        /// <summary>
        /// Deserialize a PatchInfo
        /// </summary>
        /// <param name="reader">A reader aligned to a serialized PatchInfo</param>
        public PatchInfo(BinaryReader reader)
        {
            //reading a FV (metadata) now
            var filesize = reader.ReadUInt32();
            var checksum = reader.ReadUInt64();
            if (filesize == 0 && checksum == 0)
                this.Metadata = null;
            else
                this.Metadata = new FileValidation(checksum, filesize);

            //reading data now
            var dataSize = reader.ReadInt32();
            if (dataSize > 0)
                this.Data = reader.ReadBytes(dataSize);
        }

        public void WriteTo(BinaryWriter writer)
        {
            if (Metadata != null)
            {
                writer.Write(Metadata.Filesize);
                writer.Write(Metadata.Checksum);
            }
            else
            {
                writer.Write(0);
                writer.Write(0);
            }

            if (Data != null)
            {
                writer.Write(Data.Length);
                writer.Write(Data);
            }
            else
            {
                writer.Write(0);
            }
        }

        /// <summary>
        /// Used only in PatchMaker
        /// </summary>
        public static unsafe byte[] GetDiff(string diffPath, long convertSignature = -1, bool moveToUsed = false)
        {
            if (File.Exists(diffPath))
            {
                var diffBytes = File.ReadAllBytes(diffPath);
                if (convertSignature > 0)
                    fixed (byte* pBz2 = diffBytes)
                        return BinaryPatchUtility.ConvertPatch(pBz2, diffBytes.Length, BinaryPatchUtility.SIG_BSDIFF40, convertSignature);

                if (moveToUsed)
                    File.Move(diffPath, Path.ChangeExtension(diffPath, ".used"));
                return diffBytes;
            }

            return null;
        }

        /// <summary>
        /// Used only in PatchMaker
        /// </summary>
        public static PatchInfo FromOldChecksum(string diffPath, FileValidation newChkVal)
        {
            byte[] diffData = GetDiff(diffPath, BinaryPatchUtility.SIG_LZDIFF41);
            return new PatchInfo()
            {
                Metadata = newChkVal,
                Data = diffData
            };
        }
    }
}
