using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TaleOfTwoWastelands.Patching
{
    public class PatchDict : Dictionary<string, PatchInfo[]>
    {
        public PatchDict(int size)
            : base(size)
        {
        }
        public PatchDict(string file)
            : base()
        {
            using (var stream = File.OpenRead(file))
            using (var reader = new BinaryReader(stream))
            {
                while (stream.Position < stream.Length)
                {
                    var key = reader.ReadString();
                    Add(key, ReadPatches(reader));
                }
            }
        }

        public void WriteAll(Stream outStream)
        {
            using (var writer = new BinaryWriter(outStream))
            {
                foreach (var kvp in this)
                {
                    writer.Write(kvp.Key);
                    writer.Write(WritePatches(kvp.Value));
                }
            }
        }

        public new void Add(string key, params PatchInfo[] patches)
        {
            base.Add(key, patches);
        }

        private PatchInfo[] ReadPatches(BinaryReader reader)
        {
            var patchCount = reader.ReadInt32();
            var patches = new PatchInfo[patchCount];
            for (int i = 0; i < patchCount; i++)
            {
                patches[i] = new PatchInfo(reader);
            }

            return patches;
        }

        private byte[] WritePatches(PatchInfo[] patches)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(patches.Length);
                foreach (var patch in patches)
                {
                    patch.WriteTo(writer);
                }

                return ms.ToArray();
            }
        }
    }
}
