using CachedEfCore.DependencyManager;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CachedEfCore.Cache
{
    public class DbQueryCacheStore : IDbQueryCacheStore
    {
        private readonly ConcurrentDictionary<Guid, ConcurrentBag<object>> _cacheKeysByContextId = new();
        private readonly ConcurrentDictionary<Type, ConcurrentBag<object>> _cacheKeysByType = new();

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

        public event Action<HashSet<IEntityType>, EntityDependency>? OnInvalidatingRootEntities;

        public event Action<HashSet<IEntityType>>? OnInvalidatingDependentEntities;

        public void RemoveAllLazyLoadByContextId(Guid contextId, EntityDependency dependencyManager)
        {
            if (_cacheKeysByContextId.TryRemove(contextId, out var keys))
            {
                RemoveLazyLoadKeys(keys, dependencyManager);
            }
        }

        public void RemoveRootEntities(HashSet<IEntityType> entitiesToRemove, EntityDependency dependencyManager, bool fireEvent = true)
        {
            if (fireEvent)
            {
                OnInvalidatingRootEntities?.Invoke(entitiesToRemove, dependencyManager);
            }

            var typesToRemove = new HashSet<IEntityType>();

            foreach (var typeToRemove in entitiesToRemove)
            {
                typesToRemove.UnionWith(dependencyManager.GetAboveRelatedEntities(typeToRemove));
            }

            RemoveDependentEntities(typesToRemove, fireEvent);
        }

        public void RemoveDependentEntities(HashSet<IEntityType> entitiesToRemove, bool fireEvent = true)
        {
            if (fireEvent)
            {
                OnInvalidatingDependentEntities?.Invoke(entitiesToRemove);
            }

            foreach (var item in entitiesToRemove)
            {
                if (_cacheKeysByType.TryRemove(item.ClrType, out var keysWithType))
                {
                    RemoveKeys(keysWithType);
                }
            }
        }

        private void RemoveKeys(IEnumerable<object> keys)
        {
            foreach (var key in keys)
            {
                _cache.Remove(key);
            }
        }

        private void RemoveLazyLoadKeys(IEnumerable<object> keysToRemove, EntityDependency dependencyManager)
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
        public T? GetCached<T>(object key)
        {
            var cached = _cache.Get<T>(key);

            return cached;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddToCache(Guid contextId, Type rootEntityType, object key, object? dataToCache)
        {
            if (_cacheKeysByContextId.TryGetValue(contextId, out var contextValues))
            {
                contextValues.Add(key);
            }
            else
            {
                _cacheKeysByContextId[contextId] = new() { key };
            }

            if (_cacheKeysByType.TryGetValue(rootEntityType, out var typeValues))
            {
                typeValues.Add(key);
            }
            else
            {
                _cacheKeysByType[rootEntityType] = new() { key };
            }

            _cache.Set(key, dataToCache, _cacheOptions);
        }

        private void AddingToCache(Guid contextId, Type rootEntityType, object cacheKey)
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
        public T? GetOrAdd<T>(Guid contextId, Type rootEntityType, object key, Func<T?> create)
        {
            var value = _cache.GetOrCreate(key, (cacheEntry) => {
                AddingToCache(contextId, rootEntityType, key);
                return create();
            }, _cacheOptions);

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<T?> GetOrAddAsync<T>(Guid contextId, Type rootEntityType, object key, Func<Task<T?>> create)
        {
            var value = _cache.GetOrCreateAsync(key, (cacheEntry) => {
                AddingToCache(contextId, rootEntityType, key);
                return create();
            }, _cacheOptions);

            return value;
        }
    }
}