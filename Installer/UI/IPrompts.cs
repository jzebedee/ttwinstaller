namespace TaleOfTwoWastelands.UI
{
	public interface IPrompts
	{
		string Fallout3Path { get; }
		string FalloutNVPath { get; }
		string TTWSavePath { get; }

		bool BuildPrompt(string name, string path);
		string Fallout3Prompt(bool manual = false);
		string FalloutNVPrompt(bool manual = false);
		bool OverwritePrompt(string name, string path);
	    bool BuildFOMODsPrompt();
		ErrorPromptResult PatchingErrorPrompt(string file);
		void PromptPaths();
		string TTWPrompt(bool manual = false);
	}
}