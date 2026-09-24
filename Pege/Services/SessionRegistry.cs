using Microsoft.EntityFrameworkCore;
using Pege.Data;
using Pege.Entities;
using Pege.Interfaces;
using Pege.Resource;
using Serilog;
using System.Collections.Concurrent;

namespace Pege.Services
{
    public class SessionRegistry : ISessionRegistry, IDisposable
    {
        private ConcurrentDictionary<Guid, SessionInfo> sessions = new();

        private readonly IServiceProvider serviceProvider;

        private readonly CancellationTokenSource cts = new();

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
                    var dataContext = scope.ServiceProvider.GetService<DataContext>();
                    if (dataContext == null) continue;

                    var snapshot = Interlocked.Exchange(ref sessions, new ConcurrentDictionary<Guid, SessionInfo>());
                    if (snapshot.IsEmpty) continue;

                    await dataContext.Sessions.AddRangeAsync(snapshot.Values.Where(si => si.IsNew).Select(si => si.ToSession()), cancellationToken);

                    _ = snapshot.Values.Where(si => !si.IsNew).LeftJoin(dataContext.Sessions, si => si.Id, s => s.Id, (si, s) =>
                    {
                        if (s == null) return null;
                        s.Updated = si.Updated;
                        s.BytesSent += si.BytesSent;
                        s.Closed = si.Closed;
                        s.CloseReason = si.CloseReason;
                        return s;
                    }).ToList();

                    await dataContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    Log.Error("DB flush error");
                }
            }
        }

        public void Dispose()
        {
            cts.Cancel();
        }
    }
}