using Pege.Resource;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Pege.Services
{
    public class TelegramService
    {
        private readonly TelegramBotClient? _botClient;

        public TelegramService(string? botToken)
        {
            try
            {
                _botClient = new(botToken!);
            }
            catch (Exception ex)
            {
                Log.Error(string.Format(Error.TelegramServiceInitializingError, ex.Message));
            }
        }

        public async Task<Telegram.Bot.Types.Message?> SendMessageAsync(string message, string channelId)
        {
            try
            {
                return await _botClient!.SendMessage(
                    chatId: new ChatId(channelId),
                    text: message,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                    cancellationToken: CancellationToken.None
                );
            }
            catch (Exception ex)
            {
                Log.Error(string.Format(Error.TelegramMessageSendError, ex.Message));
                return null;
            }
        }

        public async Task<bool> DeleteMessageAsync(int messageId, string channelId)
        {
            try
            {
                await _botClient!.DeleteMessage(new ChatId(channelId), messageId, CancellationToken.None);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(string.Format(Error.TelegramMessageDeleteError, ex.Message));
                return false;
            }
        }

        public async Task SendFileAsync(Stream file, string fileName, string channelId) =>
            await SendFileAsync(file, fileName, new ChatId(channelId));        

        public async Task SendFileAsync(Stream file, string fileName, ChatId chatlId)
        {
            try
            {
                await _botClient!.SendDocument(chatlId, InputFile.FromStream(file, fileName));
            }
            catch (Exception ex)
            {
                Log.Error(string.Format(Error.TelegramSendingFileError, ex.Message));
            }
        }

        public async Task SetWebhookAsync(string url)
        {
            try
            {
                await _botClient!.SetWebhook(url, dropPendingUpdates: true);
            }
            catch (Exception ex)
            {
                Log.Error(string.Format(Error.TelegramWebhookSettingError, ex.Message));
            }
        }
    }
}
