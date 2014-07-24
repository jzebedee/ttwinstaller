using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TaleOfTwoWastelands.Patching.Murmur;

namespace TaleOfTwoWastelands.Patching
{
    using Patch = Tuple<FileValidation, PatchInfo[]>;

    public class PatchDict : Dictionary<string, Patch>
    {
        public PatchDict(int size)
            : base(size)
        {
        }
        public PatchDict(string file)
            : this(new BinaryReader(File.OpenRead(file)))
        {
        }
        private PatchDict(BinaryReader reader)
            : this(reader, reader.ReadInt32())
        {
        }
        private PatchDict(BinaryReader reader, int size)
            : this(size)
        {
            using (reader)
            {
                for (; size-- > 0; )
                {
                    var key = reader.ReadString();
                    Add(key, ReadPatches(reader));
                }

                Debug.Assert(reader.BaseStream.Position == reader.BaseStream.Length);
            }
        }

        public void WriteAll(Stream outStream)
        {
            using (var writer = new BinaryWriter(outStream))
            {
                writer.Write(this.Count);
                foreach (var kvp in this)
                {
                    writer.Write(kvp.Key);
                    writer.Write(WritePatches(kvp.Value));
                }
            }
        }

        private Patch ReadPatches(BinaryReader reader)
        {
            var target = FileValidation.ReadFrom(reader);

            var patchCount = reader.ReadInt32();
            var patches = new PatchInfo[patchCount];
            for (int i = 0; i < patchCount; i++)
                patches[i] = new PatchInfo(reader);

            return new Patch(target, patches);
        }

        private byte[] WritePatches(Patch patch)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                FileValidation.WriteTo(writer, patch.Item1);

                var patchInfos = patch.Item2;
                if (patchInfos != null)
                {
                    writer.Write(patchInfos.Length);
                    foreach (var patchInfo in patchInfos)
                        patchInfo.WriteTo(writer);
                }
                else
                {
                    writer.Write(0);
                }

                return ms.ToArray();
            }
        }

#if LEGACY
        public static PatchDict FromOldDatabase(IDictionary<string, string> oldDict, string prefix, Func<byte[], byte[]> convertPatch)
        {
            Debug.Assert(oldDict != null);
            var patchDict = new PatchDict(oldDict.Count);

            foreach (var kvp in oldDict)
            {
                var file = kvp.Key;
                var newMd5 = Util.FromMD5String(kvp.Value);

                Debug.Assert(Util.MakeMD5String(newMd5) == kvp.Value);

                var newChk = new FileValidation(newMd5, 0, FileValidation.ChecksumType.Md5);

                var patches = new List<PatchInfo>();

                var diffPath = Path.Combine(prefix, file + ".." + kvp.Value + ".diff");

                //diffPath doesn't exist, so this will include the "right" diff
                var diffs = Util.FindAlternateVersions(diffPath);
                if (diffs != null)
                {
                    foreach (var diff in diffs)
                    {
                        //var altDiffBytes = convertPatch(altDiff.Item1, Diff.SIG_LZDIFF41);
                        var diffBytes = convertPatch(File.ReadAllBytes(diff.Item1));
                        var diffChk = new FileValidation(diff.Item2, 0, FileValidation.ChecksumType.Md5);

                        patches.Add(PatchInfo.FromOldDiff(diffBytes, diffChk));
                    }
                }

                patchDict.Add(file, new Patch(newChk, patches.ToArray()));
            }

            return patchDict;
        }
#endif
    }
}
