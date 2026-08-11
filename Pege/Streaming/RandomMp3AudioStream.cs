using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Pege.Data;
using Pege.Entities;
using Pege.Extensions;
using Pege.Interfaces;
using Pege.Resource;
using Pege.Services;
using Serilog;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace Pege.Streaming
{
    internal class RandomMp3AudioStream : Stream<FileAudioStreamInfo, FileAudioStreamStatus, AudioChunk>, IFileUploader
    {
        public RandomMp3AudioStream(FileAudioStreamInfo info, IServiceProvider serviceProvider) : base(info, serviceProvider)
        {
            CastedStatus?.Path = info.Path;
            CastedStatus?.ContentType = "audio/mpeg";

            if (!Path.Exists(CastedStatus?.Path))
                throw new ApplicationException(string.Format(Error.DirectoryDoesNotExist, CastedStatus?.Path));

            _ffmpegService = serviceProvider.GetRequiredService<FFmpegService>();
            _ffmpegService.Log = _log;

            _ = UpdateTotalTracksAndDurationAsync();
        }

        protected override async Task BroadcastCycleAsync(CancellationToken cancellationToken)
        {
            _log.Information(Message.BroadcastingStarted);

            try
            {
                // Загружаем первый трек
                string currentTrackPath = GetNextFilename();
                byte[] currentTrackData = await _ffmpegService.EncodeTrackAsync(currentTrackPath, _targetBitrate, _targetSamplerate, cancellationToken);
                (CastedStatus!.Artist, CastedStatus.Track) = _ffmpegService.GetMp3Metadata(currentTrackPath);

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Асинхронно начинаем загружать следующий трек (параллельно с воспроизведением текущего)
                    string nextTrackPath = GetNextFilename();
                    (CastedStatus!.NextArtist, CastedStatus!.NextTrack) = _ffmpegService.GetMp3Metadata(nextTrackPath);
                    var loadNextTask = Task.Run(
                        async () => await _ffmpegService.EncodeTrackAsync(nextTrackPath, _targetBitrate, _targetSamplerate, cancellationToken),
                        cancellationToken
                    );

                    //Публикуем пост в Tg-канале
                    await SendCurrentTrackInfoToTgChannel();

                    // Отправляем текущий трек клиентам
                    await BroadcastTrackAsync(currentTrackData, cancellationToken);

                    // Ждем загрузки следующего трека
                    currentTrackData = await loadNextTask;

                    CastedStatus!.Artist = CastedStatus!.NextArtist;
                    CastedStatus.Track = CastedStatus!.NextTrack;

                    CastedStatus!.NextArtist = null;
                    CastedStatus!.NextTrack = null;
                }
            }
            catch { throw; }
            finally
            {
                CastedStatus?.NextTrack = null;
                CastedStatus?.NextArtist = null;

                if (cancellationToken.IsCancellationRequested)
                    _log.Information(Message.BroadcastingStopped);
            }
        }

        /// <summary>
        /// Метод отправляет трек в поток с синхронизацией времени.
        /// </summary>
        private async Task BroadcastTrackAsync(byte[] encodedData, CancellationToken cancellationToken)
        {
            _log.Information($"Now playing: \"{CastedStatus?.Track}\" by {CastedStatus?.Artist}");
            _log.Information($"Next: \"{CastedStatus!.NextTrack}\" by {CastedStatus!.NextArtist}");

            int offset = 0;
            long totalBytes = encodedData.Length;

            double bytesPerMs = _targetBitrate * 1000.0 / 8.0 / 1000.0;

            var stopwatch = Stopwatch.StartNew();
            long totalSent = 0;

            while (offset < totalBytes && !cancellationToken.IsCancellationRequested)
            {
                int bytesToSend = (int)Math.Min(_chunkSize, totalBytes - offset);

                // Создаем чанк без копирования данных
                var chunkData = new Memory<byte>(encodedData, offset, bytesToSend);
                offset += bytesToSend;
                totalSent += bytesToSend;

                // Отправляем чанк в канал
                BroadcastChunk(new AudioChunk
                {
                    Data = chunkData,
                    BitrateKbps = _targetBitrate,
                    DurationMs = (int)(bytesToSend / bytesPerMs),
                    StreamMetadata = GenerateMetadataString().ToIcyMetadata()
                });

                // Синхронизация времени (чтобы не улететь вперед)
                if (offset < totalBytes)
                {
                    double expectedMs = totalSent / bytesPerMs;
                    double actualMs = stopwatch.Elapsed.TotalMilliseconds;
                    int sleepMs = (int)(expectedMs - actualMs);

                    if (sleepMs > 0)
                    {
                        await Task.Delay(sleepMs, cancellationToken);
                    }
                }
            }
        }

        private async Task SendCurrentTrackInfoToTgChannel()
        {
            var tgService = _serviceProvider.GetService<TelegramService>();
            if (tgService == null) return;

            var message = await tgService.SendMessageAsync(@$"<u>Now playing</u>:
<b>""{CastedStatus?.Track}""</b>
by <b>{CastedStatus?.Artist}</b>

<u>Next track</u>:
<b>""{CastedStatus?.NextTrack}""</b>
by <b>{CastedStatus?.NextArtist}</b>", Status.TelegramChannelId!);

            _ = ClearPreviousTgMessages(message);
        }

        /// <summary>
        /// Метод удаления старых сообщений в Tg-канале.
        /// </summary>
        /// <param name="newMessage">Последнее сообщение,
        /// которое только-что опубликовано и не должно быть удалено.</param>
        /// <returns></returns>
        private async Task ClearPreviousTgMessages(Telegram.Bot.Types.Message? newMessage = null)
        {
            var tgService = _serviceProvider.GetService<TelegramService>();

            await _lockTgMessageId.WaitAsync();
            try
            {
                var list = _tgMessageId.ToList();

                foreach (int id in list)
                    if (await tgService!.DeleteMessageAsync(id, Status.TelegramChannelId!))
                        _tgMessageId.Remove(id);

                if (newMessage != null)
                    _tgMessageId.Add(newMessage.Id);
            }
            finally
            {
                _lockTgMessageId.Release();
            }
        }

        private string GenerateMetadataString() => $"StreamTitle='{CastedStatus?.Artist} - {CastedStatus?.Track}';NextTrack='{CastedStatus?.NextArtist} - {CastedStatus?.NextTrack}';";

        /// <summary>
        /// Метод выбирает следующий случайный MP3-файл.
        /// </summary>
        private string GetNextFilename()
        {
            var allFiles = Directory.GetFiles(CastedStatus!.Path!, "*.mp3");
            if (allFiles.Length == 0)
                throw new InvalidOperationException(Error.NoFilesToPlay);

            // Исключаем последние сыгранные треки
            var available = allFiles.Except(_history).ToList();

            // Если все треки сыграны, сбрасываем половину истории
            if (available.Count == 0)
            {
                _history.RemoveRange(0, _history.Count / 2);
                available = [.. allFiles.Except(_history)];

                if (available.Count == 0)
                    available = [.. allFiles];
            }

            // Выбираем случайный трек
            string nextFile = available[_random.Next(available.Count)];
            _history.Add(nextFile);

            return nextFile;
        }

        /// <summary>
        /// Метод обновляет информацию о продолжительности воспроизведения и количестве треков.
        /// </summary>
        private async Task UpdateTotalTracksAndDurationAsync()
        {
            var files = Directory.GetFiles(CastedStatus?.Path!, "*.mp3", SearchOption.AllDirectories);

            var totalSeconds = new ConcurrentBag<double>();
            var errors = new ConcurrentBag<string>();
            var processed = 0;

            await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 },
                async (file, token) =>
                {
                    try
                    {
                        var seconds = await _ffmpegService.GetDurationAsync(file, CancellationToken.None);
                        totalSeconds.Add(seconds);

                        Interlocked.Increment(ref processed);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
                    }
                });

            CastedStatus?.TotalDuration = TimeSpan.FromSeconds(totalSeconds.Sum());
            CastedStatus?.TotalTracks = processed;
        }

        public void DeleteTrack(string fileName)
        {
            if (!Path.Exists(CastedStatus?.Path))
                throw new ApplicationException(Error.DirectoryDoesNotExist);

            var path = Path.Combine(CastedStatus?.Path!, fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
                _ = UpdateTotalTracksAndDurationAsync();
            }
            else
                throw new FileNotFoundException();
        }

        public async Task<UploadResult> UploadAsync(MultipartReader reader, bool quietly, CancellationToken cancellationToken)
        {
            var result = new UploadResult();
            string? currentFileName = null;

            do
            {
                try
                {
                    // Изначально в качестве имени берём порядковый индекс файла
                    // на случай, если не получится найти файл в теле запроса,
                    // а ошибку в результат под каким-то именем файла записать нужно.
                    currentFileName = result.TotalUploaded.ToString();

                    var section = await reader.ReadNextSectionAsync(cancellationToken);
                    if (section == null) break;

                    var hasContentDispositionHeader = ContentDispositionHeaderValue.TryParse(
                        section.ContentDisposition, out var contentDisposition);

                    if (hasContentDispositionHeader && (contentDisposition?.HasFileContentDisposition() ?? false))
                    {
                        currentFileName = Path.GetFileName(contentDisposition.FileName.Value);
                        if (string.IsNullOrWhiteSpace(currentFileName))
                        {
                            currentFileName = result.TotalUploaded.ToString();
                            throw new ApplicationException(Error.UnableReadUploadedFileName);
                        }

                        if (Path.GetExtension(currentFileName)?.ToLowerInvariant() != ".mp3")
                            throw new ApplicationException(Error.OnlyMp3FileExtensionAvailable);


                        if (!Path.Exists(CastedStatus?.Path))
                            throw new ApplicationException(Error.DirectoryDoesNotExist);

                        var filename = Path.Combine(CastedStatus?.Path!, currentFileName);
                        if (File.Exists(filename))
                            throw new ApplicationException(string.Format(Error.FileAlreadyExists, currentFileName));

                        using var targetStream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);

                        await section.Body.CopyToAsync(targetStream, cancellationToken);

                        result.TotalUploaded++;
                        result.Errors.Add(currentFileName, null);
                    }
                    else
                    {
                        result.Errors.Add(currentFileName, Error.FileContentDispositionNotFound);
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add(currentFileName!, ex.Message);
                }
            }
            while (true);

            var updateStatusTask = UpdateTotalTracksAndDurationAsync();
            var ffmpegService = _serviceProvider.GetService<FFmpegService>();

            if (!quietly && ffmpegService != null)
                updateStatusTask?.ContinueWith(async _ => {
                    var list = result.Errors.Where(i => i.Value == null)
                    .Select(i =>
                    {
                        try
                        {
                            return ffmpegService.GetMp3Metadata(Path.Combine(CastedStatus?.Path!, i.Key));
                        }
                        catch
                        {
                            return (string.Empty, string.Empty);
                        }
                    }).Where(i => !string.IsNullOrWhiteSpace(i.Item1)).ToList();

                    await SendNewTracksInfoToTgChannel(list);
                });

            return result;
        }

        private async Task SendNewTracksInfoToTgChannel(List<(string artist, string title)> list)
        {
            var config = _serviceProvider.GetService<IConfiguration>();
            var tgService = _serviceProvider.GetService<TelegramService>();
            if (list.Count == 0 || tgService == null) return;

            var message = new StringBuilder($"{(list.Count == 1 ? "New track" : $"{list.Count} new tracks")} has been uploaded to the playlist:");
            message.AppendLine(string.Empty);
            message.AppendLine(string.Empty);

            message = list.Aggregate(message, (mess, i) =>
            {
                mess.AppendLine($@" ◻ <b>""{i.title}""</b> by <b>{i.artist}</b>");
                return mess;
            });

            message.AppendLine(string.Empty);
            message.AppendLine($"Now we have {CastedStatus?.TotalTracks.ToString() ?? "some"} tracks for a non-stop <a href=\"{config?["BaseUri"]}/stream/_\">stream</a> without repeats, lasting {CastedStatus?.TotalDuration.FormatTimeSpan() ?? "some time"}.");

            Log.Information(message.ToString());

            await tgService.SendMessageAsync(message.ToString(), Status.TelegramChannelId!);
        }

        private readonly HashSet<int> _tgMessageId = [];
        private readonly SemaphoreSlim _lockTgMessageId = new(1, 1);
        private readonly FFmpegService _ffmpegService;
        private readonly List<string> _history = [];
        private readonly Random _random = new();
        private readonly int _targetBitrate = 320;
        private readonly int _targetSamplerate = 44100;
        private readonly int _chunkSize = 8192;
    }
}