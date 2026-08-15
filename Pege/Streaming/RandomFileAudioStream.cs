
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
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
    /// <summary>
    /// Функционал стрима, транслирующего по кругу аудиофайлы из указанной папки.
    /// </summary>
    internal class RandomFileAudioStream : Stream<FileAudioStreamStatus, AudioChunk>, IFileUploader
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

        /// <summary>
        /// Метод реалиует основной цикл трансляции.
        /// </summary>
        /// <param name="cancellationToken">Токен остановки трансляции.</param>
        protected override async Task BroadcastCycleAsync(CancellationToken cancellationToken)
        {
            _log.Information(Message.BroadcastingStarted);

            try
            {
                // Загружаем первый трек
                string currentTrackPath = GetNextFilename();
                (CastedStatus.Artist, CastedStatus.Track) = _ffmpegService.GetMetadata(currentTrackPath);
                byte[] currentTrackData = await _ffmpegService.EncodeTrackAsync(currentTrackPath, _targetBitrate, _targetSamplerate, cancellationToken);

                if(!string.IsNullOrWhiteSpace(_ffmpegService.NewFilePath))
                    _history.Add(_ffmpegService.NewFilePath);

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Асинхронно начинаем загружать следующий трек (параллельно с воспроизведением текущего)
                    string nextTrackPath = GetNextFilename();
                    (CastedStatus.NextArtist, CastedStatus.NextTrack) = _ffmpegService.GetMetadata(nextTrackPath);
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

                    if (!string.IsNullOrWhiteSpace(_ffmpegService.NewFilePath))
                        _history.Add(_ffmpegService.NewFilePath);

                    CastedStatus.Artist = CastedStatus.NextArtist;
                    CastedStatus.Track = CastedStatus.NextTrack;

                    CastedStatus.NextArtist = null;
                    CastedStatus.NextTrack = null;
                }
            }
            catch { throw; }
            finally
            {
                CastedStatus.NextTrack = null;
                CastedStatus.NextArtist = null;

                if (cancellationToken.IsCancellationRequested)
                    _log.Information(Message.BroadcastingStopped);
            }
        }

        /// <summary>
        /// Метод отправляет трек в стрим с синхронизацией времени.
        /// </summary>
        private async Task BroadcastTrackAsync(byte[] encodedData, CancellationToken cancellationToken)
        {
            _log.Information($"Now playing: \"{CastedStatus.Track}\" by {CastedStatus.Artist}");

            // 1. Используем ReadOnlyMemory вместо ReadOnlySpan, чтобы безопасно пересекать границы await
            ReadOnlyMemory<byte> totalMemory = encodedData;
            int offset = 0;

            // Константы формата AAC ADTS для 44100 Гц
            const double SamplesPerFrame = 1024.0;
            const double SampleRate = 44100.0;
            const double FrameDurationMs = (SamplesPerFrame / SampleRate) * 1000.0; // ~23.219954 мс

            // Желаемое количество целых кадров в одной порции вещания
            // 17 кадров * 23.22мс = ~394.7 мс звука в одном чанке
            const int FramesPerChunk = 17;

            if (_isFirstTrack)
            {
                _isFirstTrack = false;
                _globalStreamStopwatch.Start();
            }

            while (offset < totalMemory.Length && !cancellationToken.IsCancellationRequested)
            {
                int chunkStartOffset = offset;
                int framesPacked = 0;

                // Внутренний цикл нарезки (БЕЗ await внутри)
                while (framesPacked < FramesPerChunk && offset < totalMemory.Length)
                {
                    if (offset + 7 > totalMemory.Length)
                    {
                        offset = totalMemory.Length;
                        break;
                    }

                    // Локально создаем Span только для быстрой проверки заголовка текущего кадра
                    ReadOnlySpan<byte> headerSpan = totalMemory.Span[offset..];

                    // Проверяем синхрослово ADTS (0xFFF)
                    if (headerSpan[0] == 0xFF && (headerSpan[1] & 0xF0) == 0xF0)
                    {
                        // Вытаскиваем 13-битное число длины текущего кадра из заголовка
                        int frameLength = ((headerSpan[3] & 0x03) << 11)
                                        | (headerSpan[4] << 3)
                                        | ((headerSpan[5] & 0xE0) >> 5);

                        // Защита от битых кадров
                        if (frameLength < 7 || offset + frameLength > totalMemory.Length)
                        {
                            offset = totalMemory.Length;
                            break;
                        }

                        // Шагаем строго на конец текущего целого кадра
                        offset += frameLength;
                        framesPacked++;
                    }
                    else
                    {
                        // Если потеряли синхронизацию, смещаемся по 1 байту
                        offset++;
                    }
                }

                int bytesToWrite = offset - chunkStartOffset;
                if (bytesToWrite <= 0) break;

                // Вычисляем РЕАЛЬНУЮ длительность этой порции звука исходя из кол-ва ЦЕЛЫХ кадров
                double chunkDurationMs = framesPacked * FrameDurationMs;

                // Безопасно нарезаем Memory, которая без проблем живет в куче
                var chunkData = totalMemory.Slice(chunkStartOffset, bytesToWrite);
                //_globalBytesSent += bytesToWrite;

                BroadcastChunk(new AudioChunk
                {
                    Data = chunkData, // Передаем нашу ReadOnlyMemory<byte> напрямую
                    BitrateKbps = _targetBitrate,
                    DurationMs = (int)chunkDurationMs,
                    StreamMetadata = GenerateMetadataString().ToIcyMetadata()
                });

                // Вот здесь происходит точка прерывания (await), 
                // но так как у нас в теле цикла больше нет ReadOnlySpan полей, ошибка пропадет!
                if (offset < totalMemory.Length)
                {
                    _globalStreamDurationMs += chunkDurationMs;

                    double actualMs = _globalStreamStopwatch.Elapsed.TotalMilliseconds;
                    int sleepMs = (int)(_globalStreamDurationMs - actualMs);

                    if (sleepMs > 0)
                    {
                        await Task.Delay(sleepMs, cancellationToken);
                    }
                }
            }
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
        /// Метод генерации строки с метаданными.
        /// </summary>
        /// <returns></returns>
        private string GenerateMetadataString() => $"StreamTitle='{CastedStatus.Artist} - {CastedStatus.Track}';NextTrack='{CastedStatus.NextArtist} - {CastedStatus.NextTrack}';";

        /// <summary>
        /// Метод выбирает следующий случайный аудиофайл.
        /// </summary>
        private string GetNextFilename()
        {
            var allFiles = Directory.GetFiles(CastedStatus.Path!, "*.*")
                .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".aac", StringComparison.OrdinalIgnoreCase))
                .ToArray();

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
        private async Task UpdateTotalTracksAndDurationAsync()
        {
            var files = Directory.GetFiles(CastedStatus.Path!, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".aac", StringComparison.OrdinalIgnoreCase))
                .ToArray();

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

            CastedStatus.TotalDuration = TimeSpan.FromSeconds(totalSeconds.Sum());
            CastedStatus.TotalTracks = processed;
        }

        /// <summary>
        /// Метод удаления трека из плейлиста.
        /// </summary>
        /// <param name="fileName">Имя файла.</param>
        /// <exception cref="ApplicationException">В случае, если каталог плейлиста не существует.</exception>
        /// <exception cref="FileNotFoundException">В случае, если файл не найден в плейлисте.</exception>
        public void DeleteTrack(string fileName)
        {
            if (!Path.Exists(CastedStatus.Path))
                throw new ApplicationException(Error.DirectoryDoesNotExist);

            var path = Path.Combine(CastedStatus.Path!, fileName);

            var lockManager = _serviceProvider.GetRequiredService<FileLockManager>();
            lock (lockManager.GetLock(path))
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    _ = UpdateTotalTracksAndDurationAsync();
                }
                else
                    throw new FileNotFoundException();
            }
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

                        string? fileExtension = Path.GetExtension(currentFileName)?.ToLowerInvariant();
                        if (fileExtension != ".mp3" && fileExtension != ".aac")
                        {
                            throw new ApplicationException(Error.UnacceptableFileExtension);
                        }

                        if (!Path.Exists(CastedStatus.Path))
                            throw new ApplicationException(Error.DirectoryDoesNotExist);

                        var filename = Path.Combine(CastedStatus.Path, currentFileName);
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
                            return ffmpegService.GetMetadata(Path.Combine(CastedStatus.Path!, i.Key));
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

        /// <summary>
        /// Метод публикации в Telegram-канале сообшения о новых треках.
        /// </summary>
        /// <param name="list">Список новых треков.</param>
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
            message.AppendLine($"Now we have {CastedStatus.TotalTracks.ToString() ?? "some"} tracks for a non-stop <a href=\"{config?["BaseUri"]}/stream/_\">stream</a> without repeats, lasting {CastedStatus.TotalDuration.FormatTimeSpan() ?? "some time"}.");

            Log.Information(message.ToString());

            await tgService.SendMessageAsync(message.ToString(), Status.TelegramChannelId!);
        }

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

        private readonly Stopwatch _globalStreamStopwatch = new();
        private double _globalStreamDurationMs = 0; // Считаем абсолютное медиа-время в мс
        private bool _isFirstTrack = true;
        private readonly Random _random = new();
        private readonly int _targetBitrate = 160;
        private readonly int _targetSamplerate = 44100;
    }
}
