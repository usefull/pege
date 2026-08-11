using Microsoft.AspNetCore.Mvc;
using Pege.Resource;
using Pege.Services;
using Pege.Streaming;
using Serilog;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Pege.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BotController(StreamFactory factory, TelegramService tgService) : ControllerBase
    {
        [HttpPost("command")]
        public async Task<IActionResult> Command([FromBody] Update update)
        {
            if (update.Type != UpdateType.Message)
                return Ok();

            try
            {

                var chatId = new ChatId(update.Message?.Chat?.Id ?? 0);

                switch (update.Message?.Text?.ToLower())
                {
                    case "/stream_list":
                        await tgService.SendFileAsync(await factory.ListAsCsvAsync(), "streamlist.csv", chatId);
                        break;
                    case "/orig_stream_list":
                        await tgService.SendFileAsync(await factory.ListAsCsvAsync(true), "streamlist.csv", chatId);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Format(Error.BotCommandHandlingError, update.Message?.Text?.ToLower(), ex.Message));
            }

            return Ok();
        }
    }
}
