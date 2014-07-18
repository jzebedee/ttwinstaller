using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TaleOfTwoWastelands.Patching;
using TaleOfTwoWastelands.Patching.Murmur;

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

        #region GetMD5 overloads
        public static BigInteger GetMD5(string file)
        {
            using (var stream = File.OpenRead(file))
                return GetMD5(stream);
        }

        public static BigInteger GetMD5(Stream stream)
        {
            using (var fileHash = MD5.Create())
            using (stream)
                return fileHash.ComputeHash(stream).ToBigInteger();
        }

        public static BigInteger GetMD5(byte[] buf)
        {
            using (var fileHash = MD5.Create())
                return fileHash.ComputeHash(buf).ToBigInteger();
        }

        public static string MakeMD5String(BigInteger md5)
        {
            return md5.ToString("x32");
        }

        public static string GetMD5String(string file)
        {
            return MakeMD5String(GetMD5(file));
        }

        public static string GetMD5String(Stream stream)
        {
            return MakeMD5String(GetMD5(stream));
        }

        public static string GetMD5String(byte[] buf)
        {
            return MakeMD5String(GetMD5(buf));
        }
        #endregion

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
