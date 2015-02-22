namespace TaleOfTwoWastelands.UI
{
    public interface IPrompts
    {
        string Fallout3Path { get; }
        string FalloutNVPath { get; }
        string TTWSavePath { get; }
        void PromptPaths();
        string Fallout3Prompt(bool manual = false);
        string FalloutNVPrompt(bool manual = false);
        string TTWPrompt(bool manual = false);
    }
}