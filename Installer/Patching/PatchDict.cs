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
    }
}
