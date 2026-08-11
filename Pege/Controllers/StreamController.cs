using Microsoft.AspNetCore.Mvc;
using Pege.Entities;
using Pege.Streaming;

namespace Pege.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StreamController(StreamFactory factory) : ControllerBase
    {
        [HttpGet("status/{streamId?}")]
        public async Task<IActionResult> GetStatusAsync(string? streamId)
        {
            return Ok(string.IsNullOrWhiteSpace(streamId)
                ? await factory.ListAsync()
                : await factory.GetStreamStatusAsync(streamId));
        }

        [HttpGet("list/{fileMode?}")]
        public async Task<IActionResult> ListAsync(string? fileMode)
        {
            var mode = fileMode?.Trim().ToLower();
            if (!string.IsNullOrWhiteSpace(mode) && (mode == "csv" || mode == "orig-csv"))
            {
                var fileStream = await factory!.ListAsCsvAsync(mode == "orig-csv");
                return File(fileStream, "text/csv");
            }

            return await GetStatusAsync(null);
        }

        [HttpPost("start/{id?}")]
        public async Task<IActionResult> StartAsync(string? id)
        {
            var streamId = id?.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(streamId))
                await factory.CreateAllAsync();
            else
                await factory.CreateAsync(streamId);

            return Ok();
        }

        [HttpPost("stop/{id?}")]
        public async Task<IActionResult> StopAsync(string? id)
        {
            var streamId = id?.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(streamId))
                await factory.DestroyAllAsync();
            else
                await factory.DestroyAsync(streamId);

            return Ok();
        }

        [HttpPut]
        public async Task<IActionResult> PutStreamAsync([FromBody] StreamStatus streamStatus)
        {
            ;
            return Ok();
        }
    }
}
