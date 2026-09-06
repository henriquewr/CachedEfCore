using CachedEfCore.Cache.EventData;
using CachedEfCore.Cache.Metrics;
using CachedEfCore.DependencyManager;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CachedEfCore.Cache
{
    public partial class DbQueryCacheInternalStore : IDbQueryCacheInternalStore
    {
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _dbContextDependentKeys = new();
        private readonly ConcurrentDictionary<Type, CancellationTokenSource> _typeKeys = new();

        private readonly IMemoryCache _cache;
        private readonly IDbQueryCacheMetrics _metrics;
        private readonly MemoryCacheEntryOptions _cacheOptions;

        public DbQueryCacheInternalStore(IMemoryCache cache, IDbQueryCacheMetrics metrics, MemoryCacheEntryOptions cacheOptions)
        {
            _cache = cache;
            _metrics = metrics;
            _cacheOptions = cacheOptions;
        }

        public DbQueryCacheInternalStore(IMemoryCache cache, IDbQueryCacheMetrics metrics)
        {
            _cache = cache;
            _metrics = metrics;

            _cacheOptions = new() 
            { 
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
            };
        }

        public event Action<IOnInvalidatingRootEntities>? OnInvalidatingRootEntities;

        public event Action<IOnInvalidatingDependentEntities>? OnInvalidatingDependentEntities;

        public void RemoveAllDbContextDependent(Guid contextId)
        {
            if (_dbContextDependentKeys.TryRemove(contextId, out var keys))
            {
                keys.Cancel();
                keys.Dispose();
            }
        }

        public void RemoveAll()
        {
            var l_cacheKeysByContextId = _dbContextDependentKeys;
            foreach (var item in l_cacheKeysByContextId)
            {
                item.Value.Cancel();
                item.Value.Dispose();
            }
            l_cacheKeysByContextId.Clear();

            var l_cacheKeysByType = _typeKeys;
            foreach (var item in l_cacheKeysByType)
            {
                item.Value.Cancel();
                item.Value.Dispose();
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
                    keysWithType.Dispose();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? GetCached<T>(IDbQueryCacheKey key)
        {
            if (_cache.TryGetValue<T>(key, out var cached))
            {
                ReportCacheHit();
                return cached;
            }
            
            ReportCacheMiss();

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddToCache(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, object? dataToCache)
        {
            InternalAddToCache(dbContext, rootEntityType, key, dataToCache);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InternalAddToCache(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey cacheKey, object? dataToCache)
        {
            using var cacheEntry = _cache.CreateEntry(cacheKey).SetOptions(_cacheOptions);
            cacheEntry.SetSize(0);
            cacheEntry.Value = dataToCache;

            if (dataToCache is not null && cacheKey.DependentDbContext.HasValue)
            {
                // if dataToCache is null the object is not really dependent to the DbContext instance
                CancellationTokenSource dbContextDependentCts;

                var dbContextId = dbContext.ContextId.InstanceId;

                if (!_dbContextDependentKeys.TryGetValue(dbContextId, out dbContextDependentCts!))
                {
                    dbContextDependentCts = new CancellationTokenSource();
                    _dbContextDependentKeys.TryAdd(dbContextId, dbContextDependentCts);
                }

                cacheEntry.AddExpirationToken(new CancellationChangeToken(dbContextDependentCts.Token));
            }

            CancellationTokenSource ctsByType;

            if (!_typeKeys.TryGetValue(rootEntityType, out ctsByType!))
            {
                ctsByType = new CancellationTokenSource();
                _typeKeys.TryAdd(rootEntityType, ctsByType);
            }

            cacheEntry.AddExpirationToken(new CancellationChangeToken(ctsByType.Token));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetOrAdd<T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, Func<T> create)
        {
            if (_cache.TryGetValue<T>(key, out var cachedValue))
            {
                ReportCacheHit();
                return cachedValue!;
            }

            var createdValue = create();
            ReportCacheMiss();
            InternalAddToCache(dbContext, rootEntityType, key, createdValue);

            return createdValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<T> GetOrAddAsync<T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, Func<Task<T>> create)
        {
            if (_cache.TryGetValue<T>(key, out var cachedValue))
            {
                ReportCacheHit();
                return cachedValue!;
            }

            var createdValue = await create().ConfigureAwait(false);
            ReportCacheMiss();
            InternalAddToCache(dbContext, rootEntityType, key, createdValue);

            return createdValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReportCacheHit()
        {
            DbQueryCacheMetrics.GlobalInstance.ReportCacheHit();
            _metrics.ReportCacheHit();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReportCacheMiss()
        {
            DbQueryCacheMetrics.GlobalInstance.ReportCacheMiss();
            _metrics.ReportCacheMiss();
        }
    }
}