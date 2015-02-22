using System.Threading;
using TaleOfTwoWastelands.Progress;

namespace TaleOfTwoWastelands
{
    public interface IInstaller
    {
        /// <summary>
        /// Provides progress updates for minor operations
        /// </summary>
        IProgress<InstallStatus> ProgressMinorOperation { get; set; }

        /// <summary>
        /// Provides progress updates for major operations
        /// </summary>
        IProgress<InstallStatus> ProgressMajorOperation { get; set; }

        void Install(CancellationToken inToken);
    }
}