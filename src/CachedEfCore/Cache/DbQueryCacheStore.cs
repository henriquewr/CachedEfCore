using CachedEfCore.DependencyManager;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CachedEfCore.Cache
{
    public class DbQueryCacheStore : IDbQueryCacheStore
    {
        private const string CacheKeyPrefix = "CachedEfCoreCache:";

        private readonly ConcurrentDictionary<Guid, ConcurrentBag<string>> _cacheKeysByContextId = new();
        private readonly ConcurrentDictionary<Type, ConcurrentBag<string>> _cacheKeysByType = new();

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

        public event Action<HashSet<IEntityType>>? OnInvalidatingEntities;

        public void RemoveAllLazyLoadByContextId(Guid contextId, EntityDependency dependencyManager)
        {
            if (_cacheKeysByContextId.TryRemove(contextId, out var keys))
            {
                RemoveLazyLoadKeys(keys, dependencyManager);
            }
        }

        public void RemoveAllOfType(HashSet<IEntityType> entitiesToRemove, EntityDependency dependencyManager)
        {
            OnInvalidatingEntities?.Invoke(entitiesToRemove);

            RemoveAllOfTypeNoEvent(entitiesToRemove, dependencyManager);
        }

        public void RemoveAllOfTypeNoEvent(HashSet<IEntityType> entitiesToRemove, EntityDependency dependencyManager)
        {
            var typesToRemove = new HashSet<Type>();

            foreach (var typeToRemove in entitiesToRemove)
            {
                typesToRemove.UnionWith(dependencyManager.GetAboveRelatedEntities(typeToRemove).Select(x => x.ClrType));
            }

            foreach (var item in typesToRemove)
            {
                if (_cacheKeysByType.TryRemove(item, out var keysWithType))
                {
                    RemoveKeys(keysWithType);
                }
            }
        }

        private void RemoveKeys(IEnumerable<string> keys)
        {
            foreach (var key in keys)
            {
                _cache.Remove(key);
            }
        }

        private void RemoveLazyLoadKeys(IEnumerable<string> keysToRemove, EntityDependency dependencyManager)
        {
            foreach (var key in keysToRemove)
            {
                var cachedItem = _cache.Get(key);

                if (cachedItem is null)
                {
                    continue;
                }

                var type = cachedItem.GetType();
                var hasLazyLoad = dependencyManager.HasLazyLoad(type);

                if (hasLazyLoad)
                {
                    _cache.Remove(key);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? GetCached<T>(string key)
        {
            var cacheKey = GetCacheKey(key);

            var cached = _cache.Get<T>(cacheKey);

            return cached;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddToCache(Guid contextId, Type rootEntityType, string key, object? dataToCache)
        {
            var cacheKey = GetCacheKey(key);

            if (_cacheKeysByContextId.TryGetValue(contextId, out var contextValues))
            {
                contextValues.Add(cacheKey);
            }
            else
            {
                _cacheKeysByContextId[contextId] = new() { cacheKey };
            }

            if (_cacheKeysByType.TryGetValue(rootEntityType, out var typeValues))
            {
                typeValues.Add(cacheKey);
            }
            else
            {
                _cacheKeysByType[rootEntityType] = new() { cacheKey };
            }

            _cache.Set(cacheKey, dataToCache, _cacheOptions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetCacheKey(string query)
        {
            var cacheKey = $"{CacheKeyPrefix}.{query}";
            return cacheKey;
        }

        private void AddingToCache(Guid contextId, Type rootEntityType, string cacheKey)
        {
            if (_cacheKeysByContextId.TryGetValue(contextId, out var contextValues))
            {
                contextValues.Add(cacheKey);
            }
            else
            {
                _cacheKeysByContextId[contextId] = new() { cacheKey };
            }

            if (_cacheKeysByType.TryGetValue(rootEntityType, out var typeValues))
            {
                typeValues.Add(cacheKey);
            }
            else
            {
                _cacheKeysByType[rootEntityType] = new() { cacheKey };
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? GetOrAdd<T>(Guid contextId, Type rootEntityType, string key, Func<T?> create)
        {
            var cacheKey = GetCacheKey(key);

            var value = _cache.GetOrCreate(cacheKey, (cacheEntry) => {
                AddingToCache(contextId, rootEntityType, cacheKey);
                return create();
            }, _cacheOptions);

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<T?> GetOrAddAsync<T>(Guid contextId, Type rootEntityType, string key, Func<Task<T?>> create)
        {
            var cacheKey = GetCacheKey(key);

            var value = _cache.GetOrCreateAsync(cacheKey, (cacheEntry) => {
                AddingToCache(contextId, rootEntityType, cacheKey);
                return create();
            }, _cacheOptions);

            return value;
        }
    }
}