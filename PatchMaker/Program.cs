using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BSAsharp;
using BSAsharp.Extensions;
using Patching.Delta;
using Resources;
using SevenZip;
using TaleOfTwoWastelands;
using TaleOfTwoWastelands.Patching;
using Util = TaleOfTwoWastelands.Util;

namespace PatchMaker
{
    using Patch = Tuple<FileValidation, PatchInfo[]>;

    class Program
    {
        const string
            InDir = "BuildDB",
            OutDir = "OutDB",
            SchemaSetup = @"BEGIN TRANSACTION;
CREATE TABLE `Patches` (
	`id`	INTEGER NOT NULL UNIQUE,
	`hashId`	INTEGER NOT NULL,
	`key`	TEXT NOT NULL UNIQUE,
	PRIMARY KEY(id),
	FOREIGN KEY(`hashId`) REFERENCES Hashes_id
);
CREATE TABLE `PatchData` (
	`id`	INTEGER NOT NULL UNIQUE,
	`patchId`	INTEGER NOT NULL,
	`hashId`	INTEGER NOT NULL,
	`data`	BLOB NOT NULL,
	PRIMARY KEY(id),
	FOREIGN KEY(`patchId`) REFERENCES Patches_id,
	FOREIGN KEY(`hashId`) REFERENCES Hashes_id
);
CREATE TABLE `Hashes` (
	`id`	INTEGER NOT NULL UNIQUE,
	`type`	INTEGER NOT NULL,
	`checksum`	BLOB NOT NULL,
	PRIMARY KEY(id),
	FOREIGN KEY(`type`) REFERENCES HashTypes_id
);
CREATE TABLE `HashTypes` (
	`id`	INTEGER NOT NULL UNIQUE,
	`name`	TEXT UNIQUE,
	PRIMARY KEY(id)
);
INSERT INTO `HashTypes` (id,name) VALUES (0,'Murmur128'),(1,'Md5');
COMMIT;";

        private static string _dirTTWMain, _dirTTWOptional, _dirFO3Data;

        static void InsertHash(SQLiteConnection conn, ref int hashId, FileValidation fv)
        {
            using (var finalHashCmd = new SQLiteCommand("INSERT INTO Hashes(id, type, checksum) VALUES(@id, @type, @checksum)", conn))
            {
                finalHashCmd.Parameters.Add(new SQLiteParameter("@id", ++hashId));
                finalHashCmd.Parameters.Add(new SQLiteParameter("@type", fv.Type + 1));
                finalHashCmd.Parameters.Add(new SQLiteParameter("@checksum", fv.Checksum));

                Console.WriteLine($"Inserting: hash {hashId}");
                finalHashCmd.ExecuteNonQuery();
            }
        }

