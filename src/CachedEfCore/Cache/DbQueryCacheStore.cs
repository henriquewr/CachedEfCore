using CachedEfCore.Cache.EventData;
using CachedEfCore.Context;
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
    public partial class DbQueryCacheStore : IDbQueryCacheStore
    {
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _dbContextDependentKeys = new();
        private readonly ConcurrentDictionary<Type, CancellationTokenSource> _typeKeys = new();

        private readonly IMemoryCache _cache;
        private readonly MemoryCacheEntryOptions _cacheOptions;

        public DbQueryCacheStore(IMemoryCache cache, MemoryCacheEntryOptions cacheOptions)
        {
            _cache = cache;
            _cacheOptions = cacheOptions;
        }

        public DbQueryCacheStore(IMemoryCache cache)
        {
            _cache = cache;

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

        public void RemoveRootEntities(HashSet<IEntityType> entitiesToRemove, ICachedDbContext cachedDbContext, bool fireEvent = true)
        {
            if (fireEvent && OnInvalidatingRootEntities is not null)
            {
                OnInvalidatingRootEntities.Invoke(new OnInvalidatingRootEntities
                {
                    Entities = entitiesToRemove,
                    CachedDbContext = cachedDbContext
                });
            }

            var typesToRemove = new HashSet<IEntityType>();

            var dependencyManager = cachedDbContext.DependencyManager;

            foreach (var typeToRemove in entitiesToRemove)
            {
                typesToRemove.UnionWith(dependencyManager.GetUpperRelatedEntities(typeToRemove));
            }

            RemoveDependentEntities(typesToRemove, cachedDbContext, fireEvent);
        }

        public void RemoveDependentEntities(HashSet<IEntityType> entitiesToRemove, ICachedDbContext cachedDbContext, bool fireEvent = true)
        {
            if (fireEvent && OnInvalidatingDependentEntities is not null)
            {
                OnInvalidatingDependentEntities.Invoke(new OnInvalidatingDependentEntities
                {
                    Entities = entitiesToRemove,
                    CachedDbContext = cachedDbContext
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
            var cached = _cache.Get<T>(key);

            return cached;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddToCache(ICachedDbContext cachedDbContext, Type rootEntityType, IDbQueryCacheKey key, object? dataToCache)
        {
            InternalAddToCache(cachedDbContext, rootEntityType, key, dataToCache);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InternalAddToCache(ICachedDbContext cachedDbContext, Type rootEntityType, IDbQueryCacheKey cacheKey, object? dataToCache)
        {
            using var cacheEntry = _cache.CreateEntry(cacheKey).SetOptions(_cacheOptions);

            cacheEntry.Value = dataToCache;

            if (dataToCache is not null && cacheKey.DependentDbContext.HasValue)
            {
                // if dataToCache is null the object is not really dependent to the DbContext instance
                CancellationTokenSource dbContextDependentCts;

                if (!_dbContextDependentKeys.TryGetValue(cachedDbContext.Id, out dbContextDependentCts!))
                {
                    dbContextDependentCts = new CancellationTokenSource();
                    _dbContextDependentKeys.TryAdd(cachedDbContext.Id, dbContextDependentCts);
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
        public T GetOrAdd<T>(ICachedDbContext cachedDbContext, Type rootEntityType, IDbQueryCacheKey key, Func<T> create)
        {
            if (_cache.TryGetValue<T>(key, out var cachedValue))
            {
                return cachedValue!;
            }

            var createdValue = create();

            InternalAddToCache(cachedDbContext, rootEntityType, key, createdValue);

            return createdValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<T> GetOrAddAsync<T>(ICachedDbContext cachedDbContext, Type rootEntityType, IDbQueryCacheKey key, Func<Task<T>> create)
        {
            if (_cache.TryGetValue<T>(key, out var cachedValue))
            {
                return cachedValue!;
            }

            var createdValue = await create().ConfigureAwait(false);

            InternalAddToCache(cachedDbContext, rootEntityType, key, createdValue);

            return createdValue;
        }
    }
}