using Microsoft.Win32;

namespace TaleOfTwoWastelands
{
	public interface IPathStore
	{
		string GetPathFromKey(string keyName);
		void SetPathFromKey(string keyName, string path);
	}
}