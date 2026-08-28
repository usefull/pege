using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Pege.Resource;

namespace Pege.Controllers
{
    [ApiController]
    [Route("/", Order = 100)]
    public class HomeController(IConfiguration config, ILogger<HomeController> log) : ControllerBase
    {
        [HttpGet("{**path}")]
        public async Task<IActionResult> GetAsset(string? path)
        {
            var root = config["ClientAppPath"];
            if (string.IsNullOrWhiteSpace(root))
            {
                log.LogError(Error.ClientAppPathNotDefined);
                return NotFound(string.Empty);
            }

            var filename = Path.Combine(root, string.IsNullOrWhiteSpace(path) ? "index.html" : path);
            if (!System.IO.File.Exists(filename))
                return NotFound(string.Empty);

            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(filename, out var mimeType))
                mimeType = "application/octet-stream";

            var exactFileName = Path.GetFileName(filename).ToLowerInvariant();

            if (exactFileName == "index.html" || exactFileName.Contains("sw.js"))
            {
                Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                Response.Headers.Pragma = "no-cache";
                Response.Headers.Expires = "0";
            }
            else
            {
                Response.Headers.CacheControl = "public, max-age=604800";
            }

            var stream = System.IO.File.OpenRead(filename);
            return File(stream, mimeType);
        }
    }
}
