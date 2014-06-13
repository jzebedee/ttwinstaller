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

            var renameCopies = from folder in BSA.ToList()
                               from file in folder
                               join kvp in renameDict on file.Filename equals kvp.Value
                               let newFilename = kvp.Key
                               let newDirectory = Path.GetDirectoryName(newFilename)
                               let a = new { folder, file, newFilename, newDirectory }
                               let id = new { newFilename, newDirectory }
                               group a by id into outs
                               select outs;

            var newBsaFolders = from g in renameCopies
                                let folderAdded = BSA.Add(new BSAFolder(g.Key.newDirectory))
                                select g;

            var renameFixes = from g in newBsaFolders.ToList()
                              join newFolder in BSA on g.Key.newDirectory equals newFolder.Path
                              from a in g
                              let newFile = a.file.DeepCopy(a.newDirectory, Path.GetFileName(a.newFilename))
                              select new { a.folder, a.file, newFolder, newFile, g.Key.newFilename };

            foreach (var fix in renameFixes)
            {
                //don't say this too fast
                var cleanedDict = renameDict.Remove(fix.newFilename);

                var addedFile = fix.newFolder.Add(fix.newFile);
                var removedFile = fix.folder.Remove(fix.file);
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

            var filesToRemove = new HashSet<BSAFile>(allFiles
                .Where(file => !newChkDict.ContainsKey(file.Filename))
                .Select(file => file));
            var filesRemoved = BSA.Sum(folder => folder.RemoveWhere(bsafile => filesToRemove.Contains(bsafile)));
            BSA.RemoveWhere(folder => folder.Count == 0);

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