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
using ProtoBuf;

namespace TaleOfTwoWastelands.Patching
{
    class BSADiff
    {
        public static readonly string PatchDir = Path.Combine(Installer.AssetsDir, "TTW Data", "TTW Patches");

        public static string PatchBSA(IProgress<string> progressLog, IProgress<OperationProgress> progressUI, CancellationToken token, CompressionOptions bsaOptions, string oldBSA, string newBSA, bool simulate = false)
        {
            if (string.IsNullOrEmpty(PatchDir))
                throw new ArgumentNullException("PatchDir was not set");

            var sbErrors = new StringBuilder();
            var opProg = new OperationProgress(progressUI, token) { ItemsTotal = 7 };

            var outBsaFilename = Path.GetFileNameWithoutExtension(newBSA);

            BSAWrapper BSA;
            try
            {
                opProg.CurrentOperation = "Opening " + Path.GetFileName(oldBSA);

                BSA = new BSAWrapper(oldBSA, bsaOptions);
            }
            finally
            {
                opProg.Step();
            }

            var renameDict = new Dictionary<string, string>();
            try
            {
                opProg.CurrentOperation = "Opening rename database";

                var renamePath = Path.Combine(PatchDir, "Checksums", Path.ChangeExtension(outBsaFilename, ".ren"));
                if (File.Exists(renamePath))
                {
                    using (var stream = File.OpenRead(renamePath))
                    {
                        renameDict = Serializer.Deserialize<Dictionary<string, string>>(stream);
                    }
                }
            }
            finally
            {
                opProg.Step();
            }

            var patchDict = new Dictionary<string, PatchInfo>();
            try
            {
                opProg.CurrentOperation = "Opening patch database";

                var patchPath = Path.Combine(PatchDir, "Checksums", Path.ChangeExtension(outBsaFilename, ".pat"));
                if (File.Exists(patchPath))
                {
                    using (var stream = File.OpenRead(patchPath))
                    {
                        patchDict = Serializer.Deserialize<Dictionary<string, PatchInfo>>(stream);
                    }
                }
                else
                {
                    sbErrors.AppendLine("\tNo patch database is available for: " + oldBSA);
                    return sbErrors.ToString();
                }
            }
            finally
            {
                opProg.Step();
            }

            using (BSA)
            {
                try
                {
                    var opRename = new OperationProgress(progressUI, token);

                    RenameFiles(BSA, renameDict, opRename);

                    if (renameDict.Count > 0)
                    {
                        foreach (var kvp in renameDict)
                        {
                            sbErrors.AppendLine("\tFile not found: " + kvp.Value);
                            sbErrors.AppendLine("\t\tCannot create: " + kvp.Key);
                        }
                    }
                }
                finally
                {
                    opProg.Step();
                }

                var allFiles = BSA.SelectMany(folder => folder).ToList();
                try
                {
                    var opChk = new OperationProgress(progressUI, token);

                    var oldChkDict = FileValidation.FromBSA(BSA);
                    opChk.ItemsTotal = patchDict.Count;

                    var joinedPatches = from patKvp in patchDict
                                        join oldKvp in oldChkDict on patKvp.Key equals oldKvp.Key into foundOld
                                        join bsaFile in allFiles on patKvp.Key equals bsaFile.Filename
                                        select new
                                        {
                                            bsaFile,
                                            file = patKvp.Key,
                                            patchInfo = patKvp.Value,
                                            oldChk = foundOld.SingleOrDefault()
                                        };

#if PARALLEL
                    Parallel.ForEach(joinedPatches, join =>
#else
                    foreach (var join in joinedPatches)
#endif
                    {
                        if (string.IsNullOrEmpty(join.oldChk.Key))
                        {
                            //file not found
                            sbErrors.AppendLine("\tFile not found: " + join.file);

                            opChk.Step();
#if PARALLEL
                            return;
#else
                            continue;
#endif
                        }

                        var lazyOldChk = join.oldChk.Value;
                        using (var oldChk = lazyOldChk.Value)
                        {
                            var newChk = join.patchInfo.Metadata;
                            opChk.CurrentOperation = "Validating " + join.bsaFile.Name;

                            if (!newChk.Equals(oldChk))
                            {
                                opChk.CurrentOperation = "Patching " + join.bsaFile.Name;

                                var patchErrors = PatchFile(join.bsaFile, oldChk, join.patchInfo);
                                sbErrors.Append(patchErrors);
                            }
                        }

                        opChk.Step();
                    }
#if PARALLEL
);
#endif
                }
                finally
                {
                    opProg.Step();
                }

                try
                {
                    opProg.CurrentOperation = "Removing unnecessary files";

                    var notIncluded = allFiles.Where(file => !patchDict.ContainsKey(file.Filename));
                    var filesToRemove = new HashSet<BSAFile>(notIncluded);

                    var filesRemoved = BSA.Sum(folder => folder.RemoveWhere(bsafile => filesToRemove.Contains(bsafile)));
                    BSA.RemoveWhere(folder => folder.Count == 0);
                }
                finally
                {
                    opProg.Step();
                }

                try
                {
                    opProg.CurrentOperation = "Building " + Path.GetFileName(newBSA);

                    if (!simulate)
                        BSA.Save(newBSA.ToLowerInvariant());
                }
                finally
                {
                    opProg.Step();
                }
            }

            opProg.Finish();

            return sbErrors.ToString();
        }

