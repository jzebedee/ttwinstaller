using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace TaleOfTwoWastelands.ProgressTypes
{
    public class OperationProgress : BaseNotificationModel
    {
        public OperationProgress(IProgress<OperationProgress> progress = null, CancellationToken? token = null)
        {
            if (progress == null && !token.HasValue)
                return;

            PropertyChanged += (sender, e) =>
            {
                if (progress != null)
                    progress.Report(this);
                if (token.HasValue)
                    token.Value.ThrowIfCancellationRequested();
            };
        }

        public int Step()
        {
            var itemsDone = Interlocked.Increment(ref _itemsDone);
            if (itemsDone > ItemsTotal)
                throw new ArgumentOutOfRangeException();

            RaisePropertyChanged("ItemsDone");
            return itemsDone;
        }

        public void Finish()
        {
            _itemsDone = 0;
            _itemsTotal = 0;
            _currentOperation = "";
            RaisePropertyChanged("ItemsDone");
            RaisePropertyChanged("ItemsTotal");
            RaisePropertyChanged("CurrentOperation");
        }

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
                    RaisePropertyChanged("ItemsDone");
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
                    RaisePropertyChanged("ItemsTotal");
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
                    RaisePropertyChanged("CurrentOperation");
                }
            }
        }
    }
}