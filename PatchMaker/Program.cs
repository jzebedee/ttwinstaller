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
using SevenZip;
using System.Threading.Tasks;
using System.Collections.Concurrent;
namespace PatchMaker
{
    class Program
    {
        const string IN_DIR = "BuildDB";
        const string OUT_DIR = "OutDB";

        static void Main(string[] args)
        {
            //BenchmarkHash.Run();

            Console.WriteLine("Building {0} folder from {1} folder. Existing files are skipped. OK?", OUT_DIR, IN_DIR);
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

            Directory.CreateDirectory(OUT_DIR);

            var dirTTWMain = Path.Combine(IN_DIR, Installer.MainDir);
            var dirTTWOptional = Path.Combine(IN_DIR, Installer.OptDir);

            var bethKey = Installer.GetBethKey();

            var fo3Key = bethKey.CreateSubKey("Fallout3");
            var Fallout3Path = fo3Key.GetValue("Installed Path", "").ToString();
            var dirFO3Data = Path.Combine(Fallout3Path, "Data");

            var knownEsmVersions =
                Directory.EnumerateFiles(Path.Combine(IN_DIR, "Versions"), "*.esm", SearchOption.AllDirectories)
                .ToLookup(esm => Path.GetFileName(esm), esm => esm);

            Parallel.ForEach(Installer.CheckedESMs, ESM =>
            {
                var fixPath = Path.Combine(OUT_DIR, Path.ChangeExtension(ESM, ".pat"));
                if (File.Exists(fixPath))
                    return;

                var ttwESM = Path.Combine(dirTTWMain, ESM);
                var ttwBytes = File.ReadAllBytes(ttwESM);

                var altVersions = knownEsmVersions[ESM].ToList();

                var patches =
                    altVersions.Select(dataESM =>
                    {
                        var dataBytes = File.ReadAllBytes(dataESM);
                        byte[] patchBytes;

                        using (var msPatch = new MemoryStream())
                        {
                            BinaryPatchUtility.Create(dataBytes, ttwBytes, msPatch);
                            patchBytes = msPatch.ToArray();
                        }

                        return new PatchInfo
                        {
                            Metadata = FileValidation.FromFile(dataESM),
                            Data = patchBytes
                        };
                    })
                    .AsParallel()
                    .ToArray();

                var patchDict = new PatchDict(altVersions.Count);
                patchDict.Add(ESM, patches);

                using (var fixStream = File.OpenWrite(fixPath))
                    patchDict.WriteAll(fixStream);
            });

            SevenZipCompressor.LzmaDictionarySize = 1024 * 1024 * 64; //64MiB, 7z 'Ultra'

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
                        var newRenPath = Path.Combine(OUT_DIR, Path.ChangeExtension(outBsaName, ".ren"));
                        if (!File.Exists(newRenPath))
                            using (var fileStream = File.OpenWrite(newRenPath))
                            using (var lzmaStream = new LzmaEncodeStream(fileStream))
                            using (var writer = new BinaryWriter(lzmaStream))
                            {
                                writer.Write(renameDict.Count);
                                foreach (var kvp in renameDict)
                                {
                                    writer.Write(kvp.Key);
                                    writer.Write(kvp.Value);
                                }
                            }
                    }
                    else
                    {
                        renameDict = new Dictionary<string, string>();
                    }
                }
                Debug.Assert(renameDict != null);

                var patPath = Path.Combine(OUT_DIR, Path.ChangeExtension(outBsaName, ".pat"));
                if (File.Exists(patPath))
                    continue;

                var prefix = Path.Combine(IN_DIR, "TTW Patches", outBsaName);

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

                        var oldChk = join.oldChk.Value;
                        var newChk = join.patch;

                        PatchInfo patchInfo;
                        if (newChk != oldChk)
                        {
                            var newBytes = join.newBsaFile.GetContents(true);

                            var antiqueOldChk = Util.GetMD5(join.oldBsaFile.GetContents(true));
                            var antiqueNewChk = Util.GetMD5(newBytes);

                            patchInfo = PatchInfo.FromFileChecksum(prefix, oldFilename, antiqueOldChk, antiqueNewChk, newChk);
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

        private static IEnumerable<string> FindAlternateVersions(string file)
        {
            var justName = Path.GetFileName(file);
            justName = justName.Substring(0, justName.IndexOf('.', justName.IndexOf('.') + 1));

            var justDir = Path.GetDirectoryName(file);

            return Directory.EnumerateFiles(justDir, justName + "*.diff").Where(other => other != file);
        }

        //Shameless code duplication. So sue me.
        private static IDictionary<string, string> ReadOldDict(string outFilename, string dictName)
        {
            var dictPath = Path.Combine(IN_DIR, "TTW Patches", outFilename, dictName);
            if (!File.Exists(dictPath))
                return null;
            using (var stream = File.OpenRead(dictPath))
                return (IDictionary<string, string>)new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter().Deserialize(stream);
        }
    }
}
