namespace Pege.Entities
{
    /// <summary>
    /// Результаты загрузки файлов.
    /// </summary>
    public class UploadResult
    {
        /// <summary>
        /// Общее количество успешно загруженных файлов.
        /// </summary>
        public int TotalUploaded { get; set; }

        /// <summary>
        /// Результаты загрузки каждого файла.
        /// </summary>
        public Dictionary<string, FileUploadResult> Files { get; set; } = [];

    }

    /// <summary>
    /// Результат загрузки файла.
    /// </summary>
    public class FileUploadResult
    {
        /// <summary>
        /// Ошибка или null в случае успеха.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Признак того, что бвл заменён существующий файл.
        /// </summary>
        public bool Replaced { get; set; }
    }
}