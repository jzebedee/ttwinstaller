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
            var checksumCount = reader.ReadInt32();
            var checksums = new uint[checksumCount];
            for (int j = 0; j < checksumCount; j++)
            {
                checksums[j] = reader.ReadUInt32();
            }
            this.Metadata = FileValidation.FromMap(filesize, checksums);

            //reading data now
            var dataSize = reader.ReadInt32();
            this.Data = reader.ReadBytes(dataSize);
        }

        public void WriteTo(BinaryWriter writer)
        {
            if (Metadata != null)
            {
                writer.Write(Metadata.Filesize);
                if (Metadata.Filesize > 0)
                {
                    var checksums = Metadata.Checksums.ToArray();

                    writer.Write(checksums.Length);
                    foreach (var chk in checksums)
                        writer.Write(chk);
                }
                else
                {
                    writer.Write(0);
                }
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

        ///// <summary>
        ///// Used only in PatchMaker
        ///// </summary>
        //public static PatchInfo FromFile(string prefix, string oldFilename, string newFilename)
        //{
        //    var oldChksum = Util.GetMD5(oldFilename);
        //    var newChksum = Util.GetMD5(newFilename);

        //    prefix = Path.Combine(prefix, "TTW Patches");
        //    var diffPath = Path.Combine(prefix, Path.GetFileName(oldFilename) + "." + oldChksum + "." + newChksum + ".diff");
        //    byte[] diffData = GetDiff(diffPath, true);

        //    return new PatchInfo()
        //    {
        //        Metadata = FileValidation.FromFile(oldFilename),
        //        Data = diffData
        //    };
        //}

        private static unsafe byte[] GetDiff(string diffPath, bool bz2Convert)
        {
            byte[] diffData = null;
            if (File.Exists(diffPath))
            {
                var diffBytes = File.ReadAllBytes(diffPath);
                if (bz2Convert)
                    fixed (byte* pBz2 = diffBytes)
                        diffData = BinaryPatchUtility.ConvertBz2ToLzma(pBz2, diffBytes.Length);
                else
                    diffData = diffBytes;
                //File.Move(diffPath, Path.ChangeExtension(diffPath, ".used"));
            }

            return diffData;
        }

        /// <summary>
        /// Used only in PatchMaker
        /// </summary>
        public static PatchInfo FromFileChecksum(string prefix, string filename, string oldChk, string newChk, FileValidation newChkVal)
        {
            prefix = Path.Combine(prefix, "TTW Patches");
            var diffPath = Path.Combine(prefix, filename + "." + oldChk + "." + newChk + ".diff");
            byte[] diffData = GetDiff(diffPath, true);

            return new PatchInfo()
            {
                Metadata = newChkVal,
                Data = diffData
            };
        }
    }
}
