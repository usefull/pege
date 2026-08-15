
using Pege.Resource;
using System.Diagnostics;
using System.Text;

namespace Pege.Services
{
    /// <summary>
    /// Сервис FFmpeg для работы с медиафайлами.
    /// </summary>
    /// <remarks>
    /// Конструктор.
    /// </remarks>
    internal class FFmpegService(FileLockManager lockManager)
    {
        /// <summary>
        /// Логгер.
        /// </summary>
        public Serilog.ILogger? Log { get; set; }

        /// <summary>
        /// Путь к новому файлу после вызова метода <see cref="EncodeTrackAsync"/>.
        /// Если перекодировки не было, это свойство будет содержать null.
        /// </summary>
        public string? NewFilePath { get; private set; }

        /// <summary>
        /// Метод чтения метаданных медиафайла.
        /// </summary>
        /// <param name="filePath">Путь к медиафайлу.</param>
        /// <returns>Кортеж с именем исполнителя и названием трека.</returns>
        public (string artist, string aitle) GetMetadata(string filePath)
        {
            string artist = Message.UnknownArtist;
            string title = Path.GetFileNameWithoutExtension(filePath);

            if (Path.GetExtension(filePath).Equals(".aac", StringComparison.OrdinalIgnoreCase))
            {
                string filename = Path.GetFileNameWithoutExtension(filePath);

                // Сначала отрезаем битрейт с конца (ищем последний дефис " - ")
                int lastDashIndex = filename.LastIndexOf(" - ");
                if (lastDashIndex != -1 && int.TryParse(filename[(lastDashIndex + 3)..], out _))
                {
                    filename = filename[..lastDashIndex].Trim();
                }

                // 2. Теперь разделяем Артиста и Трек (ищем первый дефис " - ")
                int artistDashIndex = filename.IndexOf(" - ");
                if (artistDashIndex != -1)
                {
                    artist = filename[..artistDashIndex].Trim();
                    title = filename[(artistDashIndex + 3)..].Trim();
                    return (artist, title);
                }

                return (Message.UnknownArtist, filename);
            }

            try
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException(Error.FileNotFound);

                string arguments = $"-v error -show_entries format_tags=artist,title -of ini \"{filePath}\"";

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _ffprobePath,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        outputBuilder.AppendLine(e.Data);
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        errorBuilder.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new Exception(string.Format(Error.FFprobeError, errorBuilder));
                }

                string output = outputBuilder.ToString();                
                var lines = output.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    int separatorIndex = line.IndexOf('=');
                    if (separatorIndex == -1) continue;

                    string key = line[..separatorIndex].Trim();
                    string value = line[(separatorIndex + 1)..].Trim();