        static void Main()
        {
            //BenchmarkHash.Run();

            var path = @"C:\TTW-ME\resources\TTW Data\TTW Patches";
            foreach (var file in Directory.EnumerateFiles(path, "*.pat"))
            {
                var dbName = Path.GetFileName(Path.ChangeExtension(file, ".db3"));

                File.Delete(dbName);
                SQLiteConnection.CreateFile(dbName);
                using (var conn = new SQLiteConnection($"Data Source={dbName}"))
                {
                    conn.Open();
                    using (var schemaCmd = new SQLiteCommand(SchemaSetup, conn))
                    {
                        schemaCmd.ExecuteNonQuery();
                    }

                    Console.WriteLine($"Parsing {dbName}");
                    using (var fs = File.OpenRead(file))
                    using (var reader = new BinaryReader(fs))
                    {
                        var size = reader.ReadInt32();
                        using (var transaction = conn.BeginTransaction())
                        {
                            for (int i = 1, hashId = 0, patchId = 0, patchDataId = 0; i <= size; i++)
                            {
                                var key = reader.ReadString();
                                Console.WriteLine($"Inserting {key}");

                                var fv = FileValidation.ReadFrom(reader);

                                InsertHash(conn, ref hashId, fv);

                                using (var patchCmd = new SQLiteCommand("INSERT INTO Patches(id, hashId, key) VALUES(@id, @hashId, @key)", conn))
                                {
                                    patchCmd.Parameters.Add(new SQLiteParameter("@id", ++patchId));
                                    patchCmd.Parameters.Add(new SQLiteParameter("@hashId", hashId));
                                    patchCmd.Parameters.Add(new SQLiteParameter("@key", key));

                                    Console.WriteLine($"Inserting patch {patchId}");
                                    patchCmd.ExecuteNonQuery();
                                }

                                var patchCount = reader.ReadInt32();
                                Console.WriteLine($"{patchCount} patches to insert");
                                for (int j = 0; j < patchCount; j++)
                                {
                                    var patch = new PatchInfo(reader);
                                    unsafe
                                    {
                                        fixed (byte* pData = patch.Data)
                                        {
                                            patch.Data = MakeDiff.ConvertPatch(pData, patch.Data.LongLength, Diff.SIG_LZDIFF41,
                                                Diff.SIG_NONONONO);
                                        }
                                    }
                                    InsertHash(conn, ref hashId, patch.Metadata);
                                    using (var blobCmd = new SQLiteCommand("INSERT INTO PatchData(id, patchId, hashId, data) VALUES(@id, @patchId, @hashId, @data)", conn))
                                    {
                                        blobCmd.Parameters.Add(new SQLiteParameter("@id", ++patchDataId));
                                        blobCmd.Parameters.Add(new SQLiteParameter("@patchId", patchId));
                                        blobCmd.Parameters.Add(new SQLiteParameter("@hashId", hashId));
                                        blobCmd.Parameters.Add(new SQLiteParameter("@data", patch.Data));

                                        Console.WriteLine($"Inserting patchData {patchDataId}");
                                        blobCmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            transaction.Commit();
                        }
                    }
                }
            }
            Console.WriteLine("Done");
            Console.ReadKey();
            return;

            if (!Debugger.IsAttached)
                Debugger.Launch();

            Console.WriteLine("Building {0} folder from {1} folder. Existing files are skipped. OK?", OutDir, InDir);
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

            Directory.CreateDirectory(OutDir);

            _dirTTWMain = Path.Combine(InDir, "Main Files");
            _dirTTWOptional = Path.Combine(InDir, "Optional Files");

            var helper = new RegistryPathStore();
            var bethKey = helper.GetBethKey();

            var fo3Key = bethKey.CreateSubKey("Fallout3");
            Debug.Assert(fo3Key != null, "fo3Key != null");

            var fallout3Path = fo3Key.GetValue("Installed Path", "").ToString();
            _dirFO3Data = Path.Combine(fallout3Path, "Data");

            SevenZipCompressor.LzmaDictionarySize = 1024 * 1024 * 64; //64MiB, 7z 'Ultra'

            Parallel.ForEach(Game.BuildableBSAs, new ParallelOptions { MaxDegreeOfParallelism = 2 }, kvpBsa => BuildBsaPatch(kvpBsa.Key, kvpBsa.Value));

            var knownEsmVersions =
                Directory.EnumerateFiles(Path.Combine(InDir, "Versions"), "*.esm", SearchOption.AllDirectories)
                .ToLookup(Path.GetFileName, esm => esm);

            Parallel.ForEach(Game.CheckedESMs, new ParallelOptions { MaxDegreeOfParallelism = 2 }, esm => BuildMasterPatch(esm, knownEsmVersions));
        }

        private static void BuildBsaPatch(string inBsaName, string outBsaName)
        {
            string outBSAFile = Path.ChangeExtension(outBsaName, ".bsa");
            string outBSAPath = Path.Combine(_dirTTWMain, outBSAFile);

            string inBSAFile = Path.ChangeExtension(inBsaName, ".bsa");
            string inBSAPath = Path.Combine(_dirFO3Data, inBSAFile);

            var renameDict = BuildRenameDict(outBsaName);
            Debug.Assert(renameDict != null);

            var patPath = Path.Combine(OutDir, Path.ChangeExtension(outBsaName, ".pat"));
            if (File.Exists(patPath))
                return;

            var prefix = Path.Combine(InDir, "TTW Patches", outBsaName);

            using (var inBSA = new BSA(inBSAPath))
            using (var outBSA = new BSA(outBSAPath))
            {
                BsaDiff
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
                                        patch = patKvp.Value
                                    };
                var allJoinedPatches = joinedPatches.ToList();

                var patchDict = new PatchDict(allJoinedPatches.Count);
                foreach (var join in allJoinedPatches)
                {
                    var oldBsaFile = oldFiles.SingleOrDefault(file => file.Filename == join.file);
                    Debug.Assert(oldBsaFile != null, "File not found: " + join.file);

                    var oldChk = FileValidation.FromBSAFile(oldBsaFile);
                    var newChk = join.patch;

                    var oldFilename = oldBsaFile.Filename;
                    if (oldFilename.StartsWith(TaleOfTwoWastelands.Properties.Resources.VoicePrefix))
                    {
                        patchDict.Add(join.file, new Patch(newChk, null));
                        continue;
                    }

                    var patches = new List<PatchInfo>();

                    var md5OldStr = Util.GetMD5String(oldBsaFile.GetContents(true));
                    var md5NewStr = Util.GetMD5String(join.newBsaFile.GetContents(true));

                    var diffPath = Path.Combine(prefix, oldFilename + "." + md5OldStr + "." + md5NewStr + ".diff");
                    var usedPath = Path.ChangeExtension(diffPath, ".used");
                    if (File.Exists(usedPath))
                        File.Move(usedPath, diffPath); //fixes moronic things

                    var altDiffs = Util.FindAlternateVersions(diffPath);
                    if (altDiffs != null)
                    {
                        foreach (var altDiff in altDiffs)
                        {
                            var altDiffBytes = GetDiff(altDiff.Item1, Diff.SIG_LZDIFF41);
                            patches.Add(new PatchInfo
                            {
                                Metadata = new FileValidation(altDiff.Item2, 0, FileValidation.ChecksumType.Md5),
                                Data = altDiffBytes
                            });
                        }
                    }

                    if (newChk != oldChk)
                    {
                        byte[] diffData = GetDiff(diffPath, Diff.SIG_LZDIFF41);

                        var patchInfo = PatchInfo.FromOldDiff(diffData, oldChk);
                        Debug.Assert(patchInfo.Data != null);

                        patches.Add(patchInfo);
                    }

                    patchDict.Add(join.file, new Patch(newChk, patches.ToArray()));
                }

                using (var stream = File.OpenWrite(patPath))
                    patchDict.WriteAll(stream);
            }
        }

        static unsafe byte[] GetDiff(string diffPath, long convertSignature = -1, bool moveToUsed = false)
        {
            if (File.Exists(diffPath))
            {
                try
                {
                    var diffBytes = File.ReadAllBytes(diffPath);
                    if (convertSignature > 0)
                        fixed (byte* pBz2 = diffBytes)
                            return MakeDiff.ConvertPatch(pBz2, diffBytes.Length, Diff.SIG_BSDIFF40, convertSignature);

                    return diffBytes;
                }
                finally
                {
                    if (moveToUsed)
                        File.Move(diffPath, Path.ChangeExtension(diffPath, ".used"));
                }
            }

            return null;
        }

        private static IDictionary<string, string> BuildRenameDict(string bsaName)
        {
            var dictPath = Path.Combine(InDir, "TTW Patches", bsaName, "RenameFiles.dict");
            if (File.Exists(dictPath))
            {
                var renameDict = Util.ReadOldDatabase(dictPath);
                var newRenPath = Path.Combine(OutDir, Path.ChangeExtension(bsaName, ".ren"));
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

        private static void BuildMasterPatch(string esm, ILookup<string, string> knownEsmVersions)
        {
            var fixPath = Path.Combine(OutDir, Path.ChangeExtension(esm, ".pat"));
            if (File.Exists(fixPath))
                return;

            var ttwESM = Path.Combine(_dirTTWMain, esm);
            var ttwBytes = File.ReadAllBytes(ttwESM);
            var ttwChk = new FileValidation(ttwBytes);

            var altVersions = knownEsmVersions[esm].ToList();

            var patches =
                altVersions.Select(dataESM =>
                {
                    var dataBytes = File.ReadAllBytes(dataESM);
                    byte[] patchBytes;

                    using (var msPatch = new MemoryStream())
                    {
                        MakeDiff.Create(dataBytes, ttwBytes, Diff.SIG_LZDIFF41, msPatch);
                        patchBytes = msPatch.ToArray();
                    }

                    return new PatchInfo
                    {
                        Metadata = new FileValidation(dataESM),
                        Data = patchBytes
                    };
                })
                .AsParallel()
                .ToArray();

            var patchDict = new PatchDict(altVersions.Count);
            patchDict.Add(esm, new Patch(ttwChk, patches));

            using (var fixStream = File.OpenWrite(fixPath))
                patchDict.WriteAll(fixStream);
        }
    }
}
