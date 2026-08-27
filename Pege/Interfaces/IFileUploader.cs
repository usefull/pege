using Microsoft.AspNetCore.WebUtilities;
using Pege.Entities;

namespace Pege.Interfaces
{
    public interface IFileUploader
    {
        Task<UploadResult> UploadAsync(MultipartReader reader, bool quietly, bool replace, CancellationToken cancellationToken);

        Task DeleteTrackAsync(string fileName);
    }
}
