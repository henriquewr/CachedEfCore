using CachedEfCore.Cache.Store;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CachedEfCore.Caching.Store
{
    public class InMemoryPendingEntitiesImplementation
    {
        private readonly IMemoryCache _memoryCache;
        private readonly object _pendingEntitiesLock;

        public InMemoryPendingEntitiesImplementation(IMemoryCache memoryCache, object pendingEntitiesLock)
        {
            _memoryCache = memoryCache;
            _pendingEntitiesLock = pendingEntitiesLock;
        }

        private sealed record class PendingEntityKey(Type EntityType)
        {
            public static PendingEntityKey Create(IEntityType entityType)
            {
                return new PendingEntityKey(entityType.ClrType);
            }
            public static PendingEntityKey Create(Type type)
            {
                return new PendingEntityKey(type);
            }
        }

        private sealed class InMemoryPendingEntities : IPendingEntities
        {
            private bool _removed;

            private readonly object _cacheLock;
            private readonly IMemoryCache _cache;

            public InMemoryPendingEntities(IMemoryCache cache, object cacheLock)
            {
                _cache = cache;
                _cacheLock = cacheLock;
            }

            public required IReadOnlyList<IEntityType> EntityTypes { get; init; }

            private void Remove()
            {
                if (_removed)
                {
                    return;
                }

                var cache = _cache;
                lock (_cacheLock)
                {
                    foreach (var item in EntityTypes)
                    {
                        var key = PendingEntityKey.Create(item);

                        if (cache.TryGetValue<PendingEntityCounter>(key, out var entry) && entry is not null)
                        {
                            if (--entry.References <= 0)
                            {
                                cache.Remove(key);
                            }
                        }
                    }
                }

                _removed = true;
            }

            public void Dispose()
            {
                GC.SuppressFinalize(this);
                Remove();
            }

            public ValueTask DisposeAsync()
            {
                GC.SuppressFinalize(this);
                Remove();
                return ValueTask.CompletedTask;
            }

            ~InMemoryPendingEntities()
            {
                Remove();
            }
        }

        [DebuggerDisplay("References: {References}")]
        private sealed class PendingEntityCounter
        {
            public required int References { get; set; }
        }
        private static readonly MemoryCacheEntryOptions _pendingEntityCacheEntryOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromSeconds(30),
            Priority = CacheItemPriority.High,
            Size = 0,
        };

        public IPendingEntities AddPendingEntities(IEnumerable<IEntityType> entitiesToAdd)
        {
            var memoryCache = _memoryCache;

            var entityTypesList = entitiesToAdd.TryGetNonEnumeratedCount(out var count)
                ? new List<IEntityType>(count)
                : new List<IEntityType>();

            lock (_pendingEntitiesLock)
            {
                foreach (var item in entitiesToAdd)
                {
                    var key = PendingEntityKey.Create(item);

                    if (memoryCache.TryGetValue<PendingEntityCounter>(key, out var entry) && entry is not null)
                    {
                        ++entry.References;
                    }
                    else
                    {
                        memoryCache.Set<PendingEntityCounter>(key, new PendingEntityCounter { References = 1 }, _pendingEntityCacheEntryOptions);
                    }

                    entityTypesList.Add(item);
                }
            }

            var result = new InMemoryPendingEntities(memoryCache, _pendingEntitiesLock)
            {
                EntityTypes = entityTypesList
            };

            return result;
        }

        public bool IsPending(Type entityType)
        {
            var key = PendingEntityKey.Create(entityType);
            lock (_pendingEntitiesLock)
            {
                if (_memoryCache.TryGetValue<PendingEntityCounter>(key, out var entry) && entry is not null)
                {
                    return entry.References > 0;
                }
                return false;
            }
        }
    }
}
