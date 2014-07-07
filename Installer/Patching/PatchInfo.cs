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

        /// <summary>
        /// Used only in PatchMaker
        /// </summary>
        public static PatchInfo FromFile(string prefix, string oldFilename, string newFilename)
        {
            var oldChksum = Util.GetMD5(oldFilename);
            var newChksum = Util.GetMD5(newFilename);
            prefix = !string.IsNullOrEmpty(prefix) ? Path.Combine(BSADiff.PatchDir, prefix) : BSADiff.PatchDir;

            var diffPath = Path.Combine(prefix, Path.GetFileName(oldFilename) + "." + oldChksum + "." + newChksum + ".diff");
            byte[] diffData = GetDiff(diffPath, false);

            return new PatchInfo()
            {
                Metadata = FileValidation.FromFile(oldFilename),
                Data = diffData
            };
        }

        private static unsafe byte[] GetDiff(string diffPath, bool bz2Convert)
        {
            byte[] diffData = null;
            if (File.Exists(diffPath))
            {
                var diffBytes = File.ReadAllBytes(diffPath);
                if (bz2Convert)
                    fixed (byte* pBz2 = diffBytes)
                        diffData = ConvertBz2ToLzma(pBz2, diffBytes.Length);
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
            prefix = !string.IsNullOrEmpty(prefix) ? Path.Combine(BSADiff.PatchDir, prefix) : BSADiff.PatchDir;

            var diffPath = Path.Combine(prefix, filename + "." + oldChk + "." + newChk + ".diff");
            byte[] diffData = GetDiff(diffPath, true);

            return new PatchInfo()
            {
                Metadata = newChkVal,
                Data = diffData
            };
        }

        /// <summary>
        /// Used only in PatchMaker
        /// </summary>
        private static unsafe byte[] ConvertBz2ToLzma(byte* pBz2, long length)
        {
            Func<long, long, Stream> openPatchStream = (u_offset, u_length) =>
                new UnmanagedMemoryStream(pBz2 + u_offset, u_length > 0 ? u_length : length - u_offset);

            /*
            File format:
                0	    8	"BSDIFF40"
                8	    8	X
                16	    8	Y
                24	    8	sizeof(newfile)
                32      X	bzip2(control block)
                32+X	Y	bzip2(diff block)
                32+X+Y	???	bzip2(extra block)
            with control block a set of triples (x,y,z) meaning "add x bytes
            from oldfile to x bytes from the diff block; copy y bytes from the
            extra block; seek forwards in oldfile by z bytes".
            */

            byte[] header;
            using (var inputStream = openPatchStream(0, BinaryPatchUtility.HEADER_SIZE))
            using (var reader = new BinaryReader(inputStream))
                header = reader.ReadBytes(BinaryPatchUtility.HEADER_SIZE);

            // check for appropriate magic
            long signature = BinaryPatchUtility.ReadInt64(header, 0);
            if (signature != BinaryPatchUtility.FILE_SIGNATURE)
                throw new InvalidOperationException("Corrupt patch.");

            // read lengths from header
            var controlLength = BinaryPatchUtility.ReadInt64(header, sizeof(long));
            var diffLength = BinaryPatchUtility.ReadInt64(header, sizeof(long) * 2);
            var newSize = BinaryPatchUtility.ReadInt64(header, sizeof(long) * 3);
            if (controlLength < 0 || diffLength < 0 || newSize < 0)
                throw new InvalidOperationException("Corrupt patch.");

            using (MemoryStream
                msControlStream = new MemoryStream(),
                msDiffStream = new MemoryStream(),
                msExtraStream = new MemoryStream())
            {
                using (Stream
                    inControlStream = openPatchStream(BinaryPatchUtility.HEADER_SIZE, controlLength),
                    inDiffStream = openPatchStream(BinaryPatchUtility.HEADER_SIZE + controlLength, diffLength),
                    inExtraStream = openPatchStream(BinaryPatchUtility.HEADER_SIZE + controlLength + diffLength, -1))
                using (BZip2InputStream
                    bz2ControlStream = new BZip2InputStream(inControlStream),
                    bz2DiffStream = new BZip2InputStream(inDiffStream),
                    bz2ExtraStream = new BZip2InputStream(inExtraStream))
                using (LzmaEncodeStream
                    lzmaControlStream = new LzmaEncodeStream(msControlStream),
                    lzmaDiffStream = new LzmaEncodeStream(msDiffStream),
                    lzmaExtraStream = new LzmaEncodeStream(msExtraStream))
                {
                    bz2ControlStream.CopyTo(lzmaControlStream);
                    bz2DiffStream.CopyTo(lzmaDiffStream);
                    bz2ExtraStream.CopyTo(lzmaExtraStream);
                }

                var lzmaControlBytes = msControlStream.ToArray();
                var lzmaDiffBytes = msDiffStream.ToArray();
                var lzmaExtraBytes = msExtraStream.ToArray();

                using (var msOut = new MemoryStream())
                using (var writer = new BinaryWriter(msOut))
                {
                    writer.Write(signature);
                    writer.Write(lzmaControlBytes.LongLength);
                    writer.Write(lzmaDiffBytes.LongLength);
                    //BinaryPatchUtility.WriteInt64(lzmaExtraBytes.LongLength, writer);
                    writer.Write(newSize);
                    writer.Write(lzmaControlBytes);
                    writer.Write(lzmaDiffBytes);
                    writer.Write(lzmaExtraBytes);

                    return msOut.ToArray();
                }
            }
        }
    }
}
