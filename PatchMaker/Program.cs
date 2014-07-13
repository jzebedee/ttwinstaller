using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Threading;
using BSAsharp;
using TaleOfTwoWastelands;
using TaleOfTwoWastelands.Patching;
using TaleOfTwoWastelands.ProgressTypes;
namespace PatchMaker
{
    class Program
    {
        const string SOURCE_DIR = "BuildDB";
        const string BUILD_DIR = "OutDB";

        static void Main(string[] args)
        {
            Console.WriteLine("Building {0} folder from {1} folder. Existing files are skipped. OK?", BUILD_DIR, SOURCE_DIR);
            Console.Write("y/n: ");
            var keyInfo = Console.ReadKey();
            switch (keyInfo.Key)
            {
                case ConsoleKey.Y:
                    Console.WriteLine();
                    break;
                case ConsoleKey.N:
                default:
                    return;
            }

            Directory.CreateDirectory(BUILD_DIR);

            var dirTTWMain = Path.Combine(SOURCE_DIR, Installer.MainDir);
            var dirTTWOptional = Path.Combine(SOURCE_DIR, Installer.OptDir);

            var bethKey = Installer.GetBethKey();

            var fo3Key = bethKey.CreateSubKey("Fallout3");
            var Fallout3Path = fo3Key.GetValue("Installed Path", "").ToString();
            var dirFO3Data = Path.Combine(Fallout3Path, "Data");

            var knownEsmVersions =
                Directory.EnumerateFiles(Path.Combine(SOURCE_DIR, "Versions"), "*.esm", SearchOption.AllDirectories)
                .ToLookup(esm => Path.GetFileName(esm), esm => esm);

            foreach (var ESM in Installer.CheckedESMs)
            {
                var fixPath = Path.Combine(BUILD_DIR, ESM + ".pat");
                if (!File.Exists(fixPath))
                {
                    var ttwESM = Path.Combine(dirTTWMain, ESM);
                    var ttwBytes = File.ReadAllBytes(ttwESM);
                    foreach (var dataESM in knownEsmVersions[ESM]) //var dataESM = Path.Combine(dirFO3Data, ESM);
                    {
                        var dataBytes = File.ReadAllBytes(dataESM);
                        byte[] patchBytes;

                        using (var msPatch = new MemoryStream())
                        {
                            BinaryPatchUtility.Create(dataBytes, ttwBytes, msPatch);
                            patchBytes = msPatch.ToArray();
                        }

                        var patch = new PatchInfo
                        {
                            Metadata = FileValidation.FromFile(dataESM),
                            Data = patchBytes
                        };

                        using (var fixStream = File.OpenWrite(fixPath))
                        using (var writer = new BinaryWriter(fixStream))
                            patch.WriteTo(writer);
                    }
                }
            }

            var progressLog = new System.Progress<string>(s => Debug.Write(s));
            var progressUIMinor = new System.Progress<InstallOperation>();
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
                            using (var writer = new BinaryWriter(stream))
                                foreach (var kvp in renameDict)
                                {
                                    writer.Write(kvp.Key);
                                    writer.Write(kvp.Value);
                                }
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

                using (var inBSA = new BSAWrapper(inBSAPath))
                using (var outBSA = new BSAWrapper(outBSAPath))
                {
                    {
                        var renameGroup = from folder in inBSA
                                          from file in folder
                                          join kvp in renameDict on file.Filename equals kvp.Value
                                          let a = new { folder, file, kvp }
                                          select a;

                        var renameCopies = from g in renameGroup
                                           let newFilename = g.kvp.Key
                                           let newDirectory = Path.GetDirectoryName(newFilename)
                                           let a = new { g.folder, g.file, newFilename }
                                           group a by newDirectory into outs
                                           select outs;

                        var newBsaFolders = from g in renameCopies
                                            let folderAdded = inBSA.Add(new BSAFolder(g.Key))
                                            select g;
                        newBsaFolders.ToList();

                        var renameFixes = from g in newBsaFolders
                                          from a in g
                                          join newFolder in inBSA on g.Key equals newFolder.Path
                                          let newFile = a.file.DeepCopy(g.Key, Path.GetFileName(a.newFilename))
                                          let addedFile = newFolder.Add(newFile)
                                          let cleanedDict = renameDict.Remove(a.newFilename)
                                          select new { a.folder, a.file, newFolder, newFile, a.newFilename };
                        renameFixes.ToList(); // execute query
                    }

                    var oldFiles = inBSA.SelectMany(folder => folder).ToList();
                    var newFiles = outBSA.SelectMany(folder => folder).ToList();

                    var oldChkDict = FileValidation.FromBSA(inBSA);
                    var newChkDict = FileValidation.FromBSA(outBSA);

                    var joinedPatches = from patKvp in newChkDict
                                        join oldKvp in oldChkDict on patKvp.Key equals oldKvp.Key into foundOld
                                        join oldBsaFile in oldFiles on patKvp.Key equals oldBsaFile.Filename
                                        join newBsaFile in newFiles on patKvp.Key equals newBsaFile.Filename
                                        select new
                                        {
                                            oldBsaFile,
                                            newBsaFile,
                                            file = patKvp.Key,
                                            patch = patKvp.Value,
                                            oldChk = foundOld.SingleOrDefault()
                                        };
                    var allJoinedPatches = joinedPatches.ToList();

                    var patchDict = new PatchDict(allJoinedPatches.Count);
                    foreach (var join in allJoinedPatches)
                    {
                        if (string.IsNullOrEmpty(join.oldChk.Key))
                            Debug.Fail("File not found: " + join.file);

                        var oldFilename = join.oldBsaFile.Filename;
                        if (oldFilename.StartsWith(BSADiff.VOICE_PREFIX))
                        {
                            patchDict.Add(join.file, new PatchInfo());
                            continue;
                        }

                        var oldChkLazy = join.oldChk.Value;
                        var newChkLazy = join.patch;

                        var oldChk = oldChkLazy;
                        var newChk = newChkLazy;

                        PatchInfo patchInfo;
                        if (!newChk.Equals(oldChk))
                        {
                            var antiqueOldChk = Util.GetMD5(join.oldBsaFile.GetContents(true));
                            var antiqueNewChk = Util.GetMD5(join.newBsaFile.GetContents(true));

                            patchInfo = PatchInfo.FromFileChecksum(outBsaName, oldFilename, antiqueOldChk, antiqueNewChk, newChk);
                            Debug.Assert(patchInfo.Data != null);
                        }
                        else
                            //without this, we will generate sparse (patch-only) fixups
                            patchInfo = new PatchInfo { Metadata = newChk };
                        patchDict.Add(join.file, patchInfo);
                    }

                    using (var stream = File.OpenWrite(patPath))
                        patchDict.WriteAll(stream);
                }
            }
        }

        //Shameless code duplication. So sue me.
        private static IDictionary<string, string> ReadOldDict(string outFilename, string dictName)
        {
            var dictPath = Path.Combine(BSADiff.PatchDir, outFilename, dictName);
            if (!File.Exists(dictPath))
                return null;
            using (var stream = File.OpenRead(dictPath))
                return (IDictionary<string, string>)new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter().Deserialize(stream);
        }
    }
}
