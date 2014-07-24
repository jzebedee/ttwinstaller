using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ICSharpCode.SharpZipLib.BZip2;
using SevenZip;
using TaleOfTwoWastelands.Patching.Murmur;

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
            this.Metadata = FileValidation.ReadFrom(reader);

            //reading data now
            var dataSize = reader.ReadUInt32();
            Debug.Assert((int)dataSize == dataSize);
            if (dataSize > 0)
                this.Data = reader.ReadBytes((int)dataSize);
        }

        public void WriteTo(BinaryWriter writer)
        {
            FileValidation.WriteTo(writer, Metadata);

            if (Data != null)
            {
                writer.Write((uint)Data.LongLength);
                if (Data.Length > 0)
                    writer.Write(Data);
            }
            else
            {
                writer.Write(0U);
            }
        }

        public bool PatchBytes(byte[] inputBytes, FileValidation targetChk, out byte[] outputBytes, out FileValidation outputChk)
        {
            using (var output = new MemoryStream())
            {
                unsafe
                {
                    fixed (byte* pInput = inputBytes)
                    fixed (byte* pPatch = Data)
                        Diff.Apply(pInput, inputBytes.LongLength, pPatch, Data.LongLength, output);
                }

                outputBytes = output.ToArray();

                output.Seek(0, SeekOrigin.Begin);
                outputChk = new FileValidation(outputBytes, targetChk.Type);

                return targetChk == outputChk;
            }
        }

#if LEGACY || DEBUG
        public static PatchInfo FromOldDiff(byte[] diffData, FileValidation oldChk)
        {
            return new PatchInfo()
            {
                Metadata = oldChk,
                Data = diffData
            };
        }
#endif
    }
}
