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

        [HttpPut("start/{id?}")]
        public async Task<IActionResult> StartAsync(string? id)
        {
            var streamId = id?.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(streamId))
                await factory.CreateAllAsync();
            else
                await factory.CreateAsync(streamId);

            return Ok();
        }

        [HttpPut("stop/{id?}")]
        public async Task<IActionResult> StopAsync(string? id)
        {
            var streamId = id?.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(streamId))
                await factory.DestroyAllAsync();
            else
                await factory.DestroyAsync(streamId);

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> PostStreamAsync([FromBody] StreamDescriptor streamStatus)
        {
            var info = streamStatus.ToInfo();
            var streamInfo = await factory.RegisterAsync(info);
            return Ok(streamInfo);
        }

        [HttpPut]
        public async Task<IActionResult> PutStreamAsync([FromBody] StreamDescriptor streamDescription)
        {
            var info = streamDescription.ToInfo();
            var streamInfo = await factory.UpdateAsync(info);
            return Ok(streamInfo);
        }

        [HttpDelete("{streamId}")]
        public async Task<IActionResult> DeleteStreamAsync(string streamId)
        {
            await factory.DeleteAsync(streamId);
            return Ok();
        }
    }
}
