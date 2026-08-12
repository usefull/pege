using System.Collections.Concurrent;

namespace Pege.Services
{
    /// <summary>
    /// Менеджер блокировок файлов.
    /// </summary>
    internal class FileLockManager
    {
        private readonly ConcurrentDictionary<string, object> _locks = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Метод возвращает уникальный объект блокировки для конкретного пути файла.
        /// </summary>
        public object GetLock(string filePath)
        {
            string key = Path.GetFullPath(filePath);
            return _locks.GetOrAdd(key, _ => new object());
        }
    }
}
