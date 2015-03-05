using System.IO;
using System.Threading;
using TaleOfTwoWastelands.Progress;
using TaleOfTwoWastelands.Properties;
using System.Linq;
using TaleOfTwoWastelands.UI;

namespace TaleOfTwoWastelands.Install
{
    class BuildFOMODsStep : IInstallStep
    {
        private readonly IInstaller _installer;
        private readonly ILog Log;
        private readonly IPrompts _prompts;

        private readonly FOMOD _fomod;

        public BuildFOMODsStep(IInstaller installer, ILog log, IPrompts prompts)
        {
            Log = log;
            _installer = installer;
            _prompts = prompts;
            _fomod = DependencyRegistry.Container.GetInstance<FOMOD>();
        }

        public bool? Run(IInstallStatusUpdate status, CancellationToken token)
        {
            if (!_prompts.BuildFOMODsPrompt())
                return false;

            //+1 (opt)
            status.ItemsTotal++;
            status.CurrentOperation = "Building FOMODs";

            var fomodStatus = new InstallStatus(_installer.ProgressMinorOperation, _installer.Token);
            _fomod.BuildAll(fomodStatus, _installer.DirTTWMain, _installer.DirTTWOptional, _prompts.TTWSavePath);

            return true;
        }
    }
}
