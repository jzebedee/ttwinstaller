using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;

namespace TaleOfTwoWastelands.ProgressTypes
{
    [Flags]
    public enum ChangeType
    {
        None,

        ItemsDone,
        ItemsTotal,
        CurrentOperation,

        All = ItemsDone | ItemsTotal | CurrentOperation
    }

    public struct OperationProgress
    {
        public OperationProgress(int itemsDone, int itemsTotal, string currentOperation)
        {
            this.ItemsDone = itemsDone;
            this.ItemsTotal = itemsTotal;
            this.CurrentOperation = currentOperation;
        }

        public int ItemsDone;
        public int ItemsTotal;
        public string CurrentOperation;
    }

    public struct OperationProgressUpdate
    {
        public OperationProgressUpdate(OperationProgress progress, ChangeType change)
        {
            this.Progress = progress;
            this.Change = change;
        }

        public readonly OperationProgress Progress;
        public readonly ChangeType Change;
    }

    public class InstallOperation
    {
        private readonly CancellationToken? _token;
        private readonly IProgress<OperationProgressUpdate> _progress;

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
                    Update(ChangeType.ItemsDone);
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
                    Update(ChangeType.ItemsTotal);
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
                    Update(ChangeType.CurrentOperation);
                }
            }
        }

        public InstallOperation(IProgress<OperationProgressUpdate> progress, CancellationToken? token = null)
        {
            this._token = token;
            this._progress = progress;
        }

        public int Step()
        {
            var itemsDone = Interlocked.Increment(ref _itemsDone);
            if (itemsDone > ItemsTotal)
                throw new ArgumentOutOfRangeException();

            Update(ChangeType.ItemsDone);
            return itemsDone;
        }

        public void Finish()
        {
            _itemsDone = 0;
            _itemsTotal = 0;
            _currentOperation = "";
            Update(ChangeType.All);
        }

        private OperationProgressUpdate CreateUpdate(ChangeType change)
        {
            var currentProgress = new OperationProgress(ItemsDone, ItemsTotal, CurrentOperation);
            return new OperationProgressUpdate(currentProgress, change);
        }

        private void Update(ChangeType change)
        {
            if (_token.HasValue)
                _token.Value.ThrowIfCancellationRequested();

            _progress.Report(CreateUpdate(change));
        }
    }
}
