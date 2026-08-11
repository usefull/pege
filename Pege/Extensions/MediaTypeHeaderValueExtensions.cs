using Microsoft.Net.Http.Headers;
using Pege.Resource;

namespace Pege.Extensions
{
    internal static class MediaTypeHeaderValueExtensions
    {
        public static string GetBoundary(this MediaTypeHeaderValue contentType, int lengthLimit)
        {
            var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;
            if (string.IsNullOrWhiteSpace(boundary))
                throw new InvalidDataException(Error.MissingBoundary);
            if (boundary.Length > lengthLimit)
                throw new InvalidDataException(Error.BoundaryLengthExceeded);
            return boundary;
        }
    }
}
