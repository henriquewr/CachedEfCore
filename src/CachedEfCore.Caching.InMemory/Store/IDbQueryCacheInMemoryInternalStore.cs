using CachedEfCore.Cache.EventData;
using CachedEfCore.Cache.Metrics;
using CachedEfCore.Cache.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CachedEfCore.Caching.InMemory.Store
{
    public interface IDbQueryCacheInMemoryInternalStore
    {
        event Action<IOnInvalidatingRootEntities>? OnInvalidatingRootEntities;
        event Action<IOnInvalidatingDependentEntities>? OnInvalidatingDependentEntities;

        void RemoveAllByTransactionId(Guid transactionId);
        void RemoveAllDbContextDependent(DbContextId dbContextId);
        void RemoveRootEntities(HashSet<IEntityType> entitiesToRemove, DbContext dbContext, bool fireEvent = true);
        void RemoveDependentEntities(HashSet<IEntityType> entitiesToRemove, DbContext dbContext, bool fireEvent = true);
        void RemoveAll();

        void AddToCache<T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, T? dataToCache);
        T? GetCached<T>(DbContext dbContext, IDbQueryCacheKey key, IDbQueryCacheMetrics metrics);

        T GetOrAdd<T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, Func<T> create, IDbQueryCacheMetrics metrics);
        ValueTask<T> GetOrAddAsync<T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, Func<Task<T>> create, IDbQueryCacheMetrics metrics);
    }
}
