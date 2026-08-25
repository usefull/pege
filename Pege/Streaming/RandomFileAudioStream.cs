
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Pege.Entities;
using Pege.Extensions;
using Pege.Interfaces;
using Pege.Resource;
using Pege.Services;
using Serilog;
using System.Collections.Concurrent;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;

namespace Pege.Streaming
{
    /// <summary>
    /// Функционал стрима, транслирующего по кругу аудиофайлы из указанной папки.
    /// </summary>
    internal partial class RandomFileAudioStream : Stream<FileAudioStreamStatus, AudioChunk>, IFileUploader
    {
        /// <summary>
        /// Конструктор.
        /// </summary>
        /// <param name="status">Информация о стриме.</param>
        /// <param name="serviceProvider">Провайдер сервисов DI.</param>
        /// <exception cref="ApplicationException">В случае, если каталог с файлами не существует.</exception>
        public RandomFileAudioStream(FileAudioStreamStatus status, IServiceProvider serviceProvider) : base(status, serviceProvider)
        {
            CastedStatus.Path = status.Path;
            Status.ContentType = "audio/aac";

            if (!Path.Exists(CastedStatus.Path))
                throw new ApplicationException(string.Format(Error.DirectoryDoesNotExist, CastedStatus.Path));

            _ffmpegService = serviceProvider.GetRequiredService<FFmpegService>();
            _ffmpegService.Log = _log;

            _ = UpdateTotalTracksAndDurationAsync();
        }

        private async Task<TrackData> GetNextTrackAsync(CancellationToken cancellationToken)
        {
            TrackData? result = null;
            string? trackPath = null;

            while (result == null)
            {
                try
                {
                    trackPath = GetNextFilename();
                    result = await _ffmpegService.PrepareAacTrackAsync(trackPath, _targetSamplerate, _framesPerChunk, cancellationToken);
                    if (result.Filename != trackPath)
                        _history.Add(result.Filename);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _log.Error(string.Format(Error.PreparingTrackError, trackPath, ex.Message));
                }
            }

            return result;
        }

        /// <summary>
        /// Метод реалиует основной цикл трансляции.
        /// </summary>
        /// <param name="cancellationToken">Токен остановки трансляции.</param>
        protected override async Task BroadcastCycleAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!_ffmpegService.IsFFmpegAvailable() || !_ffmpegService.IsFFprobeAvailable())
                    throw new Exception(Error.FFmpegNotAvailable);

                _log.Information(Message.BroadcastingStarted);

                // Загружаем первый трек
                var trackData = await GetNextTrackAsync(cancellationToken);
                CastedStatus.Artist = trackData.Artist;
                CastedStatus.Track = trackData.Title;
                CastedStatus.FromFlac = trackData.FromFlac;
                UpdateIcyMetadata();

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var loadNextTask = Task.Run(
                        // Параллельно начинаем готовить следующий трек
                        async () =>
                        {
                            var data = await GetNextTrackAsync(cancellationToken);
                            CastedStatus.NextArtist = data.Artist;
                            CastedStatus.NextTrack = data.Title;
                            CastedStatus.NextFromFlac = data.FromFlac;
                            UpdateIcyMetadata();

                            //Публикуем пост в Tg-канале
                            _ = SendCurrentTrackInfoToTgChannel();

                            return data;
                        },
                        cancellationToken
                    );                

                    // Отправляем текущий трек клиентам
                    await BroadcastTrackAsync(trackData, cancellationToken);

                    // Ждем окончания подготовки следующего трека
                    trackData = await loadNextTask;

                    CastedStatus.Artist = CastedStatus.NextArtist;
                    CastedStatus.Track = CastedStatus.NextTrack;
                    CastedStatus.FromFlac = CastedStatus.NextFromFlac;

                    CastedStatus.NextArtist = null;
                    CastedStatus.NextTrack = null;
                    CastedStatus.NextFromFlac = false;

