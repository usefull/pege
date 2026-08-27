namespace Pege.Entities
{
    public class UploadResult
    {
        public int TotalUploaded { get; set; }
        public Dictionary<string, FileUploadResult> Files { get; set; } = [];

    }

    public class FileUploadResult
    {
        public string? Error { get; set; }

        public bool Replaced { get; set; }
    }
}