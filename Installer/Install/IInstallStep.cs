using System.Threading;
using TaleOfTwoWastelands.Progress;

namespace TaleOfTwoWastelands.Install
{
	interface IInstallStep
	{
		bool? Run(InstallStatus status, CancellationToken token);
	}
}
