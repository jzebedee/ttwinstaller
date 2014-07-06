using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using Murmur;

namespace TaleOfTwoWastelands.Patching
{
    public class RHBackshiftMap
    {
        public struct Entry
        {
            public uint keySize;
            public uint valSize;
            public byte[] data;
        };

        public struct Bucket
        {
            public uint hash;
            public Entry entry;
        };

        public ReadOnlyCollection<Bucket> Buckets
        {
            get
            {
                return new ReadOnlyCollection<Bucket>(buckets);
            }
        }

        private Bucket[] buckets;
        private uint bucketsCount;
        private uint bucketsUsedCount;
        private uint maxProbe;

        public RHBackshiftMap(uint size)
        {
            bucketsCount = size;
            bucketsUsedCount = 0;
            maxProbe = size;
            buckets = new Bucket[size];
        }

        public byte[] Get(string key)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var keySize = keyBytes.Length;

            var hash = getHash(key);
            var indexInit = hash % bucketsCount;

            uint probeDistance = 0;
            for (uint i = 0; i < maxProbe; i++)
            {
                uint indexCurrent = (indexInit + i) % bucketsCount;

                var dtii = FillDistanceToInitIndex(indexCurrent);
                if (dtii != null)
                    probeDistance = dtii.Value;
                else break;

                if (i > probeDistance) break;

                var entry = buckets[indexCurrent].entry;
                if (keySize == entry.keySize && entry.data.Take(keySize).SequenceEqual(keyBytes))
                {
                    return entry.data.Skip(keySize).ToArray();
                }
            }

            return null;
        }

        public bool Put(string key, byte[] valBytes)
        {
            if (bucketsUsedCount == bucketsCount)
                return false;

            bucketsUsedCount++;

            var keyBytes = Encoding.UTF8.GetBytes(key);
            var keySize = keyBytes.Length;

            var valSize = valBytes.Length;

            var hash = getHash(key);
            var indexInit = hash % bucketsCount;

            byte[] data = new byte[keySize + valSize];
            Buffer.BlockCopy(keyBytes, 0, data, 0, keySize);
            Buffer.BlockCopy(valBytes, 0, data, keySize, valSize);

            var entry = new Entry
            {
                keySize = (uint)keySize,
                valSize = (uint)valSize,
                data = data
            };

            uint indexCurrent = indexInit;
            uint probeDistance = 0;
            uint probeCurrent = 0;
            int swapCount = 0;

            for (uint i = 0; i < maxProbe; i++)
            {
                indexCurrent = (indexInit + i) % bucketsCount;
                //if (buckets[index_current].entry == null)
                if (IsEmpty(buckets[indexCurrent].entry))
                {
                    buckets[indexCurrent].entry = entry;
                    buckets[indexCurrent].hash = hash;
                    break;
                }
                else
                {
                    probeDistance = FillDistanceToInitIndex(indexCurrent) ?? probeDistance;
                    if (probeCurrent > probeDistance)
                    {
                        var entryTemp = buckets[indexCurrent].entry;
                        buckets[indexCurrent].entry = entry;
                        entry = entryTemp;
                        //entry = Interlocked.Exchange(ref buckets[index_current].entry, entry);
                        hash = Exchange(ref buckets[indexCurrent].hash, hash);

                        probeCurrent = probeDistance;
                        swapCount++;
                    }
                }
                probeCurrent++;
            }

            return true;
        }

        unsafe uint Exchange(ref uint target, uint v)
        {
            fixed (uint* p = &target)
                return (uint)Interlocked.Exchange(ref *(int*)p, (int)v);
        }

        private uint? FillDistanceToInitIndex(uint indexStored)
        {
            if (IsEmpty(buckets[indexStored].entry))
                return null;

            uint indexInit = buckets[indexStored].hash % bucketsCount;
            if (indexInit <= indexStored)
                return indexStored - indexInit;
            else
                return indexStored + (bucketsCount - indexInit);
        }

        public static bool IsEmpty(Entry entry)
        {
            return entry.data == null && entry.keySize == 0 && entry.valSize == 0;
        }

        private uint getHash(string key)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            using (var Hash32 = MurmurHash.Create32(managed: false))
                return BitConverter.ToUInt32(Hash32.ComputeHash(keyBytes), 0);
        }
    }
}
