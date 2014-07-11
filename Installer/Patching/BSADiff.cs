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

namespace TaleOfTwoWastelands.Patching
{
    public class BSADiff
    {
        class PatchJoin
        {
            public PatchJoin(BSAFile newFile, BSAFile oldFile, PatchInfo[] patches)
            {
                this.newFile = newFile;
                this.oldFile = oldFile;
                this.patches = patches;
            }

            public BSAFile newFile;
            public BSAFile oldFile;
            public PatchInfo[] patches;
        }

        public const string VOICE_PREFIX = @"sound\voice";
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

            var renameDict = new Dictionary<string, string>();
            try
            {
                Op.CurrentOperation = "Opening rename database";

                var renamePath = Path.Combine(PatchDir, Path.ChangeExtension(outBsaFilename, ".ren"));
                if (File.Exists(renamePath))
                    using (var stream = File.OpenRead(renamePath))
                    using (var reader = new BinaryReader(stream))
                        while (stream.Position < stream.Length)
                            renameDict.Add(reader.ReadString(), reader.ReadString());
            }
            finally
            {
                Op.Step();
            }

            IDictionary<string, PatchInfo[]> patchDict;
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
                            HandleFile(opChk, join)
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

        private void HandleFile(InstallOperation opChk, PatchJoin join)
        {
            try
            {
                var filepath = join.newFile.Filename;
                var filename = join.newFile.Name;

                if (join.oldFile == null)
                {
                    Log("ERROR: File not found: " + filepath);
                    return;
                }

                foreach (var patchInfo in join.patches)
                {
                    var newChk = patchInfo.Metadata;
                    if (FileValidation.IsEmpty(newChk) && patchInfo.Data.Length == 0)
                    {
                        opChk.CurrentOperation = "Skipping " + filename;

                        if (join.newFile.Filename.StartsWith(VOICE_PREFIX))
                        {
                            //LogFile("Skipping voice file " + filepath);
                            continue;
                        }
                        else
                        {
                            var msg = "Empty patch for file " + filepath;
                            if (newChk == null)
                                Log("ERROR: " + msg);
                            else
                                LogFile(msg);
                            continue;
                        }
                    }

                    using (var oldChk = FileValidation.FromBSAFile(join.oldFile))
                    {
                        if (!newChk.Equals(oldChk))
                        {
                            opChk.CurrentOperation = "Patching " + filename;

                            if (!PatchFile(join.newFile, oldChk, patchInfo))
                                Log("ERROR: Patching " + join.newFile.Filename + " failed");
                        }
                        else
                        {
                            opChk.CurrentOperation = "Compressing " + filename;
                            join.newFile.Cache();
                        }
                    }
                }
            }
            finally
            {
                opChk.Step();
            }
        }

        public void RenameFiles(BSAWrapper BSA, Dictionary<string, string> renameDict)
        {
            var opPrefix = "Renaming BSA files";

            var opRename = new InstallOperation(ProgressMinorUI, Token);
            opRename.CurrentOperation = opPrefix;

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

            opRename.ItemsTotal = BSA.SelectMany(folder => folder).Count(); //allFiles count

            var renameFixes = from g in newBsaFolders
                              from a in g
                              join newFolder in BSA on g.Key equals newFolder.Path
                              let newFile = a.file.DeepCopy(g.Key, Path.GetFileName(a.newFilename))
                              let addedFile = newFolder.Add(newFile)
                              select new { oldName = a.file.Name, newName = newFile.Name, a.newFilename };

#if PARALLEL
            Parallel.ForEach(renameFixes, a =>
#else
            foreach (var a in renameFixes)
#endif
            {
                renameDict.Remove(a.newFilename);

                opRename.CurrentOperation = opPrefix + ": " + a.oldName + " -> " + a.newName;
                opRename.Step();
            }
#if PARALLEL
)
#endif
            ;
        }

        public bool PatchFile(BSAFile bsaFile, FileValidation oldChk, PatchInfo patch, bool failFast = false)
        {
            bool perfect = true;

            //file exists but is not up to date
            if (patch.Data != null && patch.Data.Length > 0)
            {
                //a patch exists for the file

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
                        if (patch.Metadata.Equals(testChk))
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
            }
            else
            {
                //no patch exists for the file
                var err = "WARNING: File is of an unexpected version: " + bsaFile.Filename + " - " + oldChk;

                if (failFast)
                    Trace.Fail(err);
                else
                {
                    perfect = false;
                    Log(err);
                    Log("This file cannot be patched. Errors may occur.");
                }
            }

            return perfect;
        }
    }
}
