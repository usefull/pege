using Microsoft.Net.Http.Headers;

namespace Pege.Extensions
{
    internal static class ContentDispositionHeaderValueExtensions
    {
        public static bool HasFileContentDisposition(this ContentDispositionHeaderValue contentDisposition) =>
            contentDisposition != null
            && contentDisposition.DispositionType.Equals("form-data")
            && (!string.IsNullOrEmpty(contentDisposition.FileName.Value) || !string.IsNullOrEmpty(contentDisposition.FileNameStar.Value));
    }
}