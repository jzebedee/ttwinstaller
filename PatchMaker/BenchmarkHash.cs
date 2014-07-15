using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PatchMaker
{
    static class BenchmarkHash
    {
        internal static void Run()
        {
            List<long> timeAdler = new List<long>(0x10000);
            List<long> timeMurmur = new List<long>(0x10000);

            byte[] randBytes = new byte[0x1000];
            var rnd = new Random();

            var watchAdler = new Stopwatch();
            var watchMurmur = new Stopwatch();

            bool dry = true;
        DoRun:
            for (int i = 0; i < 0x10000; i++)
            {
                rnd.NextBytes(randBytes);

                {
                    var adler = new ICSharpCode.SharpZipLib.Checksums.Adler32();
                    watchAdler.Restart();
                    adler.Update(randBytes);
                    watchAdler.Stop();

                    if (!dry)
                        timeAdler.Add(watchAdler.ElapsedTicks);
                }

                {
                    var murmur = TaleOfTwoWastelands.Patching.Murmur.Murmur128.CreateMurmur();
                    watchMurmur.Restart();
                    murmur.ComputeHash(randBytes);
                    watchMurmur.Stop();

                    if (!dry)
                        timeMurmur.Add(watchMurmur.ElapsedTicks);
                }

                if (i == 0x1000 && dry)
                {
                    dry = false;
                    goto DoRun;
                }
            }

            var avgAdler = TimeSpan.FromTicks(Convert.ToInt64(timeAdler.Average()));
            var avgMurmur = TimeSpan.FromTicks(Convert.ToInt64(timeMurmur.Average()));

            Console.WriteLine("Average adler:\t" + avgAdler);
            Console.WriteLine("Average murmur:\t" + avgMurmur);
            Console.ReadKey();
        }
    }
}
