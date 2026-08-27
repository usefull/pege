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
    /// <summary>
    /// Функционал обработки запросов загрузки и удаления треков.
    /// </summary>
    /// <param name="factory"></param>
    [ApiController]
    [Route("api/stream/{streamId}")]
    public class UploadController(StreamFactory factory) : ControllerBase
    {
        /// <summary>
        /// Метод загрузки треков в плей-лист радиопотока.
        /// </summary>
        /// <remarks>Имя загружаемого файла нормализуется: множественные пробелы заменяются единичным.
        /// Если в строке запроса есть буква q(Q), то загрузка будет выполнена "тихо", без публикации в Tg-канале.
        /// Если в строке запроса есть буква r(R), то существующие файлы будут заменены иначе - ошибка загрузки на данном файле.
        /// Причём расширение файлов не учитывается, т.е. будут удалены все файлы, у которых совпадает имя до точки расширения, и будет загружен новый файл.
        /// Пример запроса флагами q и r: ?qr</remarks>
        /// <returns>Результаты загрузки файлов.</returns>
        /// <exception cref="ValidationException">В случае, если поток не поддерживает загрузку.</exception>
        [HttpPatch]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> Upload()
        {
            // Читаем идентификатор потока и строку с параметрами запроса из сегментов пути.
            // ВАЖНО!!! Идентификатор потока и строка с параметрами запроса не передаются как параметры метода,
            // т.к. это ломает потоковую передачу загружаемых файлов без предварительного сохранения во временных файлах.
            var streamId = RouteData.Values["streamId"]?.ToString();

            var queryString = HttpContext.Request.QueryString.ToString().ToLower();
            var quietly = queryString.Contains('q');
            var replace = queryString.Contains('r');

            if (!(Request.ContentType?.IsMultipartContentType() ?? false))
                throw new ValidationException(Error.MultipartFormDataRequired);

            if (await factory.GetStreamAsync(streamId!) is not IFileUploader stream)
                throw new ValidationException(Error.StreamDoesntSupportUploading);

            var boundary = MediaTypeHeaderValue.Parse(Request.ContentType).GetBoundary(70);
            var reader = new MultipartReader(boundary, HttpContext.Request.Body);

            return Ok(await stream.UploadAsync(reader, quietly, replace, HttpContext.RequestAborted));
        }

        /// <summary>
        /// Метод удаления трека из плейлиста.
        /// </summary>
        /// <param name="streamId">Идентификатор потока.</param>
        /// <param name="filename">Имя файла, можно без расширения.</param>
        /// <remarks>Будут удалены все файлы, найденные по имени до точки расширения.</remarks>
        /// <returns>Результат операции.</returns>
        /// <exception cref="ValidationException">В случае, если поток не поддерживает загрузку.</exception>
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
