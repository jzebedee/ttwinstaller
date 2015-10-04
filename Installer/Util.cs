using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using TaleOfTwoWastelands.Properties;

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

        public static string MakeMd5String(byte[] md5)
        {
            return BitConverter.ToString(md5).Replace("-", "");
        }
        public static byte[] FromMd5String(string md5Str)
        {
            byte[] data = new byte[md5Str.Length / 2];
            for (int i = 0; i < data.Length; i++)
                data[i] = Convert.ToByte(md5Str.Substring(i * 2, 2), 16);

            return data;
        }

        public static string GetMD5String(string file)
        {
            return MakeMd5String(GetMD5(file));
        }

        public static string GetMD5String(Stream stream)
        {
            return MakeMd5String(GetMD5(stream));
        }

        public static string GetMD5String(byte[] buf)
        {
            return MakeMd5String(GetMD5(buf));
        }
        #endregion

        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        public static bool PatternSearch(Stream inStream, string pattern, out string result)
        {
            if (!inStream.CanRead)
                throw new ArgumentException("Stream must be readable");

            var sb = new StringBuilder();
            bool wild = false, found = false;

            int matchLen = 0, cur;
            while ((cur = inStream.ReadByte()) != -1)
            {
                if (wild || pattern[matchLen] == cur)
                {
                    matchLen++;
                    sb.Append((char)cur);

                    if (wild)
                    {
                        found = true;
                        for (int i = 0; i < pattern.Length; i++)
                        {
                            var soFar = sb[sb.Length - pattern.Length + i];
                            if (soFar != pattern[i])
                            {
                                found = false;
                                break;
                            }
                        }

                        if (found)
                            break;
                    }
                }
                else if (pattern[matchLen] == '*')
                {
                    sb.Append((char)cur);

                    pattern = pattern.Substring(matchLen + 1);
                    matchLen = 0;
                    wild = true;
                }
            }

            result = sb.ToString();
            return found;
        }

        public static void CopyFolder(string inFolder, string destFolder)
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
                    var log = DependencyRegistry.Container.GetInstance<ILog>();
                    log.Dual("ERROR: " + file.Replace(inFolder, "") + " did not copy successfully");
                    log.File(error.ToString());
                }
            }
        }

        public static void AssertElevated()
        {
            Trace.Assert(Program.IsElevated, string.Format(Localization.MustBeElevated, Localization.TTW));
        }
    }
}
