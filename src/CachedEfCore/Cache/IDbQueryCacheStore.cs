using CachedEfCore.Cache.EventData;
using CachedEfCore.Context;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CachedEfCore.Cache
{
    public interface IDbQueryCacheStore
    {
        event Action<IOnInvalidatingRootEntities>? OnInvalidatingRootEntities;
        event Action<IOnInvalidatingDependentEntities>? OnInvalidatingDependentEntities;

        void RemoveAllDbContextDependent(Guid contextId);
        void RemoveRootEntities(HashSet<IEntityType> entitiesToRemove, ICachedDbContext cachedDbContext, bool fireEvent = true);
        void RemoveDependentEntities(HashSet<IEntityType> entitiesToRemove, ICachedDbContext cachedDbContext, bool fireEvent = true);
        void RemoveAll();

        void AddToCache(ICachedDbContext cachedDbContext, Type rootEntityType, IDbQueryCacheKey key, object? dataToCache);
        T? GetCached<T>(IDbQueryCacheKey key);

        T GetOrAdd<T>(ICachedDbContext cachedDbContext, Type rootEntityType, IDbQueryCacheKey key, Func<T> create);
        ValueTask<T> GetOrAddAsync<T>(ICachedDbContext cachedDbContext, Type rootEntityType, IDbQueryCacheKey key, Func<Task<T>> create);
    }
}