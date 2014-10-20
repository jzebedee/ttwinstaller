using System;
using System.Threading;

namespace TaleOfTwoWastelands.ProgressTypes
{
    public class InstallOperation
    {
        private readonly CancellationToken? _token;
        private readonly IProgress<InstallOperation> _progress;

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

        public InstallOperation(IProgress<InstallOperation> progress, CancellationToken? token = null)
        {
            _token = token;
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
            if (_token.HasValue)
                _token.Value.ThrowIfCancellationRequested();

            _progress.Report(this);
        }
    }
}
