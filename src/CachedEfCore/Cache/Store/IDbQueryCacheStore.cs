using CachedEfCore.Cache.EventData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CachedEfCore.Cache.Store
{
    public interface IDbQueryCacheStore : IResettableService, IDisposable, IAsyncDisposable
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