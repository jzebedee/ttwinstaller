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

            IDictionary<string, string> renameDict, newChkDict;
            renameDict = new SortedDictionary<string, string>();
            newChkDict = new SortedDictionary<string, string>();

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
                var outFilename = Path.GetFileNameWithoutExtension(newBSA);

                try
                {
                    opProg.CurrentOperation = "Opening rename fixes";

                    var renamePath = Path.Combine(PatchDir, outFilename, "RenameFiles.dict");
                    if (File.Exists(renamePath))
                    {
                        using (FileStream stream = new FileStream(renamePath, FileMode.Open))
                        {
                            BinaryFormatter bFormatter = new BinaryFormatter();
                            renameDict = (SortedDictionary<string, string>)bFormatter.Deserialize(stream);
                        }
                    }
                }
                finally
                {
                    opProg.Step();
                }

                try
                {
                    opProg.CurrentOperation = "Opening checksum database";

                    var checksumPath = Path.Combine(PatchDir, outFilename, "CheckSums.dict");
                    if (File.Exists(checksumPath))
                    {
                        using (FileStream stream = new FileStream(checksumPath, FileMode.Open))
                        {
                            BinaryFormatter bFormatter = new BinaryFormatter();
                            newChkDict = (SortedDictionary<string, string>)bFormatter.Deserialize(stream);
                        }
                    }
                    else
                    {
                        sbErrors.AppendLine("\tNo Checksum dictionary is available for: " + oldBSA);
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
                        sbErrors.AppendLine("\tFile not found: " + kvp.Value);
                        sbErrors.AppendLine("\t\tCannot create: " + kvp.Key);
                    }
                }

                var allFiles = BSA.SelectMany(folder => folder);

                try
                {
                    var opChk = new OperationProgress(progressUI, token);

                    var oldChkDict = allFiles.ToDictionary(file => file.Filename, file => new { file, checksum = new Lazy<string>(() => Util.GetChecksum(file.GetSaveData(true))) });
                    opChk.ItemsTotal = newChkDict.Count;

                    foreach (var entry in newChkDict)
                    {
                        string newChk = entry.Value;
                        string file = entry.Key;

                        if (oldChkDict.ContainsKey(file))
                        {
                            var anon = oldChkDict[file];

                            opChk.CurrentOperation = "Validating " + anon.file.Name;

                            //file exists
                            if (anon.checksum.Value != newChk)
                            {
                                opChk.CurrentOperation = "Patching " + anon.file.Name;

                                var patchErrors = PatchFile(outFilename, anon.file, anon.checksum.Value, newChk);
                                sbErrors.Append(patchErrors);
                            }
                        }
                        else
                        {
                            //file not found
                            sbErrors.AppendLine("\tFile not found: " + file);
                        }

                        opChk.Step();
                    }
                }
                finally
                {
                    opProg.Step();
                }

                try
                {
                    opProg.CurrentOperation = "Removing unnecessary files";

                    var filesToRemove = new HashSet<BSAFile>(allFiles.Where(file => !newChkDict.ContainsKey(file.Filename)));
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

        private static string PatchFile(string bsaPrefix, BSAFile bsaFile, string checksumA, string checksumB)
        {
            if (string.IsNullOrEmpty(PatchDir))
                throw new ArgumentNullException("PatchDir was not set");

            var sbErrors = new StringBuilder();

            //file exists but is not up to date
            var diffPath = Path.Combine(PatchDir, bsaPrefix, bsaFile.Filename + "." + checksumA + "." + checksumB + ".diff");
            if (File.Exists(diffPath))
            {
                byte[] patchedBytes;

                //a patch exists for the file
                using (MemoryStream input = new MemoryStream(bsaFile.GetSaveData(true)), output = new MemoryStream())
                {
                    using (var mmfPatch = MemoryMappedFile.CreateFromFile(diffPath))
                        BinaryPatchUtility.Apply(input, () => mmfPatch.CreateViewStream(), output);
                    patchedBytes = output.ToArray();
                }

                var oldChk = Util.GetChecksum(patchedBytes);
                if (oldChk == checksumB)
                    bsaFile.UpdateData(patchedBytes, false);
                else
                    sbErrors.AppendLine("\tPatching " + bsaFile.Filename + " has failed - " + oldChk);
            }
            else
            {
                //no patch exists for the file
                sbErrors.AppendLine("\tFile is of an unexpected version: " + bsaFile.Filename + " - " + checksumA);
                sbErrors.AppendLine("\t\tThis file cannot be patched. Errors may occur.");
            }

            return sbErrors.ToString();
        }
    }
}