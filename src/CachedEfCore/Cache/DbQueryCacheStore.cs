using CachedEfCore.Cache.EventData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CachedEfCore.Cache
{
    public class DbQueryCacheStore : IDbQueryCacheStore
    {
        private readonly IDbQueryCacheInternalStore _dbQueryCacheStore;
        private readonly Guid _dbContextId;

        public event Action<IOnInvalidatingRootEntities>? OnInvalidatingRootEntities
        {
            add => _dbQueryCacheStore.OnInvalidatingRootEntities += value;
            remove => _dbQueryCacheStore.OnInvalidatingRootEntities -= value;
        }
        public event Action<IOnInvalidatingDependentEntities>? OnInvalidatingDependentEntities
        {
            add => _dbQueryCacheStore.OnInvalidatingDependentEntities += value;
            remove => _dbQueryCacheStore.OnInvalidatingDependentEntities -= value;
        }

        public DbQueryCacheStore(DbContext dbContext)
        {
            _dbQueryCacheStore = dbContext.GetService<IDbQueryCacheInternalStore>();
            _dbContextId = dbContext.ContextId.InstanceId;
        }

        private void Reset()
        {
            _dbQueryCacheStore.RemoveAllDbContextDependent(_dbContextId);
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

        public void ResetState() => Reset();

        public Task ResetStateAsync(CancellationToken cancellationToken = default)
        {
            Reset();
            return Task.CompletedTask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAllDbContextDependent(Guid contextId)
            => _dbQueryCacheStore.RemoveAllDbContextDependent(contextId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveRootEntities(HashSet<IEntityType> entitiesToRemove, DbContext dbContext, bool fireEvent = true) 
            => _dbQueryCacheStore.RemoveRootEntities(entitiesToRemove, dbContext, fireEvent);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveDependentEntities(HashSet<IEntityType> entitiesToRemove, DbContext dbContext, bool fireEvent = true)
            => _dbQueryCacheStore.RemoveDependentEntities(entitiesToRemove, dbContext, fireEvent);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAll()
            => _dbQueryCacheStore.RemoveAll();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddToCache(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, object? dataToCache)
            => _dbQueryCacheStore.AddToCache(dbContext, rootEntityType, key, dataToCache);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? GetCached<T>(IDbQueryCacheKey key)
            => _dbQueryCacheStore.GetCached<T>(key);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetOrAdd<T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, Func<T> create)
            => _dbQueryCacheStore.GetOrAdd(dbContext, rootEntityType, key, create);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<T> GetOrAddAsync<T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, Func<Task<T>> create)
            => _dbQueryCacheStore.GetOrAddAsync(dbContext, rootEntityType, key, create);
    }
}
