//#define PARALLEL
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using BSAsharp;
using System.IO.MemoryMappedFiles;
using TaleOfTwoWastelands.ProgressTypes;
using SevenZip;
using Patch = System.Tuple<TaleOfTwoWastelands.Patching.FileValidation, TaleOfTwoWastelands.Patching.PatchInfo[]>;

namespace TaleOfTwoWastelands.Patching
{
    using PatchJoin = Tuple<BSAFile, BSAFile, Patch>;
    public class BSADiff
    {
        public static readonly string PatchDir = Path.Combine(Installer.AssetsDir, "TTW Data", "TTW Patches");

        protected IProgress<string> ProgressDual { get; set; }
        protected IProgress<string> ProgressFile { get; set; }
        protected IProgress<InstallOperation> ProgressMinorUI { get; set; }
        protected CancellationToken Token { get; set; }
        protected InstallOperation Op { get; set; }

        private void Log(string msg)
        {
            ProgressDual.Report('\t' + msg);
        }

        private void LogFile(string msg)
        {
            ProgressFile.Report('\t' + msg);
        }

        public BSADiff(Installer parent, CancellationToken Token)
        {
            this.ProgressDual = parent.ProgressDual;
            this.ProgressFile = parent.ProgressFile;
            this.ProgressMinorUI = parent.ProgressMinorOperation;
            this.Token = Token;
        }

        public bool PatchBSA(CompressionOptions bsaOptions, string oldBSA, string newBSA, bool simulate = false)
        {
            Op = new InstallOperation(ProgressMinorUI, Token) { ItemsTotal = 7 };

            var outBsaFilename = Path.GetFileNameWithoutExtension(newBSA);

            BSAWrapper BSA;
            try
            {
                Op.CurrentOperation = "Opening " + Path.GetFileName(oldBSA);

                BSA = new BSAWrapper(oldBSA, bsaOptions);
            }
            finally
            {
                Op.Step();
            }

            IDictionary<string, string> renameDict;
            try
            {
                Op.CurrentOperation = "Opening rename database";

                var renamePath = Path.Combine(PatchDir, Path.ChangeExtension(outBsaFilename, ".ren"));
                if (File.Exists(renamePath))
                    using (var fileStream = File.OpenRead(renamePath))
                    using (var lzmaStream = new LzmaDecodeStream(fileStream))
                    using (var reader = new BinaryReader(lzmaStream))
                    {
                        var numPairs = reader.ReadInt32();
                        renameDict = new Dictionary<string, string>(numPairs);

                        while (numPairs-- > 0)
                            renameDict.Add(reader.ReadString(), reader.ReadString());
                    }
                else
                    renameDict = new Dictionary<string, string>();
            }
            finally
            {
                Op.Step();
            }

            PatchDict patchDict;
            try
            {
                Op.CurrentOperation = "Opening patch database";

                var patchPath = Path.Combine(PatchDir, Path.ChangeExtension(outBsaFilename, ".pat"));
                if (File.Exists(patchPath))
                {
                    patchDict = new PatchDict(patchPath);
                }
                else
                {
                    Log("\tNo patch database is available for: " + oldBSA);
                    return false;
                }
            }
            finally
            {
                Op.Step();
            }

            using (BSA)
            {
                try
                {
                    RenameFiles(BSA, renameDict);

                    if (renameDict.Count > 0)
                    {
                        foreach (var kvp in renameDict)
                        {
                            Log("File not found: " + kvp.Value);
                            Log("\tCannot create: " + kvp.Key);
                        }
                    }
                }
                finally
                {
                    Op.Step();
                }

                var allFiles = BSA.SelectMany(folder => folder).ToList();
                try
                {
                    var opChk = new InstallOperation(ProgressMinorUI, Token);

                    opChk.ItemsTotal = patchDict.Count;

                    var joinedPatches = from patKvp in patchDict
                                        //if the join is not grouped, this will exclude missing files, and we can't find and fail on them
                                        join oldFile in allFiles on patKvp.Key equals oldFile.Filename into foundOld
                                        join bsaFile in allFiles on patKvp.Key equals bsaFile.Filename
                                        select new PatchJoin(bsaFile, foundOld.SingleOrDefault(), patKvp.Value);

#if DEBUG
                    var watch = new Stopwatch();
                    try
                    {
                        watch.Start();
#endif
#if PARALLEL
                        Parallel.ForEach(joinedPatches, join =>
#else
                        foreach (var join in joinedPatches)
#endif
                            HandleFile(opChk, join, patchDict)
#if PARALLEL
)
#endif
;
#if DEBUG
                    }
                    finally
                    {
                        watch.Stop();
                        Debug.WriteLine(outBsaFilename + " HandleFile loop finished in " + watch.Elapsed);
                    }
#endif
                }
                finally
                {
                    Op.Step();
                }

                try
                {
                    Op.CurrentOperation = "Removing unnecessary files";

                    var notIncluded = allFiles.Where(file => !patchDict.ContainsKey(file.Filename));
                    var filesToRemove = new HashSet<BSAFile>(notIncluded);

                    var filesRemoved = BSA.Sum(folder => folder.RemoveWhere(bsafile => filesToRemove.Contains(bsafile)));
                    BSA.RemoveWhere(folder => folder.Count == 0);
                }
                finally
                {
                    Op.Step();
                }

                try
                {
                    Op.CurrentOperation = "Saving " + Path.GetFileName(newBSA);

                    if (!simulate)
                        BSA.Save(newBSA.ToLowerInvariant());
                }
                finally
                {
                    Op.Step();
                }
            }

            Op.Finish();

            return true;
        }

