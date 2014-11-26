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
        private const string
            NvseFile = "nvse_loader.exe",
            NvseLink = @"http://nvse.silverlock.org/",
            NvseSearch = @"http://nvse.silverlock.org/download/*.7z";

        private readonly string _fnvPath;
        public NVSE(string FNVPath)
        {
            _fnvPath = FNVPath;
        }

        public bool Check()
        {
            var nvseLoader = Path.Combine(_fnvPath, NvseFile);
            if (File.Exists(nvseLoader))
            {
                Log.File("NVSE found");
                return true;
            }

            Log.File("NVSE missing");
            return false;
        }

        public bool? Prompt()
        {
            var dlgResult = MessageBox.Show(Resources.NVSE_InstallPrompt, "NVSE missing", MessageBoxButtons.YesNoCancel);
            switch (dlgResult)
            {
                case DialogResult.Yes:
                    return true;
                case DialogResult.No:
                    Log.Dual("Proceeding without NVSE.");
                    Log.Display("NVSE must be installed before playing!");
                    return false;
                case DialogResult.Cancel:
                    Log.File("Install cancelled due to NVSE requirement");
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
                Log.File("Requesting NVSE page at " + NvseLink);

                string dlLink;
                using (var resStream = wc.OpenRead(NvseLink))
                {
                    if (!Util.PatternSearch(resStream, NvseSearch, out dlLink))
                    {
                        err = "Failed to download NVSE.";
                        return false;
                    }
                }

                Log.File("Parsed NVSE link: " + dlLink.Truncate(100));

                var archiveName = Path.GetFileName(dlLink);
                var tmpPath = Path.Combine(Path.GetTempPath(), archiveName);
                wc.DownloadFile(dlLink, tmpPath);

                using (var lzExtract = new SevenZipExtractor(tmpPath))
                {
                    if (!lzExtract.Check())
                    {
                        err = archiveName + " is an invalid 7z archive.";
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
                        Log.File("Extracting " + a.filename);

                        using (var fsStream = File.OpenWrite(savePath))
                        {
                            try
                            {
                                lzExtract.ExtractFile(a.file, fsStream);
                            }
                            catch
                            {
                                err = "Failed to extract NVSE.";
                                throw;
                            }
                        }
                    }
                }
            }

            Log.Dual("NVSE was installed successfully.");
            return true;
        }
    }
}
