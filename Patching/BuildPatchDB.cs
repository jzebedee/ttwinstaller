﻿//#define RECHECK
using BSAsharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using TaleOfTwoWastelands.ProgressTypes;

namespace TaleOfTwoWastelands.Patching
{
    class BuildPatchDB
    {
        const string SOURCE_DIR = "BuildDB";
        const string BUILD_DIR = "Checksums";

        private static readonly BinaryFormatter BF = new BinaryFormatter();

        //Shameless code duplication. So sue me.
        public static void Build()
        {
#if RECHECK
            bool keepGoing = true;
#endif

            Directory.CreateDirectory(BUILD_DIR);

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
                        BF.Serialize(fixStream, new PatchFixup(fvOriginal, patch));
                }
#if RECHECK
                using (var fixStream = File.OpenRead(fixPath))
                {
                    var p = (PatchFixup)BF.Deserialize(fixStream);
                    if(keepGoing)
                        Debugger.Break();
                }
#endif
            }

            var progressLog = new Progress<string>(s => Debug.Write(s));
            var progressUIMinor = new Progress<OperationProgress>();
            var token = new CancellationTokenSource().Token;
            foreach (var kvpBsa in Installer.BuildableBSAs)
            {
                var inBsaName = kvpBsa.Key;
                var outBsaName = kvpBsa.Value;

                string outBSAFile = Path.ChangeExtension(outBsaName, ".bsa");
                string outBSAPath = Path.Combine(dirTTWMain, outBSAFile);

                string inBSAFile = Path.ChangeExtension(inBsaName, ".bsa");
                string inBSAPath = Path.Combine(dirFO3Data, inBSAFile);

                IDictionary<string, string> renameDict = null;
                { //comment this out if you don't need it, but set renameDict
                    var renDict = ReadOldDict(outBsaName, "RenameFiles.dict");
                    if (renDict != null)
                    {
                        renameDict = new Dictionary<string, string>(renDict);
                        var newRenPath = Path.Combine(BUILD_DIR, Path.ChangeExtension(outBsaName, ".ren"));
                        if (!File.Exists(newRenPath))
                            using (var stream = File.OpenWrite(newRenPath))
                                BF.Serialize(stream, renameDict);
                    }
                    else
                    {
                        renameDict = new Dictionary<string, string>();
                    }
                }
                Debug.Assert(renameDict != null);

                var patPath = Path.Combine(BUILD_DIR, Path.ChangeExtension(outBsaName, ".pat"));
                if (File.Exists(patPath))
                    continue;

                var checkDict = new Dictionary<string, PatchFixup>();

                using (var inBSA = new BSAWrapper(inBSAPath))
                using (var outBSA = new BSAWrapper(outBSAPath))
                {
                    foreach (var kvpRen in renameDict)
                    {
                        var oldFilePath = kvpRen.Key;
                        var newFilePath = kvpRen.Value;
                        Console.WriteLine();
                    }

                    var oldFiles = inBSA.SelectMany(folder => folder).ToList();
                    var newFiles = outBSA.SelectMany(folder => folder).ToList();

                    Func<BSAFile, string> keySel = file => file.Filename;
                    Func<BSAFile, string> valSel = file => GetChecksum(file.GetSaveData(true));

                    var oldChkDict = FileValidation.FromBSA(inBSA);
                    var newChkDict = FileValidation.FromBSA(outBSA);

                    var joinedPatches = from patKvp in newChkDict
                                        join oldKvp in oldChkDict on patKvp.Key equals oldKvp.Key into foundOld
                                        join bsaFile in oldFiles on patKvp.Key equals bsaFile.Filename
                                        select new
                                        {
                                            bsaFile,
                                            file = patKvp.Key,
                                            patch = patKvp.Value,
                                            oldChk = foundOld.SingleOrDefault()
                                        };

                    var OldDiff_oldChkDict = oldFiles.ToDictionary(keySel, valSel);
                    var OldDiff_newChkDict = newFiles.ToDictionary(keySel, valSel);

                    Debug.Assert(checkDict != null);
                    foreach (var join in joinedPatches)
                    {
                        if (string.IsNullOrEmpty(join.oldChk.Key))
                            Debug.Fail("File not found: " + join.file);

                        var oldChk = join.oldChk.Value;
                        var newChk = join.patch;

                        if (!newChk.Equals(oldChk))
                        {
                            var patchInfo = PatchInfo.FromFileChecksum(outBsaName, join.bsaFile.Filename, OldDiff_oldChkDict[join.file], OldDiff_newChkDict[join.file], newChk);
                            Debug.Assert(patchInfo.Data != null);

                            checkDict.Add(join.file, new PatchFixup(oldChk, patchInfo));
                            //BSADiff.PatchFile(join.bsaFile, oldChk, patchInfo, true);
                        }
                    }

                    using (var stream = File.OpenWrite(patPath))
                        BF.Serialize(stream, checkDict);
                }
            }
        }

        private static IDictionary<string, string> ReadOldDict(string outFilename, string dictName)
        {
            var dictPath = Path.Combine(BSADiff.PatchDir, outFilename, dictName);
            if (!File.Exists(dictPath))
                return null;
            using (var stream = File.OpenRead(dictPath))
                return (IDictionary<string, string>)BF.Deserialize(stream);
        }

        private static string GetChecksum(byte[] buf)
        {
            MD5 fileHash = MD5.Create();
            return BitConverter.ToString(fileHash.ComputeHash(buf)).Replace("-", "");
        }
    }
}