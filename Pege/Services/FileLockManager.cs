using System.Collections.Concurrent;

namespace Pege.Services
{
    /// <summary>
    /// Менеджер блокировок файлов.
    /// </summary>
    internal class FileLockManager
    {
        private readonly ConcurrentDictionary<string, RefCountedSemaphore> _locks = new(StringComparer.OrdinalIgnoreCase);

        public async Task<IDisposable> AcquireLockAsync(string filePath)
        {
            string key = Path.GetFullPath(filePath);
            RefCountedSemaphore item;

            lock (_locks)
            {
                item = _locks.GetOrAdd(key, _ => new RefCountedSemaphore());
                item.RefCount++;
            }

            await item.Semaphore.WaitAsync();

            return new Releaser(this, key, item);
        }

        private class RefCountedSemaphore
        {
            public readonly SemaphoreSlim Semaphore = new(1, 1);
            public int RefCount;
        }

        private class Releaser(FileLockManager manager, string key, RefCountedSemaphore item) : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                item.Semaphore.Release();

                lock (manager._locks)
                {
                    item.RefCount--;
                    if (item.RefCount == 0)
                    {
                        manager._locks.TryRemove(key, out _);
                        item.Semaphore.Dispose();
                    }
                }
            }
        }
    }

}