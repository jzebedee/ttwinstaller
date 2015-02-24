using System;
using System.Diagnostics;
using Microsoft.Win32;
using TaleOfTwoWastelands.Properties;

namespace TaleOfTwoWastelands
{
    public class SettingsPathStore : IPathStore
	{
        public string GetPathFromKey(string keyName)
        {
			return (string)Settings.Default[keyName];
		}

		public void SetPathFromKey(string keyName, string path)
        {
			Settings.Default[keyName] = path;
			Settings.Default.Save();
		}
	}
}
