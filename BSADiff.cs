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

namespace TaleOfTwoWastelands
{
    class BSADiff
    {
#if ASYNC
        public static async Task<string> PatchBSA_Async(IProgress<string> progress, CancellationToken token, string oldBSA, string newBSA, string BSADir, string patchDir)
#else
        public static string PatchBSA(IProgress<string> progress, CancellationToken token, string oldBSA, string newBSA, string BSADir, string patchDir)
#endif
        {
            var sbErrors = new StringBuilder();

            IDictionary<string, string> renameDict, newChkDict, oldChkDict;
            renameDict = new SortedDictionary<string, string>();
            newChkDict = new SortedDictionary<string, string>();

#if ASYNC
            await BSAOpt.ExtractBSA_Async(progress, token, oldBSA, BSADir);
#else
            BSAOpt.ExtractBSA(progress, token, oldBSA, BSADir);
#endif
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

            foreach (var entry in renameDict)
            {
                string oldFile = entry.Value;
                string newFile = entry.Key;

                var oldPath = Path.Combine(BSADir, oldFile);
                var newPath = Path.Combine(BSADir, newFile);

                if (File.Exists(oldPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                    File.Copy(oldPath, newPath);
                }
                else
                {
                    sbErrors.AppendLine("\tFile not found: " + oldFile);
                    sbErrors.AppendLine("\t\tCannot create: " + newFile);
                }
            }

            oldChkDict =
                Directory.EnumerateFiles(BSADir, "*", SearchOption.AllDirectories).
                ToDictionary(
                    file => file.Replace(BSADir, "").TrimStart(Path.DirectorySeparatorChar),
                    file => Util.GetChecksum(file)
                );
            //foreach (string file in Directory.EnumerateFiles(BSADir, "*", SearchOption.AllDirectories))
            //{
            //    oldChkDict.Add(file.Replace(BSADir, "").TrimStart(Path.DirectorySeparatorChar), GetChecksum(file));
            //}

            foreach (var entry in newChkDict)
            {
                string oldChk;
                string newChk = entry.Value;
                string file = entry.Key;
                string filePath = Path.Combine(BSADir, file);

                if (oldChkDict.TryGetValue(file, out oldChk))
                {
                    //file exists
                    if (oldChk != newChk)
                    {
                        //file exists but is not up to date
                        var diffPath = Path.Combine(patchDir, file + "." + oldChk + "." + newChk + ".diff");
                        var tmpPath = filePath + ".tmp";
                        if (File.Exists(diffPath))
                        {
                            //a patch exists for the file
                            using (FileStream input = File.OpenRead(filePath), output = File.OpenWrite(tmpPath))
                                BinaryPatchUtility.Apply(input, () => File.OpenRead(diffPath), output);

                            oldChk = Util.GetChecksum(tmpPath);
                            if (oldChk == newChk)
                                File.Replace(tmpPath, filePath, null);
                            else
                                sbErrors.AppendLine("\tPatching " + file + " has failed - " + oldChk);

                        }
                        else
                        {
                            //no patch exists for the file
                            sbErrors.AppendLine("\tFile is of an unexpected version: " + file + " - " + oldChk);
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

            foreach (var file in oldChkDict.Where(kvp => newChkDict.ContainsKey(kvp.Key)).Select(kvp => kvp.Value))
            {
                var filePath = Path.Combine(BSADir, file);
                File.Delete(filePath);
            }

#if ASYNC
            await BSAOpt.BuildBSA_Async(progress, token, BSADir, newBSA);
#else
            BSAOpt.BuildBSA(progress, token, BSADir, newBSA);
#endif

            return sbErrors.ToString();
        }
    }
}