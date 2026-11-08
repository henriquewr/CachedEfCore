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
    public class DbQueryCacheInMemoryInternalStore
    {
        internal readonly Dictionary<DbContextId, CancellationTokenSource> _dbContextDependentKeys = new(0);
        internal readonly Dictionary<Type, CancellationTokenSource> _typeKeys = new();
        private readonly object _transactions = new();

        private readonly IMemoryCache _cache;

        public DbQueryCacheInMemoryInternalStore(IMemoryCache cache)
        {
            _cache = cache;
        }

        private record class TransactionKey(Guid TransactionId);

        private readonly static MemoryCacheEntryOptions _transactionCacheEntryOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5),
            Priority = CacheItemPriority.High,
            Size = 0,
        };

        private sealed record class InMemoryKeyWithTransaction(IDbQueryCacheKey Key, Guid TransactionId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static object GetKey(IDbQueryCacheKey existingKey, Guid? transactionId)
        {
            return transactionId.HasValue
                ? new InMemoryKeyWithTransaction(existingKey, transactionId.Value)
                : existingKey;
        }

        public void RemoveAllByTransactionId(Guid transactionId)
        {
            var key = new TransactionKey(transactionId);

            lock (_transactions)
            {
                if (_cache.TryGetValue<CancellationTokenSource>(key, out var cts) && cts is not null)
                {
                    cts.Cancel();
                    cts.Dispose();

                    _cache.Remove(key);
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
                    item.Value.Cancel();
                    item.Value.Dispose();
                }
                _dbContextDependentKeys.Clear();
            }
        }

        public void RemoveRootEntities(HashSet<IEntityType> entitiesToRemove, DbContext dbContext)
        {
            var typesToRemove = new HashSet<IEntityType>(32);

            var dependencyManager = dbContext.GetService<EntityDependency>();

            foreach (var typeToRemove in entitiesToRemove)
            {
                typesToRemove.UnionWith(dependencyManager.GetUpperRelatedEntities(typeToRemove));
            }

            RemoveDependentEntities(typesToRemove);
        }

        public void RemoveDependentEntities(HashSet<IEntityType> entitiesToRemove)
        {
            var l_typeKeys = _typeKeys;

            lock (_typeKeys)
            {
                foreach (var item in entitiesToRemove)
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
            var inMemoryKey = GetKey(key, transactionId);

            if (_cache.TryGetValue<T>(inMemoryKey, out var cachedValue))
            {
                metrics.ReportCacheHit();
                return cachedValue!;
            }

            metrics.ReportCacheMiss();

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddToCache(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, Guid? transactionId, object? dataToCache)
        {
            var inMemoryKey = GetKey(key, transactionId);

            InternalAddToCache(dbContext, rootEntityType, inMemoryKey, key.DependentDbContext, transactionId, dataToCache);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InternalAddToCache(DbContext dbContext,
            Type rootEntityType, 
            object cacheKey,
            DbContextId? dependentDbContext, 
            Guid? transactionId, 
            object? dataToCache)
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

            if (dataToCache is not null && dependentDbContext.HasValue)
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

            if (transactionId.HasValue)
            {
                var transactionIdVal = transactionId.Value;
                var transactionKey = new TransactionKey(transactionIdVal);

                lock (_transactions)
                {
                    var cts = _cache.GetOrCreate(transactionKey, static entry => new CancellationTokenSource(), _transactionCacheEntryOptions)!;

                    cacheEntry.AddExpirationToken(new CancellationChangeToken(cts.Token));
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetOrAdd<T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, Guid? transactionId, Func<T> create, IDbQueryCacheMetrics metrics)
        {
            var inMemoryKey = GetKey(key, transactionId);

            if (_cache.TryGetValue<T>(inMemoryKey, out var cachedValue))
            {
                metrics.ReportCacheHit();
                return cachedValue!;
            }

            var createdValue = create();
            metrics.ReportCacheMiss();
            InternalAddToCache(dbContext, rootEntityType, inMemoryKey, key.DependentDbContext, transactionId, createdValue);

            return createdValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<T> GetOrAddAsync<T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, Guid? transactionId, Func<Task<T>> create, IDbQueryCacheMetrics metrics)
        {
            var inMemoryKey = GetKey(key, transactionId);

            if (_cache.TryGetValue<T>(inMemoryKey, out var cachedValue))
            {
                metrics.ReportCacheHit();
                return cachedValue!;
            }

            var createdValue = await create().ConfigureAwait(false);
            metrics.ReportCacheMiss();
            InternalAddToCache(dbContext, rootEntityType, inMemoryKey, key.DependentDbContext, transactionId, createdValue);

            return createdValue;
        }
    }
}