                    if (key.Equals("artist", StringComparison.OrdinalIgnoreCase))
                        artist = value;
                    else if (key.Equals("title", StringComparison.OrdinalIgnoreCase))
                        title = value;
                }                
            }
            catch (Exception ex)
            {
                Log?.Error(string.Format(Error.MetadataReadingError, filePath, ex.Message));
            }

            return (artist, title);
        }

        /// <summary>
        /// Метод получения продолжительности медиафайла в секундах.
        /// </summary>
        /// <param name="filePath">Путь к медиафайлу.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        public async Task<double> GetDurationAsync(string filePath, CancellationToken cancellationToken)
        {
            if (!IsFFprobeAvailable())
                return 0;

            var psi = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (double.TryParse(output.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                return seconds;

            return 0;
        }

        /// <summary>
        /// Метод получения аудиорейтов медиафайла.
        /// </summary>
        /// <param name="filePath">Путь к медиафайлу.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Кортеж со значениями битрейта в килобитах/с и частоты дискретизации в герцах.</returns>
        public async Task<(int bitrate, int samplerate)> GetAudioRateAsync(string filePath, CancellationToken cancellationToken)
        {
            int bitrate = 0;
            int samplerate = 0;

            try
            {
                // Попытка достать битрейт из имени файла (для .aac)
                string filename = Path.GetFileNameWithoutExtension(filePath);
                int lastDashIndex = filename.LastIndexOf(" - ");

                if (lastDashIndex != -1)
                {
                    string potentialBitrateStr = filename[(lastDashIndex + 3)..].Trim();
                    // Проверяем, действительно ли в конце имени файла указано число битрейта
                    if (int.TryParse(potentialBitrateStr, out int parsedBitrate))
                    {
                        bitrate = parsedBitrate; // Нашли битрейт прямо в имени! (например, 160)
                    }
                }

                // Считываем параметры через ffprobe
                // Если битрейт уже нашли в имени, запрашиваем у ffprobe ТОЛЬКО sample_rate (это быстро)
                // Если не нашли (для .mp3) — запрашиваем и bit_rate, и sample_rate
                string entries = bitrate > 0 ? "stream=sample_rate" : "stream=bit_rate,sample_rate";
                string arguments = $"-v error -select_streams a:0 -show_entries {entries} -of ini \"{filePath}\"";

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _ffprobePath,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8
                    }
                };

                var outputBuilder = new StringBuilder();
                process.OutputDataReceived += (sender, e) => { if (!string.IsNullOrEmpty(e.Data)) outputBuilder.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                await process.WaitForExitAsync(cancellationToken);

                string output = outputBuilder.ToString().Trim();
                var lines = output.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    int separatorIndex = line.IndexOf('=');
                    if (separatorIndex == -1) continue;

                    string key = line[..separatorIndex].Trim();
                    string value = line[(separatorIndex + 1)..].Trim();

                    if (key.Equals("sample_rate", StringComparison.OrdinalIgnoreCase))
                        _ = int.TryParse(value, out samplerate);
                    else if (key.Equals("bit_rate", StringComparison.OrdinalIgnoreCase) && bitrate == 0)
                    {
                        if (int.TryParse(value, out int rawBitrate))
                            bitrate = rawBitrate / 1000; // Для MP3 переводим в кбит/с
                    }
                }
            }
            catch (Exception ex)
            {
                Log?.Error(string.Format(Error.AudioRateReadingError, filePath, ex.Message));
            }

            return (bitrate, samplerate);
        }

        /// <summary>
        /// Метод перекодирует медиафайл в заданный битрейт и частоту дискретизации.
        /// </summary>
        /// <remarks>Метод перезаписывает исходный файл. Если рейты исходного файла совпадают с требуемыми, перезаписи фала не происходит.</remarks>
        /// <param name="filePath">путь к файлу</param>
        /// <param name="targetBitrate">Требуемый битрей.</param>
        /// <param name="targetSamplerate">Требуемый семплрейт.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Содержимое перекодированного файла.</returns>
        public async Task<byte[]> EncodeTrackAsync(string filePath, int targetBitrate, int targetSamplerate, CancellationToken cancellationToken)
        {
            NewFilePath = null;

            if (!IsFFmpegAvailable() || !IsFFprobeAvailable())
            {
                Log?.Warning($"{Error.FFmpegNotAvailable} {string.Format(Message.UsingOriginalFile, filePath)}");
                return await File.ReadAllBytesAsync(filePath, cancellationToken);
            }

            (var originalBitrate, var origSamplerate) = await GetAudioRateAsync(filePath, cancellationToken);

            // Проверяем, не является ли файл уже готовым AAC с нужными параметрами
            bool isAac = Path.GetExtension(filePath).Equals(".aac", StringComparison.OrdinalIgnoreCase);
            if (isAac && originalBitrate == targetBitrate && origSamplerate == targetSamplerate)
            {
                Log?.Information($"{Message.NoEncodingNeeded} {string.Format(Message.UsingOriginalFile, filePath)}");
                return await File.ReadAllBytesAsync(filePath, cancellationToken);
            }

            Log?.Information(string.Format(Message.Encoding, filePath, targetBitrate, targetSamplerate));

            string tempOutput = Path.Combine(Path.GetTempPath(), $"output_{Guid.NewGuid()}.aac");

            try
            {
                // Аргументы под AAC ADTS:
                // -c:a aac : используем нативный кодек AAC
                // -f adts  : упаковываем в потоковый контейнер ADTS (аналог стрима MP3 для радио)
                string arguments = $"-i \"{filePath}\" -c:a aac -b:a {targetBitrate}k -ar {targetSamplerate} -ac 2 -map 0:a -f adts -y \"{tempOutput}\"";

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _ffmpegPath,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };

                var errorBuilder = new StringBuilder();
                process.ErrorDataReceived += (sender, e) => { if (!string.IsNullOrEmpty(e.Data)) errorBuilder.AppendLine(e.Data); };

                try
                {
                    process.Start();
                    process.BeginErrorReadLine();
                    await process.WaitForExitAsync(cancellationToken);
                }
                finally
                {
                    try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
                }

                if (process.ExitCode != 0) throw new Exception(errorBuilder.ToString());
                if (!File.Exists(tempOutput)) throw new Exception(string.Format(Error.FFmpegOutputError, filePath));

                byte[] encodedData = await File.ReadAllBytesAsync(tempOutput, cancellationToken);
                if (encodedData.Length == 0) throw new Exception(string.Format(Error.FFmpegOutputError, filePath));

                string targetPath = filePath;

                lock (_lockManager.GetLock(filePath))
                {
                    // Если мы впервые кодируем MP3 в AAC, создаем новое "говорящее" имя файла
                    if (!isAac)
                    {
                        try
                        {
                            // Читаем теги из MP3, пока он жив
                            (string artist, string title) = GetMetadata(filePath);

                            // Безопасная очистка имени от запрещенных символов файловой системы
                            string safeArtist = string.Concat(artist.Split(Path.GetInvalidFileNameChars()));
                            string safeTitle = string.Concat(title.Split(Path.GetInvalidFileNameChars()));

                            string directory = Path.GetDirectoryName(filePath) ?? "";

                            // Формируем имя: Папка/Исполнитель - Название - 160.aac
                            targetPath = Path.Combine(directory, $"{safeArtist} - {safeTitle} - {targetBitrate}.aac");
                        }
                        catch
                        {
                            // Резервный вариант, если теги прочитать не удалось вовсе
                            string directory = Path.GetDirectoryName(filePath) ?? "";
                            string nameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                            targetPath = Path.Combine(directory, $"{nameWithoutExt} - {targetBitrate}.aac");
                        }
                    }
                    else
                    {
                        // Если файл уже был .aac, но мы почему-то решили его переписать (например, изменился битрейт в конфиге)
                        // Нам нужно обновить цифру битрейта в его имени, если она там старая
                        string directory = Path.GetDirectoryName(filePath) ?? "";
                        string filename = Path.GetFileNameWithoutExtension(filePath);
                        int lastDashIndex = filename.LastIndexOf(" - ");

                        if (lastDashIndex != -1 && int.TryParse(filename[(lastDashIndex + 3)..], out _))
                        {
                            string baseName = filename[..lastDashIndex];
                            targetPath = Path.Combine(directory, $"{baseName} - {targetBitrate}.aac");
                        }
                    }

                    // Копируем временный файл в целевой путь
                    File.Copy(tempOutput, targetPath, true);
                    NewFilePath = targetPath;

                    // Если мы создали новый .aac файл вместо старого .mp3 — удаляем старый .mp3
                    if (!targetPath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(filePath); } catch { }
                    }
                }

                return encodedData;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Log?.Warning($"{ex.Message}\n{Message.UsingOriginalFile}");
                return await File.ReadAllBytesAsync(filePath, cancellationToken);
            }
            finally
            {
                try { if (File.Exists(tempOutput)) File.Delete(tempOutput); } catch (Exception ex) { Log?.Warning(string.Format(Error.TempFilesDeleteError, ex.Message)); }
            }
        }

        /// <summary>
        /// Метод проверки доступности утилиты FFmpeg.
        /// </summary>
        private bool IsFFmpegAvailable()
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _ffmpegPath,
                        Arguments = "-version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                if (process.WaitForExit(TimeSpan.FromSeconds(5)))
                {
                    return process.ExitCode == 0;
                }
                else
                {
                    try { process.Kill(); } catch { }
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Метод проверки доступности утилиты FFprobe.
        /// </summary>
        private bool IsFFprobeAvailable()
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _ffprobePath,
                        Arguments = "-version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                if (process.WaitForExit(TimeSpan.FromSeconds(5)))
                {
                    return process.ExitCode == 0;
                }
                else
                {
                    try { process.Kill(); } catch { }
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Метод определения пути к утилите FFmpeg.
        /// </summary>
        private static string GetFFmpegPath()
        {
            if (OperatingSystem.IsWindows())
            {
                string localPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
                if (File.Exists(localPath))
                    return localPath;

                var pathEnv = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
                foreach (var dir in pathEnv.Split(Path.PathSeparator))
                {
                    string fullPath = Path.Combine(dir, "ffmpeg.exe");
                    if (File.Exists(fullPath))
                        return fullPath;
                }

                return "ffmpeg.exe";
            }

            return "ffmpeg";
        }

        /// <summary>
        /// Метод определения пути к утилите FFprobe.
        /// </summary>
        private static string GetFFprobePath()
        {
            if (OperatingSystem.IsWindows())
            {
                string localPath = Path.Combine(AppContext.BaseDirectory, "ffprobe.exe");
                if (File.Exists(localPath))
                    return localPath;

                var pathEnv = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
                foreach (var dir in pathEnv.Split(Path.PathSeparator))
                {
                    string fullPath = Path.Combine(dir, "ffprobe.exe");
                    if (File.Exists(fullPath))
                        return fullPath;
                }

                return "ffprobe.exe";
            }

            return "ffprobe";
        }

        /// <summary>
        /// Путь к утилите FFmpeg.
        /// </summary>
        private readonly string _ffmpegPath = GetFFmpegPath();

        /// <summary>
        /// Путь к утилите FFprobeg.
        /// </summary>
        private readonly string _ffprobePath = GetFFprobePath();

        private readonly FileLockManager _lockManager = lockManager;
    }
}