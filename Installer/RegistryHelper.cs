using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace TaleOfTwoWastelands
{
    public static class RegistryHelper
    {
        public static RegistryKey GetBethKey()
        {
            using (var bethKey =
                Registry.LocalMachine.OpenSubKey(
                //determine software reg path (depends on architecture)
                Environment.Is64BitOperatingSystem ? "Software\\Wow6432Node" : "Software", RegistryKeyPermissionCheck.ReadWriteSubTree))
            //create or retrieve BethSoft path
            {
                Debug.Assert(bethKey != null, "bethKey != null");
                return bethKey.CreateSubKey("Bethesda Softworks", RegistryKeyPermissionCheck.ReadWriteSubTree);
            }
        }

        public static string GetPathFromKey(string keyName)
        {
            using (var bethKey = GetBethKey())
            using (var subKey = bethKey.CreateSubKey(keyName))
            {
                Debug.Assert(subKey != null, "subKey != null");
                return subKey.GetValue("Installed Path", "").ToString();
            }
        }

        public static void SetPathFromKey(string keyName, string path)
        {
            using (var bethKey = GetBethKey())
            using (var subKey = bethKey.CreateSubKey(keyName))
            {
                Debug.Assert(subKey != null, "subKey != null");
                subKey.SetValue("Installed Path", path, RegistryValueKind.String);
            }
        }

    }
}
