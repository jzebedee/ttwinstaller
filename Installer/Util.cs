using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using TaleOfTwoWastelands.Patching;
using TaleOfTwoWastelands.Patching.Murmur;
using TaleOfTwoWastelands.ProgressTypes;

namespace TaleOfTwoWastelands
{
    public static class Util
    {
        #region GetMD5 overloads
        public static byte[] GetMD5(string file)
        {
            using (var stream = File.OpenRead(file))
                return GetMD5(stream);
        }

        public static byte[] GetMD5(Stream stream)
        {
            using (var fileHash = MD5.Create())
            using (stream)
                return fileHash.ComputeHash(stream);
        }

        public static byte[] GetMD5(byte[] buf)
        {
            using (var fileHash = MD5.Create())
                return fileHash.ComputeHash(buf);
        }

        public static string MakeMD5String(byte[] md5)
        {
            return BitConverter.ToString(md5).Replace("-", "");
        }
        public static byte[] FromMD5String(string md5str)
        {
            byte[] data = new byte[md5str.Length / 2];
            for (int i = 0; i < data.Length; i++)
                data[i] = Convert.ToByte(md5str.Substring(i * 2, 2), 16);

            return data;
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

        #region Legacy mode
#if LEGACY || DEBUG
        public static IDictionary<string, string> ReadOldDatabase(string path)
        {
            Debug.Assert(File.Exists(path));

            using (var stream = File.OpenRead(path))
                return (IDictionary<string, string>)new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter().Deserialize(stream);
        }

        public static IEnumerable<Tuple<string, byte[]>> FindAlternateVersions(string file)
        {
            var justName = Path.GetFileName(file);
            var split = justName.Split('.');
            split[split.Length - 3] = "*";
            //combatshotgun.nif.8154C65E957F6A29B36ADA24CFBC1FDE.1389525E123CD0F8CD5BB47EF5FD1901.diff
            //end[0] = diff, end[1] = newChk, end[2] = oldChk, end[3 ...] = fileName

            var justDir = Path.GetDirectoryName(file);
            if (!Directory.Exists(justDir))
                return null;

            return from other in Directory.EnumerateFiles(justDir, string.Join(".", split))
                   where other != file
                   let splitOther = Path.GetFileName(other).Split('.')
                   select Tuple.Create(other, Util.FromMD5String(splitOther[splitOther.Length - 3]));
        }
#endif
        #endregion

        public static void CopyFolder(string inFolder, string destFolder, IProgress<string> log)
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
                    log.Report("ERROR: " + file.Replace(inFolder, "") + " did not copy successfully due to: Unauthorized Access Exception " + error.Source + ".");
                }
            }
        }
    }
}
