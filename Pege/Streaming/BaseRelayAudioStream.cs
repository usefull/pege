using Pege.Entities;
using Pege.Extensions;
using System.Buffers;
using System.Text;

namespace Pege.Streaming
{
    /// <summary>
    /// Базовый фнкцинал стрима-ретранслятора аудио.
    /// </summary>
    internal abstract class BaseRelayAudioStream : Stream<RelayAudioStreamStatus, AudioChunk>
    {
        /// <summary>
        /// Конструктор.
        /// </summary>
        /// <param name="status">Информация о стриме.</param>
        /// <param name="serviceProvider">Провайдер сервисов DI.</param>
        public BaseRelayAudioStream(RelayAudioStreamStatus status, IServiceProvider serviceProvider) : base(status, serviceProvider)
        {
            CastedStatus.Uri = status.Uri;
            CastedStatus.MetadataSwap = status.MetadataSwap;
            Status.ContentType = "audio/mpeg";
        }

        /// <summary>
        /// Метод ретрансляции.
        /// </summary>
        /// <param name="networkStream">Сетевой источник ретранслируемых данных.</param>
        /// <param name="bufferSize">Размер буфера.</param>
        /// <param name="streamReadTimeout">Таймаут чтения источника.</param>
        /// <param name="bitrate">Битрейт аудио.</param>
        /// <param name="metaInterval">Интервал между блоками металанных.</param>
        /// <param name="cancellationToken">Токен отмены ретрансляции.</param>
        /// <returns></returns>
        protected virtual async Task RelayCycleAsync(Stream networkStream, int bufferSize, TimeSpan streamReadTimeout, int bitrate, int metaInterval, CancellationToken cancellationToken)
        {
            var networkBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            var bytesPerMs = bitrate / 8.0;

            byte[]? metadata = null;

            try
            {
                int metadataLeft = 0;
                int metadataLength = 0;
                int bytesAfterMetadata = 0;

                while (true)
                {
                    var bytesRead = await networkStream.ReadAsync(networkBuffer.AsMemory(0, bufferSize), cancellationToken)
                                                    .AsTask()
                                                    .WaitAsync(streamReadTimeout, cancellationToken);

                    if (bytesRead == 0) break;

                    if (metaInterval > 0)
                    {
                        var bufferPos = 0;
                        while (bufferPos < bytesRead)
                        {
                            if (metadataLeft > 0)
                            {
                                // нужно читать метаданные
                                if (metadataLeft <= bytesRead - bufferPos)
                                {
                                    // метаданные заканчиваются в текущем батче
                                    networkBuffer.AsSpan(bufferPos, metadataLeft).CopyTo(metadata.AsSpan(metadataLength - metadataLeft));
                                    bufferPos += metadataLeft;
                                    metadataLeft = 0;

                                    var strMetadata = Encoding.UTF8.GetString(metadata!);
                                    ArrayPool<byte>.Shared.Return(metadata!);
                                    metadata = null;

                                    UpdateStatusWithMetadata(strMetadata);
                                }
                                else
                                {
                                    // метаданные переходят на следующий батч
                                    networkBuffer.AsSpan(bufferPos, bytesRead - bufferPos).CopyTo(metadata.AsSpan(metadataLength - metadataLeft));
                                    metadataLeft -= bytesRead - bufferPos;
                                    bufferPos += bytesRead - bufferPos;
                                }
                            }
                            else
                            {
                                // мотаем дальше
                                if (metaInterval - bytesAfterMetadata < bytesRead - bufferPos)
                                {
                                    // метаданные есть в оставшемся батче
                                    if (metaInterval - bytesAfterMetadata > 0)
                                    { // есть аудиоданные в начале текущего батча

                                        byte[] buff = GC.AllocateUninitializedArray<byte>(metaInterval - bytesAfterMetadata);
                                        networkBuffer.AsSpan(bufferPos, metaInterval - bytesAfterMetadata).CopyTo(buff);
                                        BroadcastChunk(new AudioChunk{
                                            Data = buff,
                                            StreamMetadata = GenerateMetadataString().ToIcyMetadata()
                                        });
                                    }

                                    bufferPos += metaInterval - bytesAfterMetadata;
                                    metadataLength = networkBuffer[bufferPos] * 16;
                                    bufferPos += 1;
                                    if (metadataLength == 0)
                                    {
                                        bytesAfterMetadata = 0;
                                        //log.Information("Пустой блок метаданных");
                                    }
                                    else
                                    {
                                        metadata = ArrayPool<byte>.Shared.Rent(metadataLength);
                                        metadataLeft = metadataLength;
                                        bytesAfterMetadata = 0;
                                    }
                                }
                                else
                                {
                                    // метаданных в оставшемся батче нет
                                    byte[] buff = GC.AllocateUninitializedArray<byte>(bytesRead - bufferPos);
                                    networkBuffer.AsSpan(bufferPos, bytesRead - bufferPos).CopyTo(buff);
                                    BroadcastChunk(new AudioChunk
                                    {
                                        Data = buff,
                                        StreamMetadata = GenerateMetadataString().ToIcyMetadata()
                                    });

                                    bytesAfterMetadata += bytesRead - bufferPos;
                                    bufferPos += bytesRead - bufferPos;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Выделяем память без предварительного зануления
                        byte[] dedicatedBuffer = GC.AllocateUninitializedArray<byte>(bytesRead);

                        // Быстрое копирование через Span
                        networkBuffer.AsSpan(0, bytesRead).CopyTo(dedicatedBuffer);

                        BroadcastChunk(new AudioChunk
                        {
                            Data = dedicatedBuffer,
                            StreamMetadata = GenerateMetadataString().ToIcyMetadata()
                        });
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(networkBuffer);
                if (metadata != null)
                {
                    ArrayPool<byte>.Shared.Return(metadata);
                }
            }
        }

        /// <summary>
        /// Метод обновления статуса потока информацией из метаданных ретранслируемого потока.
        /// </summary>
        /// <param name="strMetadata">Строка метаданных.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        private void UpdateStatusWithMetadata(string strMetadata)
        {
            var streamTitle = strMetadata.Split(';').FirstOrDefault(s => s.StartsWith("StreamTitle='"));
            if (streamTitle == null)
            {
                lock (CastedStatus)
                {
                    CastedStatus.Track = string.Empty;
                    CastedStatus.Artist = string.Empty;
                }
            }
            else
            {
                var start = streamTitle.IndexOf('\'') + 1;
                var end = streamTitle.LastIndexOf('\'');
                if (start < 1 || end < 0)
                {
                    lock (CastedStatus)
                    {
                        CastedStatus.Track = string.Empty;
                        CastedStatus.Artist = string.Empty;
                    }
                }
                else
                {
                    var parts = streamTitle[start..end]
                        ?.Split(" - ").Select(s => s.Trim()).ToList();

                    lock (CastedStatus)
                    {
                        if (CastedStatus.MetadataSwap ?? false)
                        {
                            if (parts?.Count > 0)
                                CastedStatus.Track = parts[0];

                            if (parts?.Count > 1)
                                CastedStatus.Artist = parts[1];
                        }
                        else
                        {
                            if (parts?.Count > 0)
                                CastedStatus.Artist = parts[0];

                            if (parts?.Count > 1)
                                CastedStatus.Track = parts[1];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Метод формирования строки метаданных.
        /// </summary>
        /// <returns>Строка с метаданными.</returns>
        private string GenerateMetadataString()
        {
            lock (CastedStatus)
            {
                return $"StreamTitle='{CastedStatus.Artist} - {CastedStatus.Track}';";
            }
        }
            
    }
}
