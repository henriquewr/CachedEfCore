using CachedEfCore.DependencyManager;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CachedEfCore.Cache
{
    public interface IDbQueryCacheStore 
    {
        event Action<HashSet<IEntityType>, EntityDependency>? OnInvalidatingRootEntities;
        event Action<HashSet<IEntityType>>? OnInvalidatingDependentEntities;

        void RemoveAllLazyLoadByContextId(Guid contextId, EntityDependency dependencyManager);
        void RemoveRootEntities(HashSet<IEntityType> entitiesToRemove, EntityDependency dependencyManager, bool fireEvent = true);
        void RemoveDependentEntities(HashSet<IEntityType> entitiesToRemove, bool fireEvent = true);

        void AddToCache(Guid contextId, Type rootEntityType, string key, object? dataToCache);
        T? GetCached<T>(string key);

        T? GetOrAdd<T>(Guid contextId, Type rootEntityType, string key, Func<T?> create);
        Task<T?> GetOrAddAsync<T>(Guid contextId, Type rootEntityType, string key, Func<Task<T?>> create);
    }
}