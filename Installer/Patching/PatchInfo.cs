using System.Diagnostics;
using System.IO;
using Patching.Delta;

namespace TaleOfTwoWastelands.Patching
{
    public class PatchInfo
    {
        public byte[] Data { get; }
        public FileValidation Hash { get; }

        public PatchInfo(byte[] data, FileValidation hash)
        {
            Data = data;
            Hash = hash;
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

        public bool PatchStream(Stream input, FileValidation targetChk, Stream output, out FileValidation outputChk)
        {
            unsafe
            {
                fixed (byte* pPatch = Data)
                    Diff.Apply(input, pPatch, Data.LongLength, output);
            }

            output.Seek(0, SeekOrigin.Begin);
            outputChk = new FileValidation(output, targetChk.Type);

            return targetChk == outputChk;
        }
    }
}
