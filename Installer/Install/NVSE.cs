using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using SevenZip;
using TaleOfTwoWastelands.Properties;

namespace TaleOfTwoWastelands.Install
{
    internal class NVSE
    {
		private readonly string _fnvPath;
        private readonly ILog Log;

        public NVSE(string FNVPath, ILog log)
        {
            Log = log;
            _fnvPath = FNVPath;
        }

        public bool Check()
        {
            var nvseLoader = Path.Combine(_fnvPath, Paths.NvseFile);
            if (File.Exists(nvseLoader))
            {
                Log.File(Localization.NvseFound);
                return true;
            }

            Log.File(Localization.NvseMissing);
            return false;
        }

        public bool? Prompt()
        {
            var dlgResult = MessageBox.Show(Localization.NvseInstallPrompt, Localization.NvseMissing, MessageBoxButtons.YesNoCancel);
            switch (dlgResult)
            {
                case DialogResult.Yes:
                    return true;
                case DialogResult.No:
                    Log.Dual(Localization.ProceedingWithoutNvse);
                    Log.Display(Localization.NvseMustBeInstalled);
                    return false;
                case DialogResult.Cancel:
                    Log.File(Localization.InstallCancelledNvse);
                    break;
            }

            return null;
        }

        //where's my async?
        public bool Install(out string err)
        {
            err = null;

            using (var wc = new WebClient())
            {
                Log.File(Localization.RequestingNvsePage, Paths.NvseLink);

                string dlLink;
                using (var resStream = wc.OpenRead(Paths.NvseLink))
                {
                    if (!Util.PatternSearch(resStream, Paths.NvseSearchPattern, out dlLink))
                    {
                        err = Localization.FailedDownloadNvse;
                        return false;
                    }
                }

                Log.File(Localization.ParsedNvseLink, dlLink.Truncate(100));

                var archiveName = Path.GetFileName(dlLink);
                var tmpPath = Path.Combine(Path.GetTempPath(), archiveName);
                wc.DownloadFile(dlLink, tmpPath);

                using (var lzExtract = new SevenZipExtractor(tmpPath))
                {
                    if (!lzExtract.Check())
                    {
                        err = string.Format(Localization.Invalid7zArchive, archiveName);
                        return false;
                    }

                    var wantedFiles = (from file in lzExtract.ArchiveFileNames
                                       let filename = Path.GetFileName(file)
                                       let ext = Path.GetExtension(filename).ToUpperInvariant()
                                       where ext == ".EXE" || ext == ".DLL"
                                       select new { file, filename }).ToArray();

                    foreach (var a in wantedFiles)
                    {
                        var savePath = Path.Combine(_fnvPath, a.filename);
                        Log.File(Localization.Extracting, a.filename);

                        using (var fsStream = File.OpenWrite(savePath))
                        {
                            try
                            {
                                lzExtract.ExtractFile(a.file, fsStream);
                            }
                            catch
                            {
                                err = Localization.FailedExtractNvse;
                                throw;
                            }
                        }
                    }
                }
            }

            Log.Dual(Localization.NvseInstallSuccessful);
            return true;
        }
    }
}
