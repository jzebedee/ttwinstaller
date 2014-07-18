using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using TaleOfTwoWastelands;
using TaleOfTwoWastelands.Patching;
using SevenZip;
using BSAsharp;

namespace PatchMaker
{
    using Patch = Tuple<FileValidation, PatchInfo[]>;

    class Program
    {
        const string
            IN_DIR = "BuildDB",
            OUT_DIR = "OutDB",
            VOICE_PREFIX = @"sound\voice";

        private static string dirTTWMain, dirTTWOptional, dirFO3Data;

        static void Main(string[] args)
        {
            if (!Debugger.IsAttached)
                Debugger.Launch();

            //BenchmarkHash.Run();

            Console.WriteLine("Building {0} folder from {1} folder. Existing files are skipped. OK?", OUT_DIR, IN_DIR);
            Console.Write("y/n: ");
            var keyInfo = Console.ReadKey();
            switch (keyInfo.Key)
            {
                case ConsoleKey.Y:
                    Console.WriteLine();
                    break;
                default:
                    return;
            }

            Directory.CreateDirectory(OUT_DIR);

            dirTTWMain = Path.Combine(IN_DIR, Installer.MainDir);
            dirTTWOptional = Path.Combine(IN_DIR, Installer.OptDir);

            var bethKey = Installer.GetBethKey();

            var fo3Key = bethKey.CreateSubKey("Fallout3");
            var Fallout3Path = fo3Key.GetValue("Installed Path", "").ToString();
            dirFO3Data = Path.Combine(Fallout3Path, "Data");

            SevenZipCompressor.LzmaDictionarySize = 1024 * 1024 * 64; //64MiB, 7z 'Ultra'

            Parallel.ForEach(Installer.BuildableBSAs, kvpBsa => BuildBsaPatch(kvpBsa.Key, kvpBsa.Value));

            var knownEsmVersions =
                Directory.EnumerateFiles(Path.Combine(IN_DIR, "Versions"), "*.esm", SearchOption.AllDirectories)
                .ToLookup(esm => Path.GetFileName(esm), esm => esm);

            Parallel.ForEach(Installer.CheckedESMs, ESM => BuildMasterPatch(ESM, knownEsmVersions));
        }

        private static void BuildBsaPatch(string inBsaName, string outBsaName)
        {
            string outBSAFile = Path.ChangeExtension(outBsaName, ".bsa");
            string outBSAPath = Path.Combine(dirTTWMain, outBSAFile);

            string inBSAFile = Path.ChangeExtension(inBsaName, ".bsa");
            string inBSAPath = Path.Combine(dirFO3Data, inBSAFile);

            var renameDict = BuildRenameDict(outBsaName);
            Debug.Assert(renameDict != null);

            var patPath = Path.Combine(OUT_DIR, Path.ChangeExtension(outBsaName, ".pat"));
            if (File.Exists(patPath))
                return;

            var prefix = Path.Combine(IN_DIR, "TTW Patches", outBsaName);

            using (var inBSA = new BSAWrapper(inBSAPath))
            using (var outBSA = new BSAWrapper(outBSAPath))
            {
                BSADiff
                    .CreateRenameQuery(inBSA, renameDict)
                    .ToList(); // execute query

                var oldFiles = inBSA.SelectMany(folder => folder).ToList();
                var newFiles = outBSA.SelectMany(folder => folder).ToList();

                var newChkDict = FileValidation.FromBSA(outBSA);

                var joinedPatches = from patKvp in newChkDict
                                    join newBsaFile in newFiles on patKvp.Key equals newBsaFile.Filename
                                    select new
                                    {
                                        newBsaFile,
                                        file = patKvp.Key,
                                        patch = patKvp.Value,
                                    };
                var allJoinedPatches = joinedPatches.ToList();

                var patchDict = new PatchDict(allJoinedPatches.Count);
                foreach (var join in allJoinedPatches)
                {
                    //var oldChkDict = FileValidation.FromBSA(inBSA);
                    //                 join oldKvp in oldChkDict on patKvp.Key equals oldKvp.Key into foundOld
                    //                  oldChk = foundOld.SingleOrDefault()
                    var oldBsaFile = oldFiles.SingleOrDefault(file => file.Filename == join.file);
                    Debug.Assert(oldBsaFile != null, "File not found: " + join.file);

                    var oldChk = FileValidation.FromBSAFile(oldBsaFile);
                    var newChk = join.patch;

                    var oldFilename = oldBsaFile.Filename;
                    if (oldFilename.StartsWith(VOICE_PREFIX))
                    {
                        patchDict.Add(join.file, new Patch(/*newChk*/null, null));
                        continue;
                    }

                    var patches = new List<PatchInfo>();
                    if (newChk != oldChk)
                    {
                        var md5OldChk = Util.GetMD5(oldBsaFile.GetContents(true));
                        var md5NewChk = Util.GetMD5(join.newBsaFile.GetContents(true));

                        var diffPath = Path.Combine(prefix, oldFilename + "." + ToWrongFormat(md5OldChk) + "." + ToWrongFormat(md5NewChk) + ".diff");
                        var usedPath = Path.ChangeExtension(diffPath, ".used");
                        if (File.Exists(usedPath))
                            File.Move(usedPath, diffPath); //fixes moronic things

                        var altDiffs = FindAlternateVersions(diffPath).ToList();
                        foreach (var altDiff in altDiffs)
                        {
                            var altDiffBytes = PatchInfo.GetDiff(altDiff, BinaryPatchUtility.SIG_LZDIFF41);
                            patches.Add(new PatchInfo
                            {
                                Metadata = FileValidation.FromMd5(md5OldChk),
                                Data = altDiffBytes
                            });
                        }

                        var patchInfo = PatchInfo.FromOldChecksum(diffPath, oldChk);
                        Debug.Assert(patchInfo.Data != null);

                        patches.Add(patchInfo);
                    }

                    patchDict.Add(join.file, new Patch(newChk, patches.ToArray()));
                }

                using (var stream = File.OpenWrite(patPath))
                    patchDict.WriteAll(stream);
            }
        }

