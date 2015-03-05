using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace TaleOfTwoWastelands.Progress
{
    public interface IInstallStatusUpdate
    {
        string CurrentOperation { get; set; }
        int ItemsDone { get; set; }
        int ItemsTotal { get; set; }
        CancellationToken Token { get; }
        int Step();
    }
}
