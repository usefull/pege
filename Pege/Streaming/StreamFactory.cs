using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pege.Data;
using Pege.Entities;
using Pege.Exceptions;
using Pege.Interfaces;
using Pege.Resource;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace Pege.Streaming
{
    public class StreamFactory(IDbContextFactory<DataContext> dataContextFactory, IConfiguration configuration, IServiceProvider serviceProvider)
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private readonly ConcurrentDictionary<string, IStream> _streams = new();

        public async Task<IStream> CreateAsync(string id)            
        {
            var streamInfo = await GetStreamInfoAsync(id);
            
            if (streamInfo == null)
            {
                Destroy(id);
                throw new UnknownStreamException(Error.UnknownStreamId);
            }

            var semaphore = _locks.GetOrAdd(streamInfo.Id!, _ => new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync();
            try
            {
                if (_streams.TryGetValue(streamInfo.Id!, out var value))
                    return value;

                var streamType = Type.GetType($"Pege.Streaming.{streamInfo.ImplType}")
                    ?? throw new InvalidOperationException(string.Format(Error.UnknownStreamType, streamInfo.ImplType, streamInfo.Id));

                if (Activator.CreateInstance(streamType, streamInfo.ToStatus(), serviceProvider) is not IStream instance)
                    throw new InvalidOperationException(string.Format(Error.InadequateStreamType, streamInfo.ImplType, streamInfo.Id));

                instance.Start();
                _streams[streamInfo.Id!] = instance;

                _ = ResetStreamStopedAsync(streamInfo.Id!);

                return instance;
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException == null)
                    throw;
                throw ex.InnerException;
            }
            finally
            {
                semaphore.Release();
            }            
        }

        public async Task CreateAllAsync()
        {
            using var db = dataContextFactory.CreateDbContext();
            var items = await db.Streams.AsNoTracking().ToListAsync();

            foreach (var i in items)
                await CreateAsync(i.Id!);
        }

        public async Task DestroyAllAsync()
        {
            using var db = dataContextFactory.CreateDbContext();
            var items = await db.Streams.AsNoTracking().ToListAsync();

            foreach (var i in items)
                await DestroyAsync(i.Id!);
        }

        public async Task<IStream> GetStreamAsync(string streamId)
        {
            var streamInfo = GetStreamInfoAsync(streamId).GetAwaiter().GetResult()
                ?? throw new UnknownStreamException();

            if (!_streams.TryGetValue(streamInfo.Id!, out var stream))
                throw new StreamUnavailableException(Error.StreamUnavailable);

            return stream;
        }

        public async Task<StreamStatus?> GetStreamStatusAsync(string id)
        {
            IStream stream;
            StreamStatus status;

            try
            {
                stream = await GetStreamAsync(id);
                status = stream.Status;
            }
            catch (InvalidOperationException)
            {
                using var db = dataContextFactory.CreateDbContext();
                var info = await db.Streams.FirstOrDefaultAsync(s => s.Id == id.ToLower().Trim());
                if (info == null) return null;

                status = info.ToStatus();
            }
            catch
            {
                throw;
            }

            return status;
        }

        public async Task DestroyAsync(string id)
        {            
            var streamInfo = await GetStreamInfoAsync(id);
            Destroy(id);

            if (streamInfo == null)
                throw new UnknownStreamException(Error.UnknownStreamId);
        }

        public async Task<IEnumerable<StreamStatus>> ListAsync()
        {
            using var db = dataContextFactory.CreateDbContext();
            var items = await db.Streams.AsNoTracking().ToListAsync();

            var results = new List<StreamStatus>();

            foreach (var si in items)
            {
                try
                {
                    var stream = await GetStreamAsync(si.Id!);
                    results.Add(stream.Status);
                }
                catch
                {
                    results.Add(si.ToStatus());
                }
            }
            return results;
        }

        public async Task<Stream> ListAsCsvAsync(bool originalUri = false)
        {
            using var db = dataContextFactory.CreateDbContext();
            var items = await db.Streams.AsNoTracking().ToListAsync();

            var result = items.Aggregate(new StringBuilder(), (acc, i) =>
            {
                var defaultUri = $"{configuration["BaseUri"]}/stream/{i.Id}";
                var title = i.Title;
                var uri = i is RelayStreamInfo ri && originalUri
                    ? ri.Uri
                    : defaultUri;

                acc.AppendLine($"{title}\t{uri}\t0");

                return acc;
            });

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(result.ToString()))
            {
                Position = 0
            };
            return stream;
        }

        public async Task<StreamStatus> RegisterAsync(StreamInfo info)
        {
            info.Registered = DateTime.UtcNow;

            using var db = dataContextFactory.CreateDbContext();
            var entity = await db.Streams.AddAsync(info);
            await db.SaveChangesAsync();

            return entity.Entity.ToStatus();
        }

        public async Task<StreamStatus> UpdateAsync(StreamInfo info)
        {
            var isActive = true;

            try
            {
                _ = await GetStreamAsync(info.Id!);
            }
            catch (StreamUnavailableException)
            {
                isActive = false;
            }

            try
            {
                using var db = dataContextFactory.CreateDbContext();
                var dbInfo = await db.Streams.FirstOrDefaultAsync(s => s.Id == info.Id)
                    ?? throw new UnknownStreamException();

                dbInfo.CopyFrom(info);

                await db.SaveChangesAsync();

                return dbInfo.ToStatus();
            }
            finally
            {
                if (isActive)
                {
                    Destroy(info.Id!);
                    await CreateAsync(info.Id!);
                }
            }
        }

        public async Task DeleteAsync(string streamId)
        {
            using var db = dataContextFactory.CreateDbContext();
            await db.Streams.Where(s => s.Id == streamId).ExecuteDeleteAsync();
            Destroy(streamId);
        }

        private async Task<StreamInfo?> GetStreamInfoAsync(string id)
        {
            var i = id.ToLower().Trim();
            using var db = dataContextFactory.CreateDbContext();          
            return await db.Streams.AsNoTracking().FirstOrDefaultAsync(s => s.Id == i);
        }

        private async Task ResetStreamStopedAsync(string id)
        {
            using var db = dataContextFactory.CreateDbContext();
            await db.Streams.Where(si => si.Id == id.ToLower().Trim())
                .ExecuteUpdateAsync(s => s.SetProperty(si => si.Stopped, si => null));
            ;
        }

        private void Destroy(string id)
        {
            _locks.TryRemove(id, out _);
            if (_streams.TryRemove(id, out var stream))
            {
                stream.Dispose();
            }
        }
    }
}
