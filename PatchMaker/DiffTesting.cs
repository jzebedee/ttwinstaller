#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleOfTwoWastelands.Patching;

namespace PatchMaker
{
    static class DiffTesting
    {
        public static void Run()
        {
            var Desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            var Old = Desktop + @"\test\theme.html";
            var OldBytes = File.ReadAllBytes(Old);
            var New = Desktop + @"\test\time.html";
            var NewBytes = File.ReadAllBytes(New);
            var Out = Desktop + @"\test\theme_to_time.diff";

            //create
            {
                var ms = new MemoryStream();
                Diff.Create(OldBytes, NewBytes, Diff.SIG_LZDIFF41, ms);
                File.WriteAllBytes(Out, ms.ToArray());
            }

            //apply
            {
                var OutBytes = File.ReadAllBytes(Out);
                var ms = new MemoryStream();
                unsafe
                {
                    fixed (byte* pOld = OldBytes)
                    fixed (byte* pOut = OutBytes)
                    {
                        Diff.Apply(pOld, OldBytes.LongLength, pOut, OutBytes.LongLength, ms);
                        File.WriteAllBytes(@"C:\Users\James\Desktop\test\recreated___time.html", ms.ToArray());
                    }
                }
            }
        }
    }
}
#endif