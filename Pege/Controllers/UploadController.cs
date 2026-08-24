using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Pege.Extensions;
using Pege.Interfaces;
using Pege.Resource;
using Pege.Streaming;
using System.ComponentModel.DataAnnotations;

namespace Pege.Controllers
{
    [ApiController]
    [Route("api/stream/{streamId}")]
    public class UploadController(StreamFactory factory) : ControllerBase
    {
        /// <summary>
        /// Метод загрузки треков в плей-лист радиопотока.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ApplicationException"></exception>
#pragma warning disable ASP0018 // Unused route parameter
        [HttpPatch("{quietly?}")]
#pragma warning restore ASP0018 // Unused route parameter
        [DisableRequestSizeLimit]
        public async Task<IActionResult> Upload()
        {
            // Читаем идентификатор потока и параметр "quietly" из сегментов пути.
            // ВАЖНО!!! Идентификатор потока и параметр "quietly" не передаются как параметры метода,
            // т.к. это ломает потоковую передачу загружаемых файлов без предварительного сохранения во временных файлах.
            var streamId = RouteData.Values["streamId"]?.ToString();
            var quietly = RouteData.Values["quietly"]?.ToString() == "q";

            if (!(Request.ContentType?.IsMultipartContentType() ?? false))
                throw new ValidationException(Error.MultipartFormDataRequired);

            if (await factory.GetStreamAsync(streamId!) is not IFileUploader stream)
                throw new ValidationException(Error.StreamDoesntSupportUploading);

            var boundary = MediaTypeHeaderValue.Parse(Request.ContentType).GetBoundary(70);
            var reader = new MultipartReader(boundary, HttpContext.Request.Body);

            return Ok(await stream.UploadAsync(reader, quietly, HttpContext.RequestAborted));
        }

        [HttpDelete("{filename}")]
        public async Task<IActionResult> Delete(string streamId, string filename)
        {
            if (await factory.GetStreamAsync(streamId) is not IFileUploader stream)
                throw new ValidationException(Error.StreamDoesntSupportUploading);

            await stream.DeleteTrackAsync(filename);

            return Ok();
        }
    }
}
