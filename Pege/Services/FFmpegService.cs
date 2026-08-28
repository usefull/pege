using Pege.Entities;
using Pege.Resource;
using System.Diagnostics;
using System.Text;

namespace Pege.Services
{
    /// <summary>
    /// Сервис для работы с медиафайлами.
    /// </summary>
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
                var result = GetTrackMetadata(filePath);

                ReadOnlyMemory<byte> trackBytes;

                if (result.Codec == "aac" && result.SampleRate == targetSampleRate)
                    trackBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                else
                {
                    Log?.Information(string.Format(Message.Encoding, filePath, targetSampleRate));

                    tempOutput = Path.Combine(Path.GetTempPath(), $"output_{Guid.NewGuid()}.m4a");

                    var fromFlac = result.Codec == "flac" ? "-metadata comment=\"from FLAC\"" : string.Empty;

                    string arguments = $"-i \"{filePath}\" -ar {targetSampleRate} -af loudnorm=I=-18:TP=-1.5:linear=true -c:a libfdk_aac -vbr 4 -vn -movflags +faststart {fromFlac} \"{tempOutput}\"";
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

                    result = GetTrackMetadata(tempOutput);

                    trackBytes = await File.ReadAllBytesAsync(tempOutput, cancellationToken);
                    if (trackBytes.Length == 0) throw new Exception(string.Format(Error.FFmpegOutputError, filePath));
                    
                    var directory = Path.GetDirectoryName(filePath);
                    var fileName = $"{Path.GetFileNameWithoutExtension(filePath)}.m4a";
                    string targetPath = Path.Combine(directory!, fileName);
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
        /// <returns>Информация о треке.</returns>
        public static TrackData GetTrackMetadata(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(Error.FileNotFound);

            using var file = TagLib.File.Create(filePath);

            var result = new TrackData
            {
                Filename = filePath,
                Title = !string.IsNullOrWhiteSpace(file.Tag.Title)
                    ? file.Tag.Title
                    : Path.GetFileNameWithoutExtension(filePath),
                Artist = file.Tag.FirstPerformer ?? string.Empty,
                Duration = file.Properties.Duration,
                SampleRate = file.Properties.AudioSampleRate
            };

            string mime = file.MimeType.ToLowerInvariant();
            string comment = file.Tag.Comment ?? string.Empty;
            result.FromFlac = comment.Contains("from FLAC", StringComparison.OrdinalIgnoreCase);

            if (mime.Contains("flac"))
            {
                result.Codec = "flac";
            }
            else if (mime.Contains("mp3") || mime.Contains("mpeg"))
            {
                result.Codec = "mp3";
            }
            else if (mime.Contains("mp4") || mime.Contains("m4a") || mime.Contains("aac"))
            {
                result.Codec = "aac";
                result.SamplesPerFrame = ReadM4aAacFrameSize(filePath);
            }

            if (string.IsNullOrWhiteSpace(result.Codec))
                throw new Exception(Error.UnableDefineCodec);
            if (result.SampleRate == 0)
                throw new Exception(Error.UnableDefineSampleRate);                

            return result;
        }

        /// <summary>
        /// Метод чтения размера аудиофрейма из M4A-файла.
        /// </summary>
        /// <param name="filePath">Путь к файлу.</param>
        /// <returns>Количество аудио-сэмплов в одном аудиофрейме.</returns>
        private static int ReadM4aAacFrameSize(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new BinaryReader(fs);

                var result = FindSttsDelta(fs, reader, fs.Length);

                if (result <= 0)
                    throw new Exception();

                return result;
            }
            catch
            {
                throw new ApplicationException(string.Format(Error.UnableReadSamplesPerFrame, filePath));
            }
        }

        /// <summary>
        /// Метод ищет атом stts в M4A-файле и читает размер аудиофрейма.
        /// </summary>
        private static int FindSttsDelta(FileStream fs, BinaryReader reader, long endPosition)
        {
            byte[] nameBuffer = new byte[4];

            while (fs.Position < endPosition - 8)
            {
                long atomSize = (uint)((reader.ReadByte() << 24) | (reader.ReadByte() << 16) | (reader.ReadByte() << 8) | reader.ReadByte());

                if (fs.Read(nameBuffer, 0, 4) < 4) break;
                string atomName = Encoding.ASCII.GetString(nameBuffer);

                if (atomSize == 1)
                {
                    atomSize = (long)((ulong)reader.ReadByte() << 56 | (ulong)reader.ReadByte() << 48 |
                                     (ulong)reader.ReadByte() << 40 | (ulong)reader.ReadByte() << 32 |
                                     (ulong)reader.ReadByte() << 24 | (ulong)reader.ReadByte() << 16 |
                                     (ulong)reader.ReadByte() << 8 | reader.ReadByte());
                }

                if (atomSize == 0) atomSize = endPosition - fs.Position + 8;

                long atomEnd = fs.Position - 8 + atomSize;

                if (atomName == "moov" || atomName == "trak" || atomName == "mdia" || atomName == "minf" || atomName == "stbl")
                {
                    int result = FindSttsDelta(fs, reader, atomEnd);
                    if (result > 0) return result;
                }
                else if (atomName == "stts")
                {
                    fs.Seek(8, SeekOrigin.Current); // Пропускаем версию и флаги
                    fs.Seek(4, SeekOrigin.Current); // Пропускаем sample_count

                    int sampleDelta = (reader.ReadByte() << 24) | (reader.ReadByte() << 16) | (reader.ReadByte() << 8) | reader.ReadByte();
                    return sampleDelta > 0 ? sampleDelta : 0;
                }

                if (atomEnd > fs.Position && atomEnd <= endPosition)
                {
                    fs.Seek(atomEnd, SeekOrigin.Begin);
                }
                else
                {
                    break;
                }
            }

            return 0;
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

        /// <summary>
        /// Метод нарезает аудиоданные в чанки.
        /// </summary>
        /// <param name="adtsData">Аудиоданные.</param>
        /// <param name="framesPerChunk">Количество аудиофреймов в чанке.</param>
        /// <returns>Очередь чанков, готовая для воспроизведения.</returns>
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
    }
}