using CachedEfCore.Cache.EventData;
using CachedEfCore.Cache.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CachedEfCore.Caching.InMemory.Store
{
    public interface IDbQueryCacheInMemoryInternalStore
    {
        event Action<IOnInvalidatingRootEntities>? OnInvalidatingRootEntities;
        event Action<IOnInvalidatingDependentEntities>? OnInvalidatingDependentEntities;

        void RemoveAllDbContextDependent(Guid contextId);
        void RemoveRootEntities(HashSet<IEntityType> entitiesToRemove, DbContext dbContext, bool fireEvent = true);
        void RemoveDependentEntities(HashSet<IEntityType> entitiesToRemove, DbContext dbContext, bool fireEvent = true);
        void RemoveAll();

        void AddToCache(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, object? dataToCache);
        T? GetCached<T>(IDbQueryCacheKey key);

        T GetOrAdd<T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, Func<T> create);
        ValueTask<T> GetOrAddAsync<T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, Func<Task<T>> create);
    }
}
