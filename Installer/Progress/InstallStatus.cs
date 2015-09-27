using System;
using System.Threading;

namespace TaleOfTwoWastelands.Progress
{
    public class InstallStatus : IInstallStatus
	{
        private readonly IProgress<InstallStatus> _progress;

        public CancellationToken Token { get; }

        private int _itemsDone;
        public int ItemsDone
        {
            get
            {
                return _itemsDone;
            }
            set
            {
                if (_itemsDone != value)
                {
                    _itemsDone = value;
                    Update();
                }
            }
        }

        private int _itemsTotal;
        public int ItemsTotal
        {
            get
            {
                return _itemsTotal;
            }
            set
            {
                if (_itemsTotal != value)
                {
                    _itemsTotal = value;
                    Update();
                }
            }
        }

        private string _currentOperation;
        public string CurrentOperation
        {
            get
            {
                return _currentOperation;
            }
            set
            {
                if (_currentOperation != value)
                {
                    _currentOperation = value;
                    Update();
                }
            }
        }

        public InstallStatus(IProgress<InstallStatus> progress, CancellationToken? token = null)
        {
            Token = token ?? CancellationToken.None;
            _progress = progress;
        }

        public int Step()
        {
            var itemsDone = Interlocked.Increment(ref _itemsDone);
            if (itemsDone > ItemsTotal)
                throw new ArgumentOutOfRangeException();

            Update();
            return itemsDone;
        }

        public void Finish()
        {
            _itemsDone = 0;
            _itemsTotal = 0;
            _currentOperation = "";
            Update();
        }

        private void Update()
        {
            Token.ThrowIfCancellationRequested();
            _progress.Report(this);
        }
    }
}
