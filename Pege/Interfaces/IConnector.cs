namespace Pege.Interfaces
{
    /// <summary>
    /// Интерфейс коннектора стрима и контроллера.
    /// </summary>
    internal interface IConnector
    {
        /// <summary>
        /// Метод соединения стрима и контроллера.
        /// </summary>
        /// <param name="stream">Стрим.</param>
        /// <param name="httpRequest">HTTP-запрос.</param>
        /// <param name="httpResponse">HTTP-ответ.</param>
        /// <param name="cancellationToken">Токен остановки.</param>
        Task ConsumeAsync(IStream stream, HttpRequest httpRequest, HttpResponse httpResponse, CancellationToken cancellationToken);
    }
}
