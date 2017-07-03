using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GZipWorker
{
    public class CustomThreadPool : IDisposable
    {
        private readonly Queue<Action> _actions = new Queue<Action>();
        private readonly List<Thread> _threads = new List<Thread>();
        private bool _disallowAdd;
        private bool _disposed;

        public CustomThreadPool(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var worker = new Thread(BackgroundWorker) {Name = string.Format("Thread_{0}", i)};
                worker.Start();
                _threads.Add(worker);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            var waitForThreads = false;
            lock (_actions)
            {
                if (!_disposed)
                {
                    _disallowAdd = true;
                    while (_actions.Count > 0)
                    {
                        Monitor.Wait(_actions);
                    }
                    _disposed = true;
                    Monitor.PulseAll(_actions);
                    waitForThreads = true;
                }

                if (waitForThreads)
                {
                    foreach (var thread in _threads)
                    {
                        thread.Join();
                    }
                }
            }
        }

        public void Abort()
        {
            foreach (var thread in _threads)
            {
                thread.Abort();
            }
        }
        public void QueueTask(Action task)
        {
            lock (_actions)
            {
                if (_disallowAdd)
                {
                    throw new InvalidOperationException(
                        "This Pool instance is in the process of being disposed, can't add anymore");
                }
                if (_disposed)
                {
                    throw new ObjectDisposedException("This Pool instance has already been disposed");
                }
                _actions.Enqueue(task);
                Monitor.PulseAll(_actions);
            }
        }
        private void BackgroundWorker()
        {
            while (true)
            {
                Action task;
                lock (_actions)
                {
                    while (true)
                    {
                        if (_disposed)
                        {
                            return;
                        }
                        var currentThread = _threads.FirstOrDefault();
                        if (currentThread != null && ReferenceEquals(Thread.CurrentThread, currentThread) &&
                            _actions.Count > 0)
                        {
                            task = _actions.Dequeue();
                            _threads.Remove(currentThread);
                            Monitor.PulseAll(_actions);
                            break;
                        }
                        Monitor.Wait(_actions);
                    }
                }
                task();
                lock (_actions)
                {
                    _threads.Add(Thread.CurrentThread);
                }
            }
        }
    }
}