using System.Threading;

namespace TaleOfTwoWastelands.Progress
{
	public interface IInstallStatus
	{
		string CurrentOperation { get; set; }
		int ItemsDone { get; set; }
		int ItemsTotal { get; set; }
		CancellationToken Token { get; }

		void Finish();
		int Step();
	}
}