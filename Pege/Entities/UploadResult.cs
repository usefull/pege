namespace Pege.Entities
{
    public class UploadResult
    {
        public int TotalUploaded { get; set; }
        public Dictionary<string, string?> Errors { get; set; } = [];

    }
}