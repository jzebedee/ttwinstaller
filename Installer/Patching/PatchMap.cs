using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;

namespace TaleOfTwoWastelands.Patching
{
    public class PatchMap
    {
        private readonly RHBackshiftMap _map;

        private readonly Dictionary<string, Action> _lazyMapHitter = new Dictionary<string, Action>();
        private readonly MemoryMappedFile _mmf;

        public PatchMap(uint size) : this(new RHBackshiftMap(size)) { }
        public PatchMap(RHBackshiftMap map)
        {
            this._map = map;
        }
        public PatchMap(string file)
        {
            var fileInfo = new FileInfo(file);

            _mmf = MemoryMappedFile.CreateFromFile(file, FileMode.Open);

            long off = 0;
            using (var acc = _mmf.CreateViewAccessor(0, 0))
            {
                //while (off < acc.Capacity)
                //MMF length/capacity is aligned to page, NOT file!
                while (off < fileInfo.Length)
                {
                    off += sizeof(uint); //skip hash

                    var keySize = acc.ReadInt32(off);
                    off += sizeof(uint);

                    var valSize = acc.ReadInt32(off);
                    off += sizeof(uint);

                    var keyBytes = new byte[keySize];
                    off += acc.ReadArray<byte>(off, keyBytes, 0, keySize);

                    var key = Encoding.UTF8.GetString(keyBytes);

                    if (string.IsNullOrEmpty(key))
                        System.Diagnostics.Debugger.Break();

                    var newOff = off;
                    _lazyMapHitter.Add(key, () =>
                    {
                        using (var valStream = _mmf.CreateViewStream(newOff, valSize))
                        using (var reader = new BinaryReader(valStream))
                        {
                            _map.Put(key, reader.ReadBytes(valSize));
                        }
                    });

                    off += valSize; //skip data
                }

                _map = new RHBackshiftMap((uint)_lazyMapHitter.Count);
            }
        }

        public Dictionary<string, PatchInfo[]> ToDictionary()
        {
            return _lazyMapHitter.ToDictionary(kvp => kvp.Key, kvp => Get(kvp.Key));
        }

        public void WriteAll(Stream outStream)
        {
            using (var writer = new BinaryWriter(outStream))
            {
                foreach (var bucket in _map.Buckets)
                {
                    if (RHBackshiftMap.IsEmpty(bucket.entry))
                        continue;

                    writer.Write(bucket.hash);
                    writer.Write(bucket.entry.keySize);
                    writer.Write(bucket.entry.valSize);
                    writer.Write(bucket.entry.data);
                }
            }
        }

        public PatchInfo[] Get(string key)
        {
            Action hitter;
            if (_lazyMapHitter.TryGetValue(key, out hitter))
                hitter();

            var bytes = _map.Get(key);
            using (var ms = new MemoryStream(bytes))
            using (var reader = new BinaryReader(ms))
            {
                var patchCount = reader.ReadInt32();
                var patches = new PatchInfo[patchCount];
                for (int i = 0; i < patchCount; i++)
                {
                    //reading a FV (metadata) now
                    var filesize = reader.ReadUInt32();
                    var checksumCount = reader.ReadInt32();
                    var checksums = new uint[checksumCount];
                    for (int j = 0; j < checksumCount; j++)
                    {
                        checksums[j] = reader.ReadUInt32();
                    }
                    //reading data now
                    var dataSize = reader.ReadInt32();
                    var data = reader.ReadBytes(dataSize);

                    patches[i] = new PatchInfo
                    {
                        Metadata = FileValidation.FromMap(filesize, checksums),
                        Data = data
                    };
                }

                return patches;
            }
        }
        public bool Put(string key, params PatchInfo[] patchInfos)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(patchInfos.Length);
                foreach (var patch in patchInfos)
                {
                    if (patch.Metadata != null)
                    {
                        writer.Write(patch.Metadata.Filesize);
                        if (patch.Metadata.Filesize > 0)
                        {
                            writer.Write(patch.Metadata.Checksums.Count());
                            foreach (var chk in patch.Metadata.Checksums)
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

                    if (patch.Data != null)
                    {
                        writer.Write(patch.Data.Length);
                        writer.Write(patch.Data);
                    }
                    else
                    {
                        writer.Write(0);
                    }
                }

                return _map.Put(key, ms.ToArray());
            }
        }
    }
}
