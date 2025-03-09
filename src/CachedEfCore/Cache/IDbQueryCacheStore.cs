using CachedEfCore.DependencyManager;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CachedEfCore.Cache
{
    public interface IDbQueryCacheStore 
    {
        event Action<HashSet<IEntityType>> OnInvalidatingEntities;

        void RemoveAllLazyLoadByContextId(Guid contextId, EntityDependency dependencyManager);
        void RemoveAllOfType(HashSet<IEntityType> typesToRemove, EntityDependency dependencyManager);
        void RemoveAllOfTypeNoEvent(HashSet<IEntityType> entitiesToRemove, EntityDependency dependencyManager);

        void AddToCache(Guid contextId, Type rootEntityType, string key, object? dataToCache);
        T? GetCached<T>(string key);

        T? GetOrAdd<T>(Guid contextId, Type rootEntityType, string key, Func<T?> create);
        Task<T?> GetOrAddAsync<T>(Guid contextId, Type rootEntityType, string key, Func<Task<T?>> create);
    }
}