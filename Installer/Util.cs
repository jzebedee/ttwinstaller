using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TaleOfTwoWastelands
{
    public static class Util
    {
        public static void BuildFOMOD(string inDir, string outFile)
        {
            System.Diagnostics.Process zip = new System.Diagnostics.Process();

            zip.StartInfo.FileName = "\"" + Path.Combine(Installer.AssetsDir, "7Zip", "7za.exe") + "\"";
            zip.StartInfo.Arguments = " a -mx0 -tzip \"" + outFile + "\" \"" + inDir + "\"";
            zip.StartInfo.UseShellExecute = false;
            zip.StartInfo.RedirectStandardOutput = true;
            zip.Start();
            string log = zip.StandardOutput.ReadToEnd();
            zip.Close();
        }

        public static string GetMD5(string fileIn)
        {
            MD5 fileHash = MD5.Create();
            using (var checkFile = File.OpenRead(fileIn))
            {
                return BitConverter.ToString(fileHash.ComputeHash(checkFile)).Replace("-", "");
            }
        }

        public static bool ApplyPatch(Dictionary<string, string> CheckSums, string inFile, string patchFile, string outFile)
        {
            System.Diagnostics.Process xDiff = new System.Diagnostics.Process();

            xDiff.StartInfo.FileName = Path.Combine(Installer.AssetsDir, "xdelta3_x32.exe");
            xDiff.StartInfo.Arguments = " -d -s \"" + inFile + "\" \"" + patchFile + "\" \"" + outFile;
            xDiff.Start();
            xDiff.WaitForExit();

            string hash;
            CheckSums.TryGetValue(Path.GetFileName(inFile), out hash);

            return GetMD5(outFile) == hash;
        }

        public static void CopyFolder(string inFolder, string destFolder, Action<string> failHandler)
        {
            Directory.CreateDirectory(destFolder);

            foreach (string folder in Directory.EnumerateDirectories(inFolder, "*", SearchOption.AllDirectories))
            {
                var justFolder = folder.Replace(inFolder, "").TrimStart(Path.DirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destFolder, justFolder));
            }
            foreach (string file in Directory.EnumerateFiles(inFolder, "*", SearchOption.AllDirectories))
            {
                var justFile = file.Replace(inFolder, "").TrimStart(Path.DirectorySeparatorChar);

                try
                {
                    File.Copy(file, Path.Combine(destFolder, justFile), true);
                }
                catch (UnauthorizedAccessException error)
                {
                    failHandler("ERROR: " + file.Replace(inFolder, "") + " did not copy successfully due to: Unauthorized Access Exception " + error.Source + ".");
                }
            }
        }
    }
}