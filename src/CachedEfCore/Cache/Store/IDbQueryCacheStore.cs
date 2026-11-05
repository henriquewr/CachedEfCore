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
        void RemoveAllByTransactionId(Guid transactionId);
        ValueTask RemoveAllByTransactionIdAsync(Guid transactionId);

        void RemoveAllDbContextDependent(DbContextId dbContextId);
        ValueTask RemoveAllDbContextDependentAsync(DbContextId dbContextId);

        void RemoveRootEntities(HashSet<IEntityType> entitiesToRemove);
        ValueTask RemoveRootEntitiesAsync(HashSet<IEntityType> entitiesToRemove);

        void RemoveDependentEntities(HashSet<IEntityType> entitiesToRemove);
        ValueTask RemoveDependentEntitiesAsync(HashSet<IEntityType> entitiesToRemove);

        void RemoveAll();
        ValueTask RemoveAllAsync();

        void AddToCache<T>(Type rootEntityType, IDbQueryCacheKey key, T? dataToCache);
        T? GetCached<T>(IDbQueryCacheKey key);

        T GetOrAdd<T>(Type rootEntityType, IDbQueryCacheKey key, Func<T> create);
        ValueTask<T> GetOrAddAsync<T>(Type rootEntityType, IDbQueryCacheKey key, Func<Task<T>> create);
    }
}