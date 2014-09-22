using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TaleOfTwoWastelands
{
    static class Program
    {
        public static readonly string LogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "TaleOfTwoWastelands");

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            SetupTraceListeners();

            Application.ThreadException += Application_ThreadException;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UI.frm_Main());
        }

        static void SetupTraceListeners()
        {
            Directory.CreateDirectory(LogDirectory);

            var logFilename = "Install Log " + DateTime.Now.ToString("MM_dd_yyyy - HH_mm_ss") + ".txt";
            var logFilepath = Path.Combine(LogDirectory, logFilename);

            Trace.AutoFlush = true;
            Trace.Listeners.Add(new FinalizedLogTraceListener(logFilepath));
        }

        static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            HandleCrashException(e.Exception);
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            HandleCrashException((Exception)e.ExceptionObject);
        }

        static void HandleCrashException(Exception e)
        {
            Trace.WriteLine("An uncaught exception occurred: " + e);
            MessageBox.Show("An uncaught exception occurred and the program will now exit. Please submit a crash report with your installation log.");
            Environment.Exit(1);
        }
    }
}