                    UpdateIcyMetadata();
                }
            }
            catch { throw; }
            finally
            {
                CastedStatus.Track = null;
                CastedStatus.Artist = null;
                CastedStatus.FromFlac = false;
                CastedStatus.NextTrack = null;
                CastedStatus.NextArtist = null;
                CastedStatus.NextFromFlac = false;
                UpdateIcyMetadata();

                if (cancellationToken.IsCancellationRequested)
                    _log.Information(Message.BroadcastingStopped);
            }
        }

        /// <summary>
        /// Метод отправляет трек в стрим с синхронизацией времени.
        /// </summary>
        private async Task BroadcastTrackAsync(TrackData trackData, CancellationToken cancellationToken)
        {
            _log.Information($"Now playing: \"{CastedStatus.Track}\" by {CastedStatus.Artist}");

            var chunkDuration = _framesPerChunk * (double)trackData.SamplesPerFrame / trackData.SampleRate;

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(chunkDuration));

            do
            {
                if (trackData.Chunks.TryDequeue(out var chunk))
                {
                    BroadcastChunk(new AudioChunk
                    {
                        Data = chunk,
                        StreamMetadata = _currentIcyMetadata.Bytes
                    });
                }
                else
                    break;
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }

        /// <summary>
        /// Метод публикации сообщения о текущем треке в Telegram-канал.
        /// </summary>
        private async Task SendCurrentTrackInfoToTgChannel()
        {
            var tgService = _serviceProvider.GetService<TelegramService>();
            if (tgService == null) return;

            var message = await tgService.SendMessageAsync(@$"<u>Now playing</u>:
<b>""{CastedStatus.Track}""</b>
by <b>{CastedStatus.Artist}</b>

<u>Next track</u>:
<b>""{CastedStatus.NextTrack}""</b>
by <b>{CastedStatus.NextArtist}</b>", Status.TelegramChannelId!);

            _ = ClearPreviousTgMessages(message);
        }

        /// <summary>
        /// Метод удаления старых сообщений в Telegram-канале.
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

        /// <summary>
        /// Метод выбирает следующий случайный аудиофайл.
        /// </summary>
        private string GetNextFilename()
        {
            var allFiles = Directory.GetFiles(CastedStatus.Path!, "*.*");

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
        /// Метод обновляет статус стрима информацией о продолжительности воспроизведения и количестве треков.
        /// </summary>
        private async Task<List<TrackData>> UpdateTotalTracksAndDurationAsync()
        {
            var files = Directory.GetFiles(CastedStatus.Path!, "*.*", SearchOption.AllDirectories)
                .ToArray();

            var tracks = new ConcurrentBag<TrackData>();
            var errors = new ConcurrentBag<string>();

            await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 },
                async (file, token) =>
                {
                    try
                    {
                        var track = await _ffmpegService.GetTrackMetadataAsync(file, CancellationToken.None);
                        tracks.Add(track);

                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
                    }
                });

            CastedStatus.TotalDuration = tracks.Aggregate(TimeSpan.Zero, (acc, t) => acc + t.Duration);
            CastedStatus.TotalTracks = tracks.Count;

            return [.. tracks];
        }

        /// <summary>
        /// Метод удаления трека из плейлиста.
        /// </summary>
        /// <param name="fileName">Имя файла.</param>
        /// <exception cref="ApplicationException">В случае, если каталог плейлиста не существует.</exception>
        /// <exception cref="FileNotFoundException">В случае, если файл не найден в плейлисте.</exception>
        public async Task DeleteTrackAsync(string fileName)
        {
            if (!Path.Exists(CastedStatus.Path))
                throw new ApplicationException(Error.DirectoryDoesNotExist);

            var path = Path.Combine(CastedStatus.Path!, fileName);

            if (File.Exists(path))
            {
                File.Delete(path);
                _ = UpdateTotalTracksAndDurationAsync();
            }
            else
                throw new FileNotFoundException();
        }

        /// <summary>
        /// Метод загрузки новых треков в плейл лист.
        /// </summary>
        /// <param name="reader">Содержимое http-запроса.</param>
        /// <param name="quietly">Флаг предписывает не публиковать в Telegram-канале информации о новых треках.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Результаты загрузки.</returns>
        /// <exception cref="ApplicationException">В случаях ошибок чтения содержимого запроса, а так же если файл уже есть в плейлисте или каталог не существует.</exception>
        public async Task<UploadResult> UploadAsync(MultipartReader reader, bool quietly, CancellationToken cancellationToken)
        {
            var result = new UploadResult();
            string? currentFileName = null;

            var allFiles = Directory.GetFiles(CastedStatus.Path!, "*.*")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .ToList();

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

                        if (!Path.Exists(CastedStatus.Path))
                            throw new ApplicationException(Error.DirectoryDoesNotExist);

                        var name = SpaceRegex().Replace(Path.GetFileNameWithoutExtension(currentFileName), " ").Trim();
                        var ext = Path.GetExtension(currentFileName);
                        currentFileName = $"{name}{ext}";
                        if (allFiles.Contains(name))
                            throw new ApplicationException(string.Format(Error.FileAlreadyExists, currentFileName));

                        var filename = Path.Combine(CastedStatus.Path, currentFileName);                            

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
                updateStatusTask?.ContinueWith(async task =>
                {
                    var newFilesInfo = task.Result.IntersectBy(
                        result.Errors.Where(i => i.Value == null).Select(i => i.Key),
                        i => Path.GetFileName(i.Filename)).ToList();

                    await SendNewTracksInfoToTgChannel(newFilesInfo);
                });

            return result;
        }

        /// <summary>
        /// Метод публикации в Telegram-канале сообшения о новых треках.
        /// </summary>
        /// <param name="list">Список новых треков.</param>
        private async Task SendNewTracksInfoToTgChannel(List<TrackData> list)
        {
            var config = _serviceProvider.GetService<IConfiguration>();
            var tgService = _serviceProvider.GetService<TelegramService>();
            if (list.Count == 0 || tgService == null) return;

            var message = new StringBuilder($"{(list.Count == 1 ? "New track" : $"{list.Count} new tracks")} has been uploaded to the playlist:");
            message.AppendLine(string.Empty);
            message.AppendLine(string.Empty);

            message = list.Aggregate(message, (mess, i) =>
            {
                mess.AppendLine($@" ◻ <b>""{i.Title}""</b> by <b>{i.Artist}</b>");
                return mess;
            });

            message.AppendLine(string.Empty);
            message.AppendLine($"Now we have {CastedStatus.TotalTracks.ToString() ?? "some"} tracks for a non-stop <a href=\"{config?["BaseUri"]}/stream/_\">stream</a> without repeats, lasting {CastedStatus.TotalDuration.FormatTimeSpan() ?? "some time"}.");

            Log.Information(message.ToString());

            await tgService.SendMessageAsync(message.ToString(), Status.TelegramChannelId!);
        }

        /// <summary>
        /// Метод обновления ICY-метаданных.
        /// </summary>
        private void UpdateIcyMetadata()
        {
            string metaString = $"StreamTitle='{CastedStatus.Artist} - {CastedStatus.Track}';NextTrack='{CastedStatus.NextArtist} - {CastedStatus.NextTrack}';FromFlac='{(CastedStatus.FromFlac ? "1" : "0")}';";

            if (_currentIcyMetadata.String == metaString)
                return;

            Interlocked.Exchange(ref _currentIcyMetadata, new IcyMetadata(metaString, metaString.ToIcyMetadata()));
        }

        /// <summary>
        /// ICY-метаданные
        /// </summary>
        /// <param name="String">Строковое представление.</param>
        /// <param name="Bytes">Массив байтов.</param>
        private record IcyMetadata(string String, byte[]? Bytes);

        /// <summary>
        /// Текущее содержимое ICY-метаданных.
        /// </summary>
        private volatile IcyMetadata _currentIcyMetadata = new("", null);        

        /// <summary>
        /// Идентификаторы сообщений в telegram-канале о новых треках.
        /// Хранятся, чтобы в случае сбоя, их можно было удалить на следующей итерации.
        /// </summary>
        private readonly HashSet<int> _tgMessageId = [];

        /// <summary>
        /// Объект блокировки для доступа к <see cref="_tgMessageId"/>.
        /// </summary>
        private readonly SemaphoreSlim _lockTgMessageId = new(1, 1);

        /// <summary>
        /// Сервис FFmpeg для работы с медиафайлами.
        /// </summary>
        private readonly FFmpegService _ffmpegService;

        /// <summary>
        /// История трансляции треков.
        /// </summary>
        private readonly List<string> _history = [];

        private readonly Random _random = new();
        private readonly int _targetSamplerate = 44100;
        private readonly int _framesPerChunk = 15;

        [GeneratedRegex(@"\s+")]
        private static partial Regex SpaceRegex();
    }
}
