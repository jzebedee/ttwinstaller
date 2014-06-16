//#define RECHECK
using BSAsharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;

namespace TaleOfTwoWastelands.Patching
{
    class BuildPatchDB
    {
        const string SOURCE_DIR = "BuildDB";
        const string BUILD_DIR = "PatchDB";

        //Shameless code duplication. So sue me.
        public static void Build()
        {
#if RECHECK
            bool keepGoing = true;
#endif

            Directory.CreateDirectory(BUILD_DIR);

            var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();

            var dirTTWMain = Path.Combine(SOURCE_DIR, Installer.MainDir);
            var dirTTWOptional = Path.Combine(SOURCE_DIR, Installer.OptDir);

            var bethKey = Installer.GetBethKey();

            var fo3Key = bethKey.CreateSubKey("Fallout3");
            var Fallout3Path = fo3Key.GetValue("Installed Path", "").ToString();
            var dirFO3Data = Path.Combine(Fallout3Path, "Data");

            foreach (var ESM in Installer.CheckedESMs)
            {
                var fixPath = Path.Combine(BUILD_DIR, Path.ChangeExtension(ESM, ".fix"));
                if (!File.Exists(fixPath))
                {
                    var dataESM = Path.Combine(dirFO3Data, ESM);
                    var ttwESM = Path.Combine(dirTTWMain, ESM);

                    var fvOriginal = FileValidation.FromFile(dataESM);

                    var patch = PatchInfo.FromFile("", dataESM, ttwESM);

                    using (var fixStream = File.OpenWrite(fixPath))
                        bformatter.Serialize(fixStream, new Fixup(fvOriginal, patch));
                }
#if RECHECK
                using (var fixStream = File.OpenRead(fixPath))
                {
                    var p = (Fixup)bformatter.Deserialize(fixStream);
                    if(keepGoing)
                        Debugger.Break();
                }
#endif
            }

            //patDB.Add(join.bsaFile.Filename, patch);

            //var patchDBFilename = Path.Combine("Checksums", Path.ChangeExtension(outBsaFilename, ".pat"));

            //var bformatter = new BinaryFormatter();
            //using (var patStream = File.OpenWrite(patchDBFilename))
            //    bformatter.Serialize(patStream, patDB);
            ////var bsaName = Path.GetFileNameWithoutExtension(inBSAPath);

            ////using (var BSA = new BSAWrapper(inBSAPath))
            ////{
            ////    var chkDBFilename = Path.Combine("Checksums", Path.ChangeExtension(bsaName, ".chk"));

            ////    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            ////    using (var chkStream = File.OpenWrite(chkDBFilename))
            ////    {
            ////        var chkDB = Validation.FromBSA(BSA);
            ////        bformatter.Serialize(chkStream, chkDB);
            ////    }
            ////}
        }
    }
}
