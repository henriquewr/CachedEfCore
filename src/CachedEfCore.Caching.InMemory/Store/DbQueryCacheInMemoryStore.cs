using CachedEfCore.Cache.Metrics;
using CachedEfCore.Cache.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CachedEfCore.Caching.InMemory.Store
{
    public class DbQueryCacheInMemoryStore : IDbQueryCacheStore
    {
        private readonly DbContext _dbContext;
        internal readonly DbQueryCacheInMemoryInternalStore _dbQueryCacheInternalStore;
        private readonly IDbQueryCacheMetrics _metrics;
        private readonly DbContextId _dbContextId;

        public DbQueryCacheInMemoryStore(DbContext dbContext, 
            DbQueryCacheInMemoryInternalStore dbQueryCacheStore, 
            IDbQueryCacheMetrics metrics)
        {
            _dbContext = dbContext;
            _dbQueryCacheInternalStore = dbQueryCacheStore;
            _metrics = metrics;
            _dbContextId = dbContext.ContextId;
        }

        private void Reset()
        {
            _dbQueryCacheInternalStore.RemoveAllDbContextDependent(_dbContextId);
        }

        public void Dispose()
        {
            Reset();

            GC.SuppressFinalize(this);
        }

        public ValueTask DisposeAsync()
        {
            Reset();

            GC.SuppressFinalize(this);

            return ValueTask.CompletedTask;
        }
        ~DbQueryCacheInMemoryStore()
        {
            Reset();
        }

        public void ResetState() => Reset();

        public Task ResetStateAsync(CancellationToken cancellationToken = default)
        {
            Reset();
            return Task.CompletedTask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAllByTransactionId(Guid transactionId)
            => _dbQueryCacheInternalStore.RemoveAllByTransactionId(transactionId);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask RemoveAllByTransactionIdAsync(Guid transactionId)
        {
            _dbQueryCacheInternalStore.RemoveAllByTransactionId(transactionId);
            return ValueTask.CompletedTask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAllDbContextDependent(DbContextId dbContextId)
            => _dbQueryCacheInternalStore.RemoveAllDbContextDependent(dbContextId);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask RemoveAllDbContextDependentAsync(DbContextId dbContextId)
        {
            _dbQueryCacheInternalStore.RemoveAllDbContextDependent(dbContextId);
            return ValueTask.CompletedTask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveRootEntities(HashSet<IEntityType> entitiesToRemove) 
            => _dbQueryCacheInternalStore.RemoveRootEntities(entitiesToRemove, _dbContext);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask RemoveRootEntitiesAsync(HashSet<IEntityType> entitiesToRemove)
        {
            _dbQueryCacheInternalStore.RemoveRootEntities(entitiesToRemove, _dbContext);
            return ValueTask.CompletedTask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveDependentEntities(IEnumerable<IEntityType> entitiesToRemove)
            => _dbQueryCacheInternalStore.RemoveDependentEntities(entitiesToRemove);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask RemoveDependentEntitiesAsync(IEnumerable<IEntityType> entitiesToRemove)
        {
            _dbQueryCacheInternalStore.RemoveDependentEntities(entitiesToRemove);
            return ValueTask.CompletedTask;
        }

        public IPendingEntities AddPendingEntities(IEnumerable<IEntityType> entitiesToAdd)
            => _dbQueryCacheInternalStore.AddPendingEntities(entitiesToAdd);
        public ValueTask<IPendingEntities> AddPendingEntitiesAsync(IEnumerable<IEntityType> entitiesToAdd)
        {
            var pendingEntities = _dbQueryCacheInternalStore.AddPendingEntities(entitiesToAdd);
            return new(pendingEntities);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAll()
            => _dbQueryCacheInternalStore.RemoveAll();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask RemoveAllAsync()
        {
            _dbQueryCacheInternalStore.RemoveAll();
            return ValueTask.CompletedTask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddToCache<T>(Type rootEntityType, IDbQueryCacheKey key, Guid? transactionId, T? dataToCache)
            => _dbQueryCacheInternalStore.AddToCache(_dbContext, rootEntityType, key, transactionId, dataToCache);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? GetCached<T>(IDbQueryCacheKey key)
            => _dbQueryCacheInternalStore.GetCached<T>(_dbContext, key, _metrics);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetOrAdd<T>(Type rootEntityType, IDbQueryCacheKey key, Guid? transactionId, Func<T> create)
            => _dbQueryCacheInternalStore.GetOrAdd(_dbContext, rootEntityType, key, transactionId, create, _metrics);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<T> GetOrAddAsync<T>(Type rootEntityType, IDbQueryCacheKey key, Guid? transactionId, Func<Task<T>> create)
            => _dbQueryCacheInternalStore.GetOrAddAsync(_dbContext, rootEntityType, key, transactionId, create, _metrics);
    }
}
