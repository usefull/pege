namespace Pege.Data
{
    /// <summary>
    /// Причина закрытия сессии слушателя.
    /// </summary>
    public enum SessionCloseReason
    {
        /// <summary>
        /// Клиент отключился штатно.
        /// </summary>
        ClientDisconnect = 0,

        /// <summary>
        /// Сессия закрыта при перезапуске сервера.
        /// </summary>
        ServerRestart = 1,

        /// <summary>
        /// Сессия закрыта по таймауту (клиент перестал получать данные).
        /// </summary>
        Timeout = 2,

        /// <summary>
        /// Сессия закрыта из-за ошибки при передаче данных.
        /// </summary>
        Error = 3,

        /// <summary>
        /// Сессия закрыта администратором.
        /// </summary>
        Kicked = 4
    }
}