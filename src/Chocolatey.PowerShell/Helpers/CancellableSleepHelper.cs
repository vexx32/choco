using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Chocolatey.PowerShell.Helpers
{
    internal class CancellableSleepHelper : IDisposable
    {
        private readonly object _lock = new object();

        private bool _disposed;
        private bool _stopping;
        private ManualResetEvent _waitHandle;

        public void Dispose()
        {
            if (!_disposed)
            {
                _waitHandle.Dispose();
                _waitHandle = null;
            }

            _disposed = true;
        }

        // Call from Cmdlet.StopProcessing()
        internal void Cancel()
        {
            _stopping = true;
            _waitHandle?.Set();
        }

        internal void Sleep(int milliseconds)
        {
            lock (_lock)
            {
                if (!_stopping)
                {
                    _waitHandle = new ManualResetEvent(false);
                }
            }

            _waitHandle?.WaitOne(milliseconds, true);
        }

    }
}
