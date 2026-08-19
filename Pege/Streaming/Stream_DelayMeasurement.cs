using Pege.Entities;
using Pege.Interfaces;
using Pege.Test.Core;

namespace Pege.Streaming
{
    /// <summary>
    /// Функционал измерения задержек генерации аудиочанков.
    /// </summary>
    internal abstract partial class Stream<TStatus, TChunk> : IStream
        where TStatus : StreamStatus, new()
        where TChunk : Chunk, new()
    {
        /// <summary>
        /// Метод запуска очередной эпохи измерения.
        /// </summary>
        private void StartMeasurement()
        {
            if (!_meter.IsMeasuring && _consumers.Count > _consumerRate[_measureStage])
            {
                _measureStage++;
                _meter.StartMeasuring(_consumerRate[_measureStage].ToString(), [.. _periods.Select(p => TimeSpan.FromSeconds(p))]);
                _log.Information($"{_consumerRate[_measureStage]}: measuring epoch started");

                // Если запущена последняя эпоха, подключаем обработчик завершения измерений
                if (_measureStage + 1 == _consumerRate.Length)
                {
                    _log.Information("Waiting for measurement finished ...");
                    _meter.MeasuringFinished += MeasuringFinished;
                }
            }
        }

        /// <summary>
        /// Метод обработки события завершения измерений.
        /// </summary>
        /// <remarks>Метод записывает результаты замеров в файл.</remarks>
        /// <param name="sender">Инициатор события.</param>
        /// <param name="e">Параметры события.</param>
        private void MeasuringFinished(object? sender, EventArgs e)
        {
            _meter.MeasuringFinished -= MeasuringFinished;
            _ = File.WriteAllTextAsync("storage/stream.log", _meter.Report).ContinueWith(t =>
            {
                if (t.Exception != null)
                    _log.Error($"Stream log saving error: {(t.Exception.InnerException == null ? t.Exception.Message : t.Exception.InnerException.Message)}");
                else
                    _log.Information("Stream log saved.");
            });
        }

        /// <summary>
        /// Измеритель.
        /// </summary>
        private readonly Meter _meter = new();

        /// <summary>
        /// Шаги приращения количества потребителей стрима.
        /// </summary>
        private readonly int[] _consumerRate = serviceProvider.GetService<IConfiguration>().GetSection("ConsumerRate").Get<int[]>();

        /// <summary>
        /// Периоды измерегний внутри одной эпохи.
        /// </summary>
        private readonly int[] _periods = serviceProvider.GetService<IConfiguration>().GetSection("MeasuringPeriods").Get<int[]>();

        /// <summary>
        /// Флаг, указывающий на то, что стрим в режиме измерения.
        /// </summary>
        private readonly bool _delayMeasurementMode = serviceProvider.GetService<IConfiguration>().GetSection("DelayMeasurementMode").Get<bool>();

        /// <summary>
        /// Счётчик эпох измерения.
        /// </summary>
        private int _measureStage = 0;
    }
}