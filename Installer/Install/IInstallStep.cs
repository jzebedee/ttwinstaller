using System.Threading;
using TaleOfTwoWastelands.Progress;

namespace TaleOfTwoWastelands.Install
{
	interface IInstallStep
	{
		bool? Run(IInstallStatusUpdate status, CancellationToken token);
	}
}
