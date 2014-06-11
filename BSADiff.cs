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
        public static string PatchBSA(IProgress<string> progress, CancellationToken token, string oldBSA, string newBSA, string patchDir)
        {
            var sbErrors = new StringBuilder();

            IDictionary<string, string> renameDict, newChkDict;
            renameDict = new SortedDictionary<string, string>();
            newChkDict = new SortedDictionary<string, string>();

            var BSA = new BSAWrapper(oldBSA);
            token.ThrowIfCancellationRequested();

            var renamePath = Path.Combine(patchDir, "RenameFiles.dict");
            if (File.Exists(renamePath))
            {
                using (FileStream stream = new FileStream(renamePath, FileMode.Open))
                {
                    BinaryFormatter bFormatter = new BinaryFormatter();
                    renameDict = (SortedDictionary<string, string>)bFormatter.Deserialize(stream);
                }
            }

            var checksumPath = Path.Combine(patchDir, "CheckSums.dict");
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
                    //var parentFolder = BSA.Where(folder => folder.IsParent(oldBsaFile)).SingleOrDefault();
                    //Trace.Assert(parentFolder != null);

                    //parentFolder.Remove(oldBsaFile);

                    //var BSAfile = new BSAFile(Path.GetDirectoryName(newFilename), newFilename, BSA.Settings, oldBsaFile.GetSaveData(false), oldBsaFile.IsCompressed);
                    //parentFolder.Add(BSAfile);
                    oldBsaFile.UpdatePath(Path.GetDirectoryName(newFilename), newFilename);
                }
                else
                {
                    sbErrors.AppendLine("\tFile not found: " + oldFilename);
                    sbErrors.AppendLine("\t\tCannot create: " + newFilename);
                }
            }

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
                        //file exists but is not up to date
                        var diffPath = Path.Combine(patchDir, file + "." + anon.checksum + "." + newChk + ".diff");
                        if (File.Exists(diffPath))
                        {
                            byte[] patchedBytes;

                            //a patch exists for the file
                            using (MemoryStream input = new MemoryStream(anon.file.GetSaveData(true)), output = new MemoryStream())
                            {
                                using (var mmfPatch = MemoryMappedFile.CreateFromFile(diffPath))
                                    BinaryPatchUtility.Apply(input, () => mmfPatch.CreateViewStream(), output);
                                patchedBytes = output.ToArray();
                            }

                            var oldChk = Util.GetChecksum(patchedBytes);
                            if (oldChk == newChk)
                                anon.file.UpdateData(patchedBytes, false);
                            else
                                sbErrors.AppendLine("\tPatching " + file + " has failed - " + oldChk);

                        }
                        else
                        {
                            //no patch exists for the file
                            sbErrors.AppendLine("\tFile is of an unexpected version: " + file + " - " + anon.checksum);
                            sbErrors.AppendLine("\t\tThis file cannot be patched. Errors may occur.");
                        }
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
    }
}