        //antique strs are in wrong endianness, so we can't use MakeMD5String
        private static readonly byte[] Terminator = { 0 };
        public static string ToWrongFormat(BigInteger hash)
        {
            Debug.Assert(hash != BigInteger.Zero);

            var bytes = hash.ToByteArray();
            if (bytes.Length == 17 && bytes.Last() == 0)
            {
                bytes = bytes.Take(16).ToArray();
            }
            else if (bytes.Length < 16)
            {
                bytes = bytes.Concat(Terminator).ToArray();
            }

            return BitConverter.ToString(bytes).Replace("-", "");
        }

        private static IDictionary<string, string> BuildRenameDict(string bsaName)
        {
            var renDict = ReadOldDict(bsaName, "RenameFiles.dict");
            if (renDict != null)
            {
                var renameDict = new Dictionary<string, string>(renDict);
                var newRenPath = Path.Combine(OUT_DIR, Path.ChangeExtension(bsaName, ".ren"));
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

                return renameDict;
            }

            return new Dictionary<string, string>();
        }

        private static void BuildMasterPatch(string ESM, ILookup<string, string> knownEsmVersions)
        {
            var fixPath = Path.Combine(OUT_DIR, Path.ChangeExtension(ESM, ".pat"));
            if (File.Exists(fixPath))
                return;

            var ttwESM = Path.Combine(dirTTWMain, ESM);
            var ttwBytes = File.ReadAllBytes(ttwESM);
            var ttwChk = new FileValidation(ttwBytes);

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
            patchDict.Add(ESM, new Patch(ttwChk, patches));

            using (var fixStream = File.OpenWrite(fixPath))
                patchDict.WriteAll(fixStream);
        }

        private static IEnumerable<string> FindAlternateVersions(string file)
        {
            var justName = Path.GetFileName(file);
            var split = justName.Split('.');
            split[split.Length - 3] = "*";
            //combatshotgun.nif.8154C65E957F6A29B36ADA24CFBC1FDE.1389525E123CD0F8CD5BB47EF5FD1901.diff
            //end[0] = diff, end[1] = newChk, end[2] = oldChk, end[3 ...] = fileName

            var justDir = Path.GetDirectoryName(file);

            return Directory.EnumerateFiles(justDir, string.Join(".", split)).Where(other => other != file);
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
