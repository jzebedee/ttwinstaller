using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using ICSharpCode.SharpZipLib.Checksums;
using TaleOfTwoWastelands.Patching.Murmur;

namespace PatchMaker
{
    static class BenchmarkHash
    {
        internal static void Run()
        {
            const int iterations = 0x20000;
            const int testSize = 0x10000;

            List<long> timeMurmur = new List<long>(iterations);
            List<long> timeAdler = new List<long>(iterations);
            List<long> timeMd5 = new List<long>(iterations);

            byte[] randBytes = new byte[testSize];
            var rnd = new Random();

            var watchMurmur = new Stopwatch();
            var watchAdler = new Stopwatch();
            var watchMd5 = new Stopwatch();

            bool dry = true;
        DoRun:
            for (int i = 0; i < iterations; i++)
            {
                rnd.NextBytes(randBytes);

                {
                    var murmur = Murmur128.CreateMurmur();
                    watchMurmur.Restart();
                    murmur.ComputeHash(randBytes);
                    watchMurmur.Stop();

                    if (!dry)
                        timeMurmur.Add(watchMurmur.ElapsedTicks);
                }

                {
                    var adler = new Adler32();
                    watchAdler.Restart();
                    adler.Update(randBytes);
                    watchAdler.Stop();

                    if (!dry)
                        timeAdler.Add(watchAdler.ElapsedTicks);
                }

                {
                    var md5 = MD5.Create();
                    watchMd5.Restart();
                    md5.ComputeHash(randBytes);
                    watchMd5.Stop();

                    if (!dry)
                        timeMd5.Add(watchMd5.ElapsedTicks);
                }

                if (i == 0x1000 && dry)
                {
                    dry = false;
                    goto DoRun;
                }
            }

            var avgMurmur = TimeSpan.FromTicks(Convert.ToInt64(timeMurmur.Average()));
            var avgAdler = TimeSpan.FromTicks(Convert.ToInt64(timeAdler.Average()));
            var avgMd5 = TimeSpan.FromTicks(Convert.ToInt64(timeMd5.Average()));

            Console.WriteLine("Average murmur:\t" + avgMurmur);
            Console.WriteLine("Average adler:\t" + avgAdler);
            Console.WriteLine("Average md5:\t" + avgMd5);
            Console.ReadKey();
        }
    }
}
