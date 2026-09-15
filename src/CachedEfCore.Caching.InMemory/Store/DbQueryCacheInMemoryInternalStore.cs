using CachedEfCore.Cache.EventData;
using CachedEfCore.Cache.Metrics;
using CachedEfCore.Cache.Store;
using CachedEfCore.DependencyManager;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace CachedEfCore.Caching.InMemory.Store
{
    public class DbQueryCacheInMemoryInternalStore : IDbQueryCacheInMemoryInternalStore, IStatefulDbQueryCacheInMemoryInternalStore
    {
        internal readonly ConcurrentDictionary<Guid, CancellationTokenSource> _dbContextDependentKeys = new();
        internal readonly ConcurrentDictionary<Type, CancellationTokenSource> _typeKeys = new();

        private readonly IMemoryCache _cache;
        private readonly IDbQueryCacheMetrics _metrics;
        private readonly MemoryCacheEntryOptions _cacheOptions;

        public DbQueryCacheInMemoryInternalStore(IMemoryCache cache, IDbQueryCacheMetrics metrics, MemoryCacheEntryOptions cacheOptions)
        {
            _cache = cache;
            _metrics = metrics;
            _cacheOptions = cacheOptions;
        }

        public event Action<IOnInvalidatingRootEntities>? OnInvalidatingRootEntities;

        public event Action<IOnInvalidatingDependentEntities>? OnInvalidatingDependentEntities;

        public void RemoveAllDbContextDependent(Guid contextId)
        {
            if (_dbContextDependentKeys.TryRemove(contextId, out var keys))
            {
                keys.Cancel();
            }
        }

        public void RemoveAll()
        {
            var l_cacheKeysByContextId = _dbContextDependentKeys;
            foreach (var item in l_cacheKeysByContextId)
            {
                item.Value.Cancel();
            }
            l_cacheKeysByContextId.Clear();

            var l_cacheKeysByType = _typeKeys;
            foreach (var item in l_cacheKeysByType)
            {
                item.Value.Cancel();
            }
            l_cacheKeysByType.Clear();
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

            foreach (var item in entitiesToRemove)
            {
                if (_typeKeys.TryRemove(item.ClrType, out var keysWithType))
                {
                    keysWithType.Cancel();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? GetCached<T>(IDbQueryCacheKey key)
        {
            if (_cache.TryGetValue<T>(key, out var cached))
            {
                _metrics.ReportCacheHit();
                return cached;
            }

            _metrics.ReportCacheMiss();

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddToCache(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, object? dataToCache)
        {
            InternalAddToCache(dbContext, rootEntityType, key, dataToCache);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InternalAddToCache(
            DbContext dbContext,
            Type rootEntityType,
            IDbQueryCacheKey cacheKey,
            object? dataToCache,
            CancellationTokenSource? ctsByType = null)
        {
            using var cacheEntry = _cache.CreateEntry(cacheKey).SetOptions(_cacheOptions);
            cacheEntry.SetSize(0);
            cacheEntry.Value = dataToCache;

            if (dataToCache is not null && cacheKey.DependentDbContext.HasValue)
            {
                // if dataToCache is null the object is not really dependent to the DbContext instance
                var dbContextId = dbContext.ContextId.InstanceId;
                var dbContextDependentCts = GetOrAddTokenSource(_dbContextDependentKeys, dbContextId);

                cacheEntry.AddExpirationToken(new CancellationChangeToken(dbContextDependentCts.Token));
            }

            ctsByType ??= GetOrAddTokenSource(_typeKeys, rootEntityType);

            cacheEntry.AddExpirationToken(new CancellationChangeToken(ctsByType.Token));
        }

        private static CancellationTokenSource GetOrAddTokenSource<TKey>(ConcurrentDictionary<TKey, CancellationTokenSource> tokenSources, TKey key)
            where TKey : notnull
        {
            if (tokenSources.TryGetValue(key, out var tokenSource))
            {
                return tokenSource;
            }

            var newTokenSource = new CancellationTokenSource();
            tokenSource = tokenSources.GetOrAdd(key, newTokenSource);

            if (!ReferenceEquals(tokenSource, newTokenSource))
            {
                newTokenSource.Dispose();
            }

            return tokenSource;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetOrAdd<T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, Func<T> create)
        {
            if (_cache.TryGetValue<T>(key, out var cachedValue))
            {
                _metrics.ReportCacheHit();
                return cachedValue!;
            }

            var ctsByType = GetOrAddTokenSource(_typeKeys, rootEntityType);
            var createdValue = create();
            _metrics.ReportCacheMiss();
            InternalAddToCache(dbContext, rootEntityType, key, createdValue, ctsByType);

            return createdValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<T> GetOrAddAsync<T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, Func<Task<T>> create)
        {
            if (_cache.TryGetValue<T>(key, out var cachedValue))
            {
                _metrics.ReportCacheHit();
                return cachedValue!;
            }

            var ctsByType = GetOrAddTokenSource(_typeKeys, rootEntityType);
            var createdValue = await create().ConfigureAwait(false);
            _metrics.ReportCacheMiss();
            InternalAddToCache(dbContext, rootEntityType, key, createdValue, ctsByType);

            return createdValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetOrAdd<TState, T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, TState state, Func<TState, T> create)
        {
            if (_cache.TryGetValue<T>(key, out var cachedValue))
            {
                _metrics.ReportCacheHit();
                return cachedValue!;
            }

            var ctsByType = GetOrAddTokenSource(_typeKeys, rootEntityType);
            var createdValue = create(state);
            _metrics.ReportCacheMiss();
            InternalAddToCache(dbContext, rootEntityType, key, createdValue, ctsByType);

            return createdValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<T> GetOrAddAsync<TState, T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, TState state, Func<TState, Task<T>> create)
        {
            if (_cache.TryGetValue<T>(key, out var cachedValue))
            {
                _metrics.ReportCacheHit();
                return cachedValue!;
            }

            var ctsByType = GetOrAddTokenSource(_typeKeys, rootEntityType);
            var createdValue = await create(state).ConfigureAwait(false);
            _metrics.ReportCacheMiss();
            InternalAddToCache(dbContext, rootEntityType, key, createdValue, ctsByType);

            return createdValue;
        }
    }
}