        public static void RenameFiles(BSAWrapper BSA, Dictionary<string, string> renameDict, OperationProgress opRename)
        {
            var opPrefix = "Renaming BSA files";

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

            var newBsaFolders = from g in renameCopies
                                let folderAdded = BSA.Add(new BSAFolder(g.Key))
                                select g;
            newBsaFolders.ToList();

            opRename.ItemsTotal = BSA.SelectMany(folder => folder).Count();

            var renameFixes = from g in newBsaFolders
                              from a in g
                              join newFolder in BSA on g.Key equals newFolder.Path
                              let newFile = a.file.DeepCopy(g.Key, Path.GetFileName(a.newFilename))
                              let addedFile = newFolder.Add(newFile)
                              //let removedFile = a.folder.Remove(a.file)
                              //don't say this too fast
                              let cleanedDict = renameDict.Remove(a.newFilename)

                              let curOp = (opRename.CurrentOperation = opPrefix + ": " + a.file.Name + " -> " + newFile.Name)
                              let curDone = opRename.Step()

                              select new { a.folder, a.file, newFolder, newFile, a.newFilename };
            renameFixes
#if PARALLEL
.AsParallel()
#endif
.ToList(); // execute query
        }

        public static string PatchFile(BSAFile bsaFile, FileValidation oldChk, PatchInfo patch, bool failFast = false)
        {
            if (string.IsNullOrEmpty(PatchDir))
                throw new ArgumentNullException("PatchDir was not set");

            var sbErrors = new StringBuilder();

            //file exists but is not up to date
            if (patch.Data != null)
            {
                //a patch exists for the file
                using (MemoryStream
                    //InflaterInputStream won't let the patcher seek it,
                    //so we have to perform a new allocate-and-copy
                    input = new MemoryStream(bsaFile.GetContents(true)),
                    output = new MemoryStream())
                {
                    unsafe
                    {
                        fixed (byte* pData = patch.Data)
                            BinaryPatchUtility.Apply(input, pData, patch.Data.Length, output);
                    }

                    output.Seek(0, SeekOrigin.Begin);
                    using (var testChk = new FileValidation(output))
                    {
                        if (patch.Metadata.Equals(testChk))
                            bsaFile.UpdateData(output.ToArray(), false);
                        else
                        {
                            var err = "\tPatching " + bsaFile.Filename + " has failed - " + testChk;
                            if (failFast)
                                Trace.Fail(err);
                            else
                                sbErrors.AppendLine(err);
                        }
                    }
                }
            }
            else
            {
                //no patch exists for the file
                var err = "\tFile is of an unexpected version: " + bsaFile.Filename + " - " + oldChk;

                if (failFast)
                    Trace.Fail(err);
                else
                {
                    sbErrors.AppendLine(err);
                    sbErrors.AppendLine("\t\tThis file cannot be patched. Errors may occur.");
                }
            }

            return sbErrors.ToString();
        }
    }
}