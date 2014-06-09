#define CAPTURE_STDERR
#define CAPTURE_STDOUT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using System.Threading.Tasks;

namespace TaleOfTwoWastelands
{
    public static class BSAOpt
    {
        static readonly string sysArch = Environment.Is64BitOperatingSystem ? "64" : "32";

        static private Task RunAsync(IProgress<string> progress, CancellationToken token, string inArg, string outArg)
        {
            var bsaOpt = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(Installer.AssetsDir, "BSAOpt", "BSAOpt x" + sysArch + ".exe"),
                    Arguments = " -deployment -game fo -compress 10 -criticals \"" + inArg + "\" \"" + outArg + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardInput = true,
#if CAPTURE_STDOUT
                    RedirectStandardOutput = true,
#endif
#if CAPTURE_STDERR
                    RedirectStandardError = true,
#endif
                }
            };
#if CAPTURE_STDERR
            bsaOpt.ErrorDataReceived += (sender, e) => progress.Report("[err]" + e.Data);
#endif
#if CAPTURE_STDOUT
            bsaOpt.OutputDataReceived += (sender, e) => progress.Report("[out]" + e.Data);
#endif

            bsaOpt.Start();
#if CAPTURE_STDOUT
            bsaOpt.BeginOutputReadLine();
#endif
#if CAPTURE_STDERR
            bsaOpt.BeginErrorReadLine();
#endif

            var processMRE = new ManualResetEvent(false);
            processMRE.SafeWaitHandle = new SafeWaitHandle(bsaOpt.Handle, false);

            RegisteredWaitHandle procWaitHandle = null;
            procWaitHandle = ThreadPool.RegisterWaitForSingleObject(processMRE, (state, timedOut) =>
            {
                var finish = token.IsCancellationRequested || !timedOut;
                if (finish)
                {
                    procWaitHandle.Unregister(null);

                    if (token.IsCancellationRequested)
                    {
                        progress.Report("[BSAopt] Process killed from cancellation.");
                        bsaOpt.Kill();
                    }
                    processMRE.Dispose();
                    bsaOpt.Dispose();
                }
                else
                    bsaOpt.StandardInput.WriteLine();
            }, null, 1000, false);

            var task = new Task(() => processMRE.WaitOne());
            task.Start();

            return task;
        }

#if ASYNC
        static public async Task BuildBSA_Async(IProgress<string> progress, CancellationToken token, string inDirectory, string outFile)
#else
        static public void BuildBSA(IProgress<string> progress, CancellationToken token, string inDirectory, string outFile)
#endif
        {
#if ASYNC
            await RunAsync(progress, token, inDirectory, outFile);
#else
            var runTask = RunAsync(progress, token, inDirectory, outFile);
            runTask.Wait(token);
#endif
            Directory.Delete(inDirectory, true);
            Directory.CreateDirectory(inDirectory);
        }

#if ASYNC
        static public async Task ExtractBSA_Async(IProgress<string> progress, CancellationToken token, string inBSA, string outDir)
#else
        static public void ExtractBSA(IProgress<string> progress, CancellationToken token, string inBSA, string outDir)
#endif
        {
#if ASYNC
            await RunAsync(progress, token, inBSA, outDir);
#else
            var runTask = RunAsync(progress, token, inBSA, outDir);
            runTask.Wait(token);
#endif
        }
    }
}
