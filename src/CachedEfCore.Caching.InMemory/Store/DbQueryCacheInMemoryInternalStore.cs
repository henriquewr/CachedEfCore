using CachedEfCore.Cache.EventData;
using CachedEfCore.Cache.Metrics;
using CachedEfCore.Cache.Store;
using CachedEfCore.DependencyManager;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CachedEfCore.Caching.InMemory.Store
{
    public class DbQueryCacheInMemoryInternalStore : IDbQueryCacheInMemoryInternalStore
    {
        internal readonly Dictionary<DbContextId, CancellationTokenSource> _dbContextDependentKeys = new(0);
        internal readonly Dictionary<Type, CancellationTokenSource> _typeKeys = new();
        internal readonly Dictionary<Guid, CancellationTokenSource> _transactions = new(0);

        private readonly IMemoryCache _cache;

        public DbQueryCacheInMemoryInternalStore(IMemoryCache cache)
        {
            _cache = cache;
        }

        private sealed record class InMemoryKey(IDbQueryCacheKey Key, Guid? TransactionId);

        public event Action<IOnInvalidatingRootEntities>? OnInvalidatingRootEntities;

        public event Action<IOnInvalidatingDependentEntities>? OnInvalidatingDependentEntities;

        public void RemoveAllByTransactionId(Guid transactionId)
        {
            lock (_transactions)
            {
                if (_transactions.Remove(transactionId, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                }
            }
        }

        public void RemoveAllDbContextDependent(DbContextId dbContextId)
        {
            lock (_dbContextDependentKeys)
            {
                if (_dbContextDependentKeys.Remove(dbContextId, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                }
            }
        }

        public void RemoveAll()
        {
            lock (_typeKeys)
            {
                foreach (var item in _typeKeys)
                {
                    item.Value.Cancel();
                    item.Value.Dispose();
                }
                _typeKeys.Clear();
            }

            lock (_dbContextDependentKeys)
            {
                foreach (var item in _dbContextDependentKeys)
                {
                    if (_dbContextDependentKeys.Remove(item.Key))
                    {
                        item.Value.Cancel();
                        item.Value.Dispose();
                    }
                }
                _dbContextDependentKeys.Clear();
            }

            lock (_transactions)
            {
                var transactions = _transactions.ToArray();
                foreach (var item in transactions)
                {
                    item.Value.Cancel();
                    item.Value.Dispose();
                }
                _transactions.Clear();
            }
        }

        public void RemoveRootEntities(HashSet<IEntityType> entitiesToRemove, DbContext dbContext, bool fireEvent = true)
        {
            if (fireEvent && OnInvalidatingRootEntities is not null)
            {
                OnInvalidatingRootEntities.Invoke(new OnInvalidatingRootEntities
                {
                    Entities = entitiesToRemove,
                    DbContext = dbContext
                });
            }

            var typesToRemove = new HashSet<IEntityType>();

            var dependencyManager = dbContext.GetService<EntityDependency>();

            foreach (var typeToRemove in entitiesToRemove)
            {
                typesToRemove.UnionWith(dependencyManager.GetUpperRelatedEntities(typeToRemove));
            }

            RemoveDependentEntities(typesToRemove, dbContext, fireEvent);
        }

        public void RemoveDependentEntities(HashSet<IEntityType> entitiesToRemove, DbContext dbContext, bool fireEvent = true)
        {
            if (fireEvent && OnInvalidatingDependentEntities is not null)
            {
                OnInvalidatingDependentEntities.Invoke(new OnInvalidatingDependentEntities
                {
                    Entities = entitiesToRemove,
                    DbContext = dbContext
                });
            }

            var l_typeKeys = _typeKeys;

            foreach (var item in entitiesToRemove)
            {
                lock (_typeKeys)
                {
                    if (l_typeKeys.Remove(item.ClrType, out var cts))
                    {
                        cts.Cancel();
                        cts.Dispose();
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? GetCached<T>(DbContext dbContext, IDbQueryCacheKey key, IDbQueryCacheMetrics metrics)
        {
            var transactionId = dbContext.Database.CurrentTransaction?.TransactionId;
            var inMemoryKey = new InMemoryKey(key, transactionId);

            if (_cache.TryGetValue<T>(inMemoryKey, out var cachedValue))
            {
                metrics.ReportCacheHit();
                return cachedValue!;
            }

            metrics.ReportCacheMiss();

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddToCache<T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, T? dataToCache)
        {
            var transactionId = dbContext.Database.CurrentTransaction?.TransactionId;
            var inMemoryKey = new InMemoryKey(key, transactionId);

            InternalAddToCache(dbContext, rootEntityType, inMemoryKey, dataToCache);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InternalAddToCache<T>(DbContext dbContext, Type rootEntityType, InMemoryKey cacheKey, T? dataToCache)
        {
            var dbQueryCacheInMemoryCache = dbContext.GetService<DbQueryCacheStoreInMemoryCacheEntryOptions>();

            using var cacheEntry = _cache.CreateEntry(cacheKey).SetOptions(dbQueryCacheInMemoryCache.EntryOptions);
            cacheEntry.Value = dataToCache;

            lock (_typeKeys)
            {
                ref var cts = ref CollectionsMarshal.GetValueRefOrAddDefault(_typeKeys, rootEntityType, out var exists);
                if (!exists)
                {
                    cts = new CancellationTokenSource();
                }
                cacheEntry.AddExpirationToken(new CancellationChangeToken(cts!.Token));
            }

            if (dataToCache is not null && cacheKey.Key.DependentDbContext.HasValue)
            {
                // if dataToCache is null the object is not really dependent to the DbContext instance

                var dbContextId = dbContext.ContextId;
                lock (_dbContextDependentKeys)
                {
                    ref var cts = ref CollectionsMarshal.GetValueRefOrAddDefault(_dbContextDependentKeys, dbContextId, out var exists);
                    if (!exists)
                    {
                        cts = new CancellationTokenSource();
                    }
                    cacheEntry.AddExpirationToken(new CancellationChangeToken(cts!.Token));
                }
            }

            if (cacheKey.TransactionId.HasValue)
            {
                var transactionId = cacheKey.TransactionId.Value;

                lock (_transactions)
                {
                    ref var cts = ref CollectionsMarshal.GetValueRefOrAddDefault(_transactions, transactionId, out var exists);
                    if (!exists)
                    {
                        cts = new CancellationTokenSource();
                    }
                    cacheEntry.AddExpirationToken(new CancellationChangeToken(cts!.Token));
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetOrAdd<T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, Func<T> create, IDbQueryCacheMetrics metrics)
        {
            var transactionId = dbContext.Database.CurrentTransaction?.TransactionId;
            var inMemoryKey = new InMemoryKey(key, transactionId);

            if (_cache.TryGetValue<T>(inMemoryKey, out var cachedValue))
            {
                metrics.ReportCacheHit();
                return cachedValue!;
            }

            var createdValue = create();
            metrics.ReportCacheMiss();
            InternalAddToCache(dbContext, rootEntityType, inMemoryKey, createdValue);

            return createdValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<T> GetOrAddAsync<T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, Func<Task<T>> create, IDbQueryCacheMetrics metrics)
        {
            var transactionId = dbContext.Database.CurrentTransaction?.TransactionId;
            var inMemoryKey = new InMemoryKey(key, transactionId);

            if (_cache.TryGetValue<T>(inMemoryKey, out var cachedValue))
            {
                metrics.ReportCacheHit();
                return cachedValue!;
            }

            var createdValue = await create().ConfigureAwait(false);
            metrics.ReportCacheMiss();
            InternalAddToCache(dbContext, rootEntityType, inMemoryKey, createdValue);

            return createdValue;
        }
    }
}