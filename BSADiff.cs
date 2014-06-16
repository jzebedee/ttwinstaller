using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using BSAsharp;
using System.IO.MemoryMappedFiles;
using TaleOfTwoWastelands.ProgressTypes;

namespace TaleOfTwoWastelands
{
    class BSADiff
    {
        public static string PatchDir { get; set; }

        public static string PatchBSA(IProgress<string> progressLog, IProgress<OperationProgress> progressUI, CancellationToken token, string oldBSA, string newBSA)
        {
            if (string.IsNullOrEmpty(PatchDir))
                throw new ArgumentNullException("PatchDir was not set");

            var sbErrors = new StringBuilder();
            var opProg = new OperationProgress(progressUI, token) { ItemsTotal = 7 };

            var renameDict = new Dictionary<string, string>();
            var patchDict = new Dictionary<string, PatchInfo>();

            BSAWrapper BSA;
            try
            {
                opProg.CurrentOperation = "Opening " + Path.GetFileName(oldBSA);

                BSA = new BSAWrapper(oldBSA);
            }
            finally
            {
                opProg.Step();
            }

            using (BSA)
            {
                var outBsaFilename = Path.GetFileNameWithoutExtension(newBSA);

                try
                {
                    opProg.CurrentOperation = "Opening rename database";

                    var renamePath = Path.Combine(PatchDir, "Checksums", Path.ChangeExtension(outBsaFilename, ".ren"));
                    if (File.Exists(renamePath))
                    {
                        using (var stream = File.OpenRead(renamePath))
                        {
                            var bFormatter = new BinaryFormatter();
                            renameDict = (Dictionary<string, string>)bFormatter.Deserialize(stream);
                        }
                    }
                }
                finally
                {
                    opProg.Step();
                }

                try
                {
                    opProg.CurrentOperation = "Opening patch database";

                    var patchPath = Path.Combine(PatchDir, "Checksums", Path.ChangeExtension(outBsaFilename, ".pat"));
                    if (File.Exists(patchPath))
                    {
                        using (var stream = File.OpenRead(patchPath))
                        {
                            var bFormatter = new BinaryFormatter();
                            patchDict = (Dictionary<string, PatchInfo>)bFormatter.Deserialize(stream);
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

                try
                {
                    var opRename = new OperationProgress(progressUI, token);

                    var opPrefix = "Renaming BSA files";

                    opRename.CurrentOperation = opPrefix;

                    var renameGroup = from folder in BSA.ToList()
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
                    renameFixes.ToList(); // execute query
                }
                finally
                {
                    opProg.Step();
                }

                if (renameDict.Count > 0)
                {
                    foreach (var kvp in renameDict)
                    {
#if BUILD_PATCHDB
                        Trace.Fail("You can't build a patchDB with missing files!");
#endif
                        sbErrors.AppendLine("\tFile not found: " + kvp.Value);
                        sbErrors.AppendLine("\t\tCannot create: " + kvp.Key);
                    }
                }

                var allFiles = BSA.SelectMany(folder => folder);

#if BUILD_PATCHDB
                var patDB = new Dictionary<string, PatchInfo>();
#endif
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
                                            patch = patKvp.Value,
                                            oldChk = foundOld.SingleOrDefault()
                                        };

                    foreach (var join in joinedPatches)
                    {
                        if (string.IsNullOrEmpty(join.oldChk.Key))
                        {
#if BUILD_PATCHDB
                            Trace.Fail("You can't build a patchDB with invalid files!");
#endif
                            //file not found
                            sbErrors.AppendLine("\tFile not found: " + join.file);

                            opChk.Step();
                            continue;
                        }

                        var oldChk = join.oldChk.Value;
                        var newChk = join.patch.Metadata;
                        opChk.CurrentOperation = "Validating " + join.bsaFile.Name;

                        if (!newChk.Equals(oldChk))
                        {
                            opChk.CurrentOperation = "Patching " + join.bsaFile.Name;

                            var patchErrors = PatchFile(outBsaFilename, join.bsaFile, oldChk, join.patch);
                            sbErrors.Append(patchErrors);
                        }

#if BUILD_PATCHDB
                        MD5 fileHash = MD5.Create();
                        var oldChksum = BitConverter.ToString(fileHash.ComputeHash(join.bsaFile.GetSaveData(true))).Replace("-", "");
                        var diffPath = Path.Combine(PatchDir, outBsaFilename, join.bsaFile.Filename + "." + oldChksum + "." + newChk + ".diff");
                        var patch = new PatchInfo()
                        {
                            Metadata = FileValidation.FromBSAFile(join.bsaFile),
                            Data = File.Exists(diffPath) ? File.ReadAllBytes(diffPath) : null
                        };
                        patDB.Add(join.bsaFile.Filename, patch);
#endif
                        opChk.Step();
                    }
                }
                finally
                {
                    opProg.Step();
                }

#if BUILD_PATCHDB
                var patchDBFilename = Path.Combine("Checksums", Path.ChangeExtension(outBsaFilename, ".pat"));

                var bformatter = new BinaryFormatter();
                using (var patStream = File.OpenWrite(patchDBFilename))
                    bformatter.Serialize(patStream, patDB);
#endif

                try
                {
                    opProg.CurrentOperation = "Removing unnecessary files";

                    var filesToRemove = new HashSet<BSAFile>(allFiles.Where(file => !patchDict.ContainsKey(file.Filename)));
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

                    BSA.Save(newBSA);
                }
                finally
                {
                    opProg.Step();
                }
            }

            opProg.Finish();

            return sbErrors.ToString();
        }

        private static string PatchFile(string bsaPrefix, BSAFile bsaFile, FileValidation oldChk, PatchInfo patch)
        {
            if (string.IsNullOrEmpty(PatchDir))
                throw new ArgumentNullException("PatchDir was not set");

            var sbErrors = new StringBuilder();

            //file exists but is not up to date
            if (patch.Data != null)
            {
                byte[] patchedBytes;

                //a patch exists for the file
                using (MemoryStream input = new MemoryStream(bsaFile.GetSaveData(true)), output = new MemoryStream())
                {
                    BinaryPatchUtility.Apply(input, () => new MemoryStream(patch.Data), output);
                    patchedBytes = output.ToArray();
                }

                var testBsaFile = bsaFile.DeepCopy();
                testBsaFile.UpdateData(patchedBytes, false);

                var oldChk2 = FileValidation.FromBSAFile(testBsaFile);
                if (patch.Metadata.Equals(oldChk2))
                    bsaFile.UpdateData(patchedBytes, false);
                else
                    sbErrors.AppendLine("\tPatching " + bsaFile.Filename + " has failed - " + oldChk2);
            }
            else
            {
                //no patch exists for the file
                sbErrors.AppendLine("\tFile is of an unexpected version: " + bsaFile.Filename + " - " + oldChk);
                sbErrors.AppendLine("\t\tThis file cannot be patched. Errors may occur.");
            }

            return sbErrors.ToString();
        }
    }
}