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

namespace TaleOfTwoWastelands
{
    class BSADiff
    {
        public static string PatchDir { get; set; }

        public static string PatchBSA(IProgress<string> progress, CancellationToken token, string oldBSA, string newBSA)
        {
            var sbErrors = new StringBuilder();

            IDictionary<string, string> renameDict, newChkDict;
            renameDict = new SortedDictionary<string, string>();
            newChkDict = new SortedDictionary<string, string>();

            var BSA = new BSAWrapper(oldBSA);
            token.ThrowIfCancellationRequested();

            var outFilename = Path.GetFileNameWithoutExtension(newBSA);

            var renamePath = Path.Combine(PatchDir, outFilename, "RenameFiles.dict");
            if (File.Exists(renamePath))
            {
                using (FileStream stream = new FileStream(renamePath, FileMode.Open))
                {
                    BinaryFormatter bFormatter = new BinaryFormatter();
                    renameDict = (SortedDictionary<string, string>)bFormatter.Deserialize(stream);
                }
            }

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

            var allFiles = BSA.SelectMany(folder => folder);
            foreach (var entry in renameDict)
            {
                string oldFilename = entry.Value;
                string newFilename = entry.Key;

                var oldBsaFile = allFiles.Where(file => file.Filename == oldFilename).SingleOrDefault();
                if (oldBsaFile != null)
                {
                    var destFolder = BSA.Where(folder => folder.Path == Path.GetDirectoryName(newFilename)).SingleOrDefault();
                    if (destFolder == null)
                    {
                        destFolder = new BSAFolder(Path.GetDirectoryName(newFilename));
                        Trace.Assert(BSA.Add(destFolder));
                    }

                    var newBsaFile = oldBsaFile.DeepCopy();
                    newBsaFile.UpdatePath(Path.GetDirectoryName(newFilename), Path.GetFileName(newFilename));

                    destFolder.Add(newBsaFile);
                }
                else
                {
                    sbErrors.AppendLine("\tFile not found: " + oldFilename);
                    sbErrors.AppendLine("\t\tCannot create: " + newFilename);
                }
            }

            //var allGroups = BSA.SelectMany(folder => folder.Select(file => new { folder, file }));

            //var renamedToAdd =
            //    (from entry in renameDict
            //     let oldFilename = entry.Value
            //     let newFilename = entry.Key
            //     let oldBsaFile = allGroups.Where(a => a.file.Filename == oldFilename).SingleOrDefault()
            //     select new { oldFilename, newFilename, oldBsaFile })
            //     .Select(
            //     fnGroup =>
            //     {
            //         var oldBsaFile = fnGroup.oldBsaFile.file;
            //         if (oldBsaFile != null)
            //         {
            //             var newBsaFile = oldBsaFile.DeepCopy();
            //             newBsaFile.UpdatePath(Path.GetDirectoryName(fnGroup.newFilename), Path.GetFileName(fnGroup.newFilename));

            //             return new { fnGroup.oldBsaFile.folder, newBsaFile };
            //         }
            //         else
            //         {
            //             sbErrors.AppendLine("\tFile not found: " + fnGroup.oldFilename);
            //             sbErrors.AppendLine("\t\tCannot create: " + fnGroup.newFilename);
            //         }

            //         return null;
            //     })
            //     .Where(bsaGroup => bsaGroup != null);

            var oldChkDict = allFiles.ToDictionary(file => file.Filename, file => new { file, checksum = new Lazy<string>(() => Util.GetChecksum(file.GetSaveData(true))) });

            foreach (var entry in newChkDict)
            {
                string newChk = entry.Value;
                string file = entry.Key;

                if (oldChkDict.ContainsKey(file))
                {
                    var anon = oldChkDict[file];

                    //file exists
                    if (anon.checksum.Value != newChk)
                    {
                        var patchErrors = PatchFile(outFilename, anon.file, anon.checksum.Value, newChk);
                        sbErrors.Append(patchErrors);
                    }
                }
                else
                {
                    //file not found
                    sbErrors.AppendLine("\tFile not found: " + file);
                }
            }

            var filesToRemove = new HashSet<BSAFile>(
                oldChkDict
                .Where(kvp => !newChkDict.ContainsKey(kvp.Key))
                .Select(kvp => kvp.Value.file));
            var filesRemoved = BSA.Sum(folder => folder.RemoveWhere(bsafile => filesToRemove.Contains(bsafile)));

            BSA.RemoveWhere(folder => folder.Count == 0);
            BSA.Save(newBSA);

            //BSA.BuildBSA(progress, token, BSADir, newBSA);

            return sbErrors.ToString();
        }

        private static string PatchFile(string bsaPrefix, BSAFile bsaFile, string checksumA, string checksumB)
        {
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