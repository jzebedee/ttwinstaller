using System;
using System.Diagnostics;

namespace TaleOfTwoWastelands
{
    internal class Log : ILog
    {
        private static IFormattable Timestamp => $"[{DateTime.Now}]\t";

        public IProgress<string> DisplayMessage { get; set; }
        
        public void File(string msg, params object[] args)
        {
            Trace.Write(Timestamp);
            Trace.WriteLine(string.Format(msg, args));
        }

        public void Display(string msg, params object[] args)
        {
            Debug.Assert(DisplayMessage != null, "shouldn't call Display before setting DisplayMessage");

            var displayProg = DisplayMessage;
            if (displayProg != null)
            {
                var sb = Timestamp + string.Format(msg, args);
                displayProg.Report(sb);
            }
        }

        public void Dual(string msg, params object[] args)
        {
            File(msg, args);
            Display(msg, args);
        }
    }
}
