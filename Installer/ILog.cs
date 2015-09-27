using System;

namespace TaleOfTwoWastelands
{
    public interface ILog
    {
        IProgress<string> DisplayMessage { get; set; }

        void File(string msg, params object[] args);
        void Display(string msg, params object[] args);
        void Dual(string msg, params object[] args);
    }
}