        private void HandleFile(InstallOperation opChk, PatchJoin join, PatchDict patchDict)
        {
            try
            {
                var newFile = join.Item1;
                var oldFile = join.Item2;

                var filepath = newFile.Filename;
                var filename = newFile.Name;

                if (oldFile == null)
                {
                    Log("ERROR: File not found: " + filepath);
                    return;
                }

                var patchTuple = join.Item3;
                var newChk = patchTuple.Item1;
                var patches = patchTuple.Item2;

                if (newChk == null && (patches == null || patches.Length == 0))
                {
                    opChk.CurrentOperation = "Skipping " + filename;
                    LogFile("Skipping empty patch for " + filepath);
                    return;
                }

                using (var curChk = RecreateChkType(oldFile, newChk.Type))
                    if (newChk == curChk)
                    {
                        opChk.CurrentOperation = "Compressing " + filename;
                        newFile.Cache();
                    }
                    else
                    {
                        //YOUR HANDY GUIDEBOOK FOR STRANGE CHECKSUM ACRONYMS!
                        //newChk - the checksum for the expected final result (after patching)
                        //oldChk - the checksum for the original file a diff is built against
                        //curChk - the checksum for the current file being compared or patched
                        //testChk- the checksum for the current file, in the format of oldChk
                        //patChk - the checksum for the current file, after patching
                        foreach (var patchInfo in patches)
                        {
                            var oldChk = patchInfo.Metadata;

                            if (curChk.Type != oldChk.Type)
                            {
                                using (var testChk = RecreateChkType(oldFile, oldChk.Type))
                                    if (oldChk != testChk)
                                        //this is a patch for a different original
                                        continue;
                            }
                            else if (oldChk != curChk)
                                //this is a patch for a different original
                                continue;

                            //patch is for this original
                            opChk.CurrentOperation = "Patching " + filename;

                            if (PatchFile(newFile, patchInfo, newChk))
                                return;
                            else
                                Log("ERROR: Patching " + filepath + " failed");
                        }

                        using (var patChk = RecreateChkType(newFile, newChk.Type))
                            if (newChk != patChk)
                            {
                                //no patch exists for the file
                                Log("WARNING: File is of an unexpected version: " + filename + " - " + curChk);
                                Log("This file cannot be patched. Errors may occur.");
                            }
                    }
            }
            finally
            {
                opChk.Step();
            }
        }

        private static FileValidation RecreateChkType(BSAFile file, FileValidation.ChecksumType compareType)
        {
            switch (compareType)
            {
                case FileValidation.ChecksumType.Murmur128:
                    return FileValidation.FromBSAFile(file);
                case FileValidation.ChecksumType.Md5:
                    return FileValidation.FromMd5(Util.GetMD5(file.GetContents(true)));
                default:
                    throw new Exception("Unknown hash method in patch: " + compareType + "!");
            }
        }

        public static IEnumerable<Tuple<string, string, string>> CreateRenameQuery(BSAWrapper BSA, IDictionary<string, string> renameDict)
        {
            var renameGroup = from folder in BSA
                              from file in folder
                              join kvp in renameDict on file.Filename equals kvp.Value
                              let a = new { folder, file, kvp }
                              //group a by kvp.Value into g
                              select a;

            var renameCopies = from g in renameGroup
                               let newFilename = g.kvp.Key
                               let newDirectory = Path.GetDirectoryName(newFilename)
                               let a = new { g.folder, g.file, newFilename }
                               group a by newDirectory into outs
                               select outs;

            var newBsaFolders = renameCopies.ToList();
            newBsaFolders.ForEach(g => BSA.Add(new BSAFolder(g.Key)));

            return from g in newBsaFolders
                   from a in g
                   join newFolder in BSA on g.Key equals newFolder.Path
                   let newFile = a.file.DeepCopy(g.Key, Path.GetFileName(a.newFilename))
                   let addedFile = newFolder.Add(newFile)
                   select Tuple.Create(a.file.Name, newFile.Name, a.newFilename);
        }

        public void RenameFiles(BSAWrapper BSA, IDictionary<string, string> renameDict)
        {
            var opPrefix = "Renaming BSA files";

            var opRename = new InstallOperation(ProgressMinorUI, Token);
            opRename.CurrentOperation = opPrefix;

            var renameFixes = CreateRenameQuery(BSA, renameDict);
            opRename.ItemsTotal = renameDict.Count;

#if PARALLEL
            Parallel.ForEach(renameFixes, a =>
#else
            foreach (var a in renameFixes)
#endif
            {
                renameDict.Remove(a.Item3);

                opRename.CurrentOperation = opPrefix + ": " + a.Item1 + " -> " + a.Item2;
                opRename.Step();
            }
#if PARALLEL
)
#endif
            ;
        }

        public bool PatchFile(BSAFile bsaFile, PatchInfo patch, FileValidation targetChk, bool failFast = false)
        {
            bool perfect = true;

            //InflaterInputStream won't let the patcher seek it,
            //so we have to perform a new allocate-and-copy
            var inputBytes = bsaFile.GetContents(true);

            using (var output = new MemoryStream())
            {
                unsafe
                {
                    fixed (byte* pInput = inputBytes)
                    fixed (byte* pPatch = patch.Data)
                        BinaryPatchUtility.Apply(pInput, inputBytes.Length, pPatch, patch.Data.Length, output);
                }

                output.Seek(0, SeekOrigin.Begin);
                using (var testChk = new FileValidation(output))
                {
                    if (targetChk == testChk)
                        bsaFile.UpdateData(output.ToArray(), false);
                    else
                    {
                        var err = "ERROR: Patching " + bsaFile.Filename + " has failed - " + testChk;
                        if (failFast)
                            Trace.Fail(err);
                        else
                        {
                            perfect = false;
                            LogFile(err);
                        }
                    }
                }
            }

            return perfect;
        }
    }
}
