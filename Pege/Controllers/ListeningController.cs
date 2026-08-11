using Microsoft.AspNetCore.Mvc;
using Pege.Entities;
using Pege.Interfaces;
using Pege.Resource;
using Pege.Streaming;

namespace Pege.Controllers
{
    [Route("stream/{streamId}")]
    [ApiController]
    public class ListeningController(IServiceProvider serviceProvider) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get(string streamId)
        {
            var streamFactory = serviceProvider.GetRequiredService<StreamFactory>();

            var stream = streamFactory[streamId];

            var streamType = stream.GetType();
            Type? chunkType = null;
            while (streamType != null && streamType != typeof(object))
            {
                if (streamType.IsGenericType && streamType.GetGenericTypeDefinition().Name.StartsWith("Stream`2"))
                {
                    chunkType = streamType.GetGenericArguments()[1];
                    break;
                }
                streamType = streamType.BaseType;
            }

            if (chunkType == null)
                throw new InvalidOperationException(Error.UnknownStreamType);

            IConnector connector;
            if (chunkType == typeof(AudioChunk))
            {
                connector = serviceProvider.GetRequiredService<AudioStreamConnector>();
            }
            else
                throw new InvalidOperationException(Error.UnknownStreamType);

            var feature = HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
            feature?.DisableBuffering();

            await connector.ConsumeAsync(stream, Request, Response, HttpContext.RequestAborted);

            return new EmptyResult();
        }
    }
}
