using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime;
using System.Threading;
using System.Runtime.InteropServices;

namespace TaleOfTwoWastelands
{
    public interface IProgress<in T>
    {
        void Report(T value);
    }

    /// <summary>
    /// This is a compatibility class that is only used when compiled below .NET 4.5. It does NOT maintain synchronization context like the real System.Progress.
    /// </summary>
    /// <typeparam name="T">A string used to carry report progress</typeparam>
    public class Progress<T> : IProgress<T>
    {
        private readonly Action<T> m_handler;
        private readonly SendOrPostCallback m_invokeHandlers;
        private readonly SynchronizationContext m_synchronizationContext;

        [SerializableAttribute]
        [ComVisibleAttribute(true)]
        public delegate void GenericEventHandler<T>(Object sender, T e);

        public event GenericEventHandler<T> ProgressChanged;

        public Progress()
        {
            this.m_synchronizationContext = SynchronizationContext.Current;
            this.m_invokeHandlers = new SendOrPostCallback(this.InvokeHandlers);
        }

        public Progress(Action<T> handler)
            : this()
        {
            if (handler == null)
            {
                throw new ArgumentNullException("handler");
            }
            this.m_handler = handler;
        }

        private void InvokeHandlers(object state)
        {
            T local = (T)state;
            Action<T> action = this.m_handler;
            GenericEventHandler<T> progressChanged = this.ProgressChanged;
            if (action != null)
            {
                action(local);
            }
            if (progressChanged != null)
            {
                progressChanged(this, local);
            }
        }

        protected virtual void OnReport(T value)
        {
            Action<T> action = this.m_handler;
            GenericEventHandler<T> progressChanged = this.ProgressChanged;
            if ((action != null) || (progressChanged != null))
            {
                this.m_synchronizationContext.Post(this.m_invokeHandlers, value);
            }
        }

        [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
        void IProgress<T>.Report(T value)
        {
            this.OnReport(value);
        }
    }
}