
using Pege.Entities;
using Pege.Resource;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Pege.Services
{
    /// <summary>
    /// Сервис FFmpeg для работы с медиафайлами.
    /// </summary>
    /// <remarks>
    /// Конструктор.
    /// </remarks>
    internal class FFmpegService()
    {
        /// <summary>
        /// Логгер.
        /// </summary>
        public Serilog.ILogger? Log { get; set; }

        /// <summary>
        /// Метод подготавливает трек к воспроизведению.
        /// </summary>
        /// <param name="filePath">Путь к файлу трека.</param>
        /// <returns>Данные, необходимые для воспроизведения.</returns>
        public async Task<TrackData> PrepareAacTrackAsync(string filePath, int targetSampleRate, int framesPerChunk, CancellationToken cancellationToken)
        {
            string? tempOutput = null;

            try
            {
                var result = await GetTrackMetadataAsync(filePath, cancellationToken);

                ReadOnlyMemory<byte> trackBytes;

                if (result.Codec == "aac" && result.SampleRate == targetSampleRate)
                    trackBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                else
                {
                    Log?.Information(string.Format(Message.Encoding, filePath, targetSampleRate));

                    tempOutput = Path.Combine(Path.GetTempPath(), $"output_{Guid.NewGuid()}.m4a");

                    var fromFlac = result.Codec == "flac" ? "-metadata comment=\"from FLAC\"" : string.Empty;

                    string arguments = $"-i \"{filePath}\" -ar {targetSampleRate} -af loudnorm=I=-18:TP=-1.5:linear=true -c:a libfdk_aac -vbr 5 -vn -movflags +faststart {fromFlac} \"{tempOutput}\"";
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

                    result = await GetTrackMetadataAsync(tempOutput, cancellationToken);

                    trackBytes = await File.ReadAllBytesAsync(tempOutput, cancellationToken);
                    if (trackBytes.Length == 0) throw new Exception(string.Format(Error.FFmpegOutputError, filePath));
                    
                    var directory = Path.GetDirectoryName(filePath);
                    var fileName = $"{Path.GetFileNameWithoutExtension(filePath)}.m4a";
                    string targetPath = Path.Combine(directory, fileName);
                    result.Filename = targetPath;

                    try { File.Delete(filePath); } catch { }

                    File.Copy(tempOutput, targetPath, true);

                    try { File.Delete(tempOutput); } catch { }
                    
                }

                result.Chunks = ConvertM4AToAdtsChunks(trackBytes, framesPerChunk);

                return result;
            }
            catch (OperationCanceledException) { throw; }
            finally
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(tempOutput) && File.Exists(tempOutput))
                        File.Delete(tempOutput);
                }
                catch (Exception ex)
                {
                    Log?.Warning(string.Format(Error.TempFilesDeleteError, ex.Message));
                }
            }
        }

        /// <summary>
        /// Метод читает метаданные трека.
        /// </summary>
        /// <param name="filePath">Путь к файлу трека.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Информация о треке.</returns>
        public async Task<TrackData> GetTrackMetadataAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(Error.FileNotFound);

            var result = new TrackData
            {
                Filename = filePath,
                Title = Path.GetFileNameWithoutExtension(filePath)
            };

            string arguments = $"-v error -select_streams a -show_entries stream=sample_rate:packet=duration:stream=codec_name:format=duration:format_tags=artist,title -read_intervals %+1 -of ini \"{filePath}\"";

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
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                throw new Exception(string.Format(Error.FFprobeError, errorBuilder));
            }

            string output = outputBuilder.ToString();
            var lines = output.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries);

            string section = string.Empty;

            foreach (var line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (line.StartsWith('['))
                    section = line;
                else
                {
                    int separatorIndex = line.IndexOf('=');
                    if (separatorIndex == -1) continue;

                    string key = line[..separatorIndex].Trim();
                    string value = line[(separatorIndex + 1)..].Trim();

                    if (section.Contains("packet") && result.SamplesPerFrame == 0)
                    {
                        if (key.Equals("duration", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var d))
                            result.SamplesPerFrame = d;
                    }
                    else if (section == "[format]")
                    {
                        if (key.Equals("duration", StringComparison.OrdinalIgnoreCase) && double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                            result.Duration = TimeSpan.FromSeconds(d);
                    }
                    else
                    {
                        if (key.Equals("sample_rate", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var d))
                            result.SampleRate = d;
                        else if (key.Equals("codec_name", StringComparison.OrdinalIgnoreCase))
                            result.Codec = value;
                        else if (key.Equals("title", StringComparison.OrdinalIgnoreCase))
                            result.Title = value;
                        else if (key.Equals("artist", StringComparison.OrdinalIgnoreCase))
                            result.Artist = value;
                        else if (key.Equals("comment", StringComparison.OrdinalIgnoreCase))
                            result.FromFlac = value.Contains("from FLAC");
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(result.Codec))
                throw new Exception(Error.UnableDefineCodec);
            if (result.SampleRate == 0)
                throw new Exception(Error.UnableDefineSampleRate);
            if (result.SamplesPerFrame == 0)
                throw new Exception(Error.UnableDefineSamplesPerFrame);

            return result;
        }

        /// <summary>
        /// Метод проверки доступности утилиты FFmpeg.
        /// </summary>
        public bool IsFFmpegAvailable()
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
        public bool IsFFprobeAvailable()
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
        /// Конвертирует M4A VBR данные в чанки ADTS полностью в памяти без использования временных файлов на диске.
        /// </summary>
        public Queue<ReadOnlyMemory<byte>> ConvertM4AToAdtsChunks(ReadOnlyMemory<byte> m4aData, int framesPerChunk)
        {
            if (m4aData.IsEmpty) throw new ArgumentException("M4A data is empty.", nameof(m4aData));
            if (framesPerChunk <= 0) throw new ArgumentException("Frames per chunk must be greater than 0.", nameof(framesPerChunk));

            // Настройка кроссплатформенного запуска ffmpeg
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                // -i pipe:0 -> принимать данные на вход из stdin
                // -c:a copy -> копировать аудио-поток БЕЗ перекодирования (очень быстро)
                // -f adts   -> упаковать поток в формат ADTS (.aac фреймы с заголовками)
                // pipe:1    -> выводить результат в stdout
                Arguments = "-f mp4 -i pipe:0 -c:a copy -f adts pipe:1",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true, // Важно перенаправить, чтобы читать логи ошибок при сбоях
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to start ffmpeg. Ensure it is installed and available in PATH.", ex);
            }

            // В продакшене запись в stdin и чтение из stdout должны происходить асинхронно или параллельно,
            // чтобы избежать взаимной блокировки (deadlock) при заполнении буферов ОС.
            using var outputStream = new MemoryStream();

            var writeTask = Task.Run(() =>
            {
                using var stdin = process.StandardInput.BaseStream;
                stdin.Write(m4aData.Span);
                stdin.Flush();
                // Закрываем поток ввода, чтобы ffmpeg понял, что файл закончился и завершил обработку
            });

            var readTask = Task.Run(() =>
            {
                using var stdout = process.StandardOutput.BaseStream;
                stdout.CopyTo(outputStream);
            });

            // Ждем завершения потоков ввода-вывода и самого процесса
            Task.WaitAll(writeTask, readTask);
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                string errorLog = process.StandardError.ReadToEnd();
                throw new InvalidOperationException($"FFmpeg exited with error code {process.ExitCode}. Log: {errorLog}");
            }

            // Нарезаем полученный ADTS поток на чанки по количеству фреймов
            return SplitAdtsToChunks(outputStream.ToArray(), framesPerChunk);
        }

        private static Queue<ReadOnlyMemory<byte>> SplitAdtsToChunks(byte[] adtsData, int framesPerChunk)
        {
            var chunks = new Queue<ReadOnlyMemory<byte>>();
            if (adtsData.Length == 0) return chunks;

            var span = adtsData.AsSpan();
            int index = 0;

            // Список смещений начала каждого ADTS фрейма
            var framePositions = new List<(int Offset, int Length)>();

            while (index < span.Length - 6) // ADTS заголовок минимум 7 байт
            {
                // Проверка маркера синхронизации ADTS (12 бит установлены в 1: 0xFF и старшие 4 бита следующего байта)
                if (span[index] == 0xFF && (span[index + 1] & 0xF0) == 0xF0)
                {
                    // Извлекаем длину фрейма из заголовка (13 бит, хранятся в байтах 3, 4, 5)
                    // байт 3 (младшие 2 бита) + байт 4 (все 8 бит) + байт 5 (старшие 3 бита)
                    int frameLength = ((span[index + 3] & 0x03) << 11) |
                                      (span[index + 4] << 3) |
                                      ((span[index + 5] & 0xE0) >> 5);

                    if (frameLength <= 0 || index + frameLength > span.Length)
                    {
                        // Защита от битых данных — если длина некорректна, двигаемся побайтово
                        index++;
                        continue;
                    }

                    framePositions.Add((index, frameLength));
                    index += frameLength; // Прыгаем на следующий фрейм
                }
                else
                {
                    index++;
                }
            }

            // Группируем фреймы в чанки
            int currentFrameIdx = 0;
            while (currentFrameIdx < framePositions.Count)
            {
                int framesInChunk = Math.Min(framesPerChunk, framePositions.Count - currentFrameIdx);

                int chunkStartOffset = framePositions[currentFrameIdx].Offset;
                int lastFrameIdx = currentFrameIdx + framesInChunk - 1;
                int chunkEndOffset = framePositions[lastFrameIdx].Offset + framePositions[lastFrameIdx].Length;
                int chunkLength = chunkEndOffset - chunkStartOffset;

                // Выделяем память под конкретный чанк и копируем туда сгруппированные фреймы
                byte[] chunkBuffer = new byte[chunkLength];
                adtsData.AsSpan(chunkStartOffset, chunkLength).CopyTo(chunkBuffer);

                chunks.Enqueue(chunkBuffer);
                currentFrameIdx += framesInChunk;
            }

            return chunks;
        }

        /// <summary>
        /// Путь к утилите FFmpeg.
        /// </summary>
        private readonly string _ffmpegPath = GetFFmpegPath();

        /// <summary>
        /// Путь к утилите FFprobeg.
        /// </summary>
        private readonly string _ffprobePath = GetFFprobePath();
    }
}