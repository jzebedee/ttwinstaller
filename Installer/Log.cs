using System;
using System.Diagnostics;
using System.Text;
using TaleOfTwoWastelands.ProgressTypes;

namespace TaleOfTwoWastelands
{
    internal static class Log
    {
        private static readonly StringBuilder sb = new StringBuilder();
        private static string Timestamp
        {
            get
            {
                return sb
                    .Clear()
                    .Append('[')
                    .Append(DateTime.Now)
                    .Append(']')
                    .Append('\t')
                    .ToString();
            }
        }

        public static void File(string msg, params object[] args)
        {
            Trace.Write(Timestamp);
            Trace.WriteLine(string.Format(msg, args));
        }

        public static IProgress<string> DisplayMessage { get; set; }
        public static void Display(string msg, params object[] args)
        {
            Debug.Assert(DisplayMessage != null, "shouldn't call Display before setting DisplayMessage");

            var displayProg = DisplayMessage;
            if (displayProg != null)
            {
                var sb = new StringBuilder(Timestamp);
                sb.AppendFormat(msg, args);
                displayProg.Report(sb.ToString());
            }
        }

        public static void Dual(string msg, params object[] args)
        {
            File(msg, args);
            Display(msg, args);
        }
    }
}
