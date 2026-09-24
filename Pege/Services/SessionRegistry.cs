using Microsoft.EntityFrameworkCore;
using Pege.Data;
using Pege.Entities;
using Pege.Interfaces;
using Pege.Resource;
using Serilog;
using System.Collections.Concurrent;

namespace Pege.Services
{
    /// <summary>
    /// Сервис регистрации состояния сессий слушателей в БД.
    /// </summary>
    public sealed class SessionRegistry : ISessionRegistry, IDisposable
    {
        /// <summary>
        /// Состояния сессий, ожидающих обовления в БД.
        /// </summary>
        private ConcurrentDictionary<Guid, SessionInfo> sessions = new();

        /// <summary>
        /// Кешированные геоданные IP-адресов.
        /// </summary>
        private ConcurrentDictionary<string, string> ipGeo = new();

        /// <summary>
        /// Провайдер сервисов DI.
        /// </summary>
        private readonly IServiceProvider serviceProvider;

        /// <summary>
        /// Механизм остановки периодического обновления БД при уничтожении сервиса.
        /// </summary>
        private readonly CancellationTokenSource cts = new();

        /// <summary>
        /// Конструктор.
        /// </summary>
        /// <param name="serviceProvider">Провайдер сервисов DI.</param>
        public SessionRegistry(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            _ = PeriodicFlushAsync(cts.Token);
        }

        /// <summary>
        /// Метод регистрации новой сессии.
        /// </summary>
        /// <param name="id">Идентификатор сессии.</param>
        /// <param name="streamId">Идентификатор потока.</param>
        /// <param name="ip">IP-адрес.</param>
        /// <param name="userAgent">Строка User-Agent</param>
        /// <returns>Задача, представляющая асинхронную операцию регистрации новой сессии.</returns>
        public async Task RegisterNewAsync(Guid id, string streamId, string? ip, string userAgent)
        {
            var connected = DateTime.UtcNow;
            _ = sessions.GetOrAdd(id, new SessionInfo
            {
                Id = id,
                IsNew = true,
                StreamId = streamId,
                Ip = ip,
                UserAgent = userAgent,
                Connected = connected,
            });
        }

        /// <summary>
        /// Метод помечает все активные сессии как закрытые по причине остановки сервера.
        /// </summary>
        public async Task SetAllClosedAsync()
        {
            try
            {
                using var scope = serviceProvider.CreateAsyncScope();
                var dataContext = scope.ServiceProvider.GetService<DataContext>();
                if (dataContext == null) return;
                await dataContext.Sessions.Where(s => s.Closed == null).ExecuteUpdateAsync(b =>
                {
                    b.SetProperty(s => s.Closed, s => s.Updated == null ? DateTime.UtcNow : s.Updated);
                    b.SetProperty(s => s.CloseReason, SessionCloseReason.ServerRestart);
                });
            }
            catch (Exception ex)
            {
                Log.Error(string.Format(Error.AllActiveSessionSetClosedError, ex.Message));
            }
        }

        public async Task SetClosedAsync(Guid id, SessionCloseReason reason = SessionCloseReason.ClientDisconnect)
        {
            var session = sessions.GetOrAdd(id, new SessionInfo
            {
                Id = id
            });
            session.Close(reason);
        }

        public async Task UpdateAsync(Guid id, int bytesSent)
        {
            var session = sessions.GetOrAdd(id, new SessionInfo
            {
                Id = id
            });
            session.AddBytes(bytesSent);
        }

        private async Task PeriodicFlushAsync(CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    using var scope = serviceProvider.CreateAsyncScope();
                    var geoService = scope.ServiceProvider.GetService<IGeoIpService>();
                    var dataContext = scope.ServiceProvider.GetService<DataContext>();
                    if (dataContext == null) continue;

                    var snapshot = Interlocked.Exchange(ref sessions, new ConcurrentDictionary<Guid, SessionInfo>());
                    if (snapshot.IsEmpty) continue;

                    var toInsert = new List<Session>();
                    foreach (var si in snapshot.Values.Where(si => si.IsNew))
                    {
                        var s = si.ToSession();
                        var ip = s?.Ip ?? string.Empty;

                        if (!ipGeo.TryGetValue(ip, out var geo))
                        {
                            geo = geoService != null ? await geoService.GetGeoFromIpAsync(s?.Ip, cancellationToken) : null;
                            if (geo != null)
                                ipGeo.TryAdd(ip, geo);
                        }

                        s?.Geo = geo;
                        toInsert.Add(s);
                    }

                    await dataContext.Sessions.AddRangeAsync(toInsert, cancellationToken);

                    var countToUpdate = snapshot.Values.Count(s => !s.IsNew);
                    if (countToUpdate > 0)
                    {
                        var ids = new List<Guid>(countToUpdate);
                        var toUpdate = new List<SessionInfo>(countToUpdate);

                        foreach (var si in snapshot.Values)
                        {
                            if (!si.IsNew)
                            {
                                toUpdate.Add(si);
                                ids.Add(si.Id);
                            }
                        }

                        var entities = await dataContext.Sessions
                            .Where(s => ids.Contains(s.Id))
                            .ToDictionaryAsync(s => s.Id, cancellationToken);

                        foreach (var si in toUpdate)
                        {
                            if (!entities.TryGetValue(si.Id, out var entity)) continue;
                            entity.Updated = si.Updated;
                            entity.BytesSent += si.BytesSent;
                            entity.Closed = si.Closed;
                            entity.CloseReason = si.CloseReason;
                        }
                    }

                    await dataContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format(Error.SessionFlushError, ex.Message));
                }
            }
        }

        public void Dispose()
        {
            cts.Cancel();
        }
    }
}