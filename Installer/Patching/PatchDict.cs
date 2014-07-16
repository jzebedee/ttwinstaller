using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TaleOfTwoWastelands.Patching.Murmur;
using Md5Patch = System.Tuple<string, ulong, byte[]>;

namespace TaleOfTwoWastelands.Patching
{
    public class PatchDict : Dictionary<string, PatchInfo[]>
    {
        public List<Md5Patch> Md5Patches { get; private set; }

        public PatchDict(int size)
            : base(size)
        {
            Md5Patches = new List<Md5Patch>();
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

                var md5Count = reader.ReadInt32();
                Md5Patches = new List<Md5Patch>(md5Count);
                for (; md5Count-- > 0; )
                {
                    var file = reader.ReadString();
                    var md5 = reader.ReadUInt64();
                    var diffSize = reader.ReadUInt32();
                    var diffBytes = reader.ReadBytes((int)diffSize);
                    Debug.Assert(diffBytes.Length == diffSize);
                    Md5Patches.Add(Tuple.Create(file, md5, diffBytes));
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

                writer.Write(Md5Patches.Count);
                foreach (var md5patch in Md5Patches)
                {
                    writer.Write(md5patch.Item1);
                    writer.Write(md5patch.Item2);
                    writer.Write((uint)md5patch.Item3.LongLength);
                    writer.Write(md5patch.Item3);
                }
            }
        }

        public new void Add(string key, params PatchInfo[] patches)
        {
            base.Add(key, patches);
        }
        public void AddMd5(string key, string md5str, byte[] diffBytes)
        {
            var md5 = md5str.HexToByteArray().ToUInt64();
            Md5Patches.Add(Tuple.Create(key, md5, diffBytes));
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
