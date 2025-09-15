using CachedEfCore.Cache;
using Microsoft.Extensions.Caching.Memory;
using System;
using Xunit;

namespace CachedEfCore.DbQueryCacheStore.Tests
{
    public class DbQueryCacheStoreTest : IDisposable
    {
        private readonly IMemoryCache _dbQueryCacheStoreMemoryCache;
        private readonly IDbQueryCacheStore _dbQueryCacheStore;

        public DbQueryCacheStoreTest()
        {
            _dbQueryCacheStoreMemoryCache = new MemoryCache(new MemoryCacheOptions()
            {
                TrackStatistics = true
            });
            _dbQueryCacheStore = new Cache.DbQueryCacheStore(_dbQueryCacheStoreMemoryCache);
        }

        [Fact]
        public void AddToCache_Adds_To_Cache()
        {
            object cacheKey = "cacheKeyAddToCache";
            var existing = _dbQueryCacheStore.GetCached<string>(cacheKey);
            Assert.True(existing is null, "Key already existing");

            _dbQueryCacheStore.AddToCache(Guid.NewGuid(), typeof(object) /* any type */, cacheKey, "cachedString");

            var cached = _dbQueryCacheStore.GetCached<string>(cacheKey);
            Assert.Equal("cachedString", cached);
        }

        [Fact]
        public void RemoveAll_Removes_All_Entries()
        {
            var startEntryCount = GetEntryCount();

            var addingCount = 1000;
            var fakeGuid = Guid.NewGuid();
            for (int i = 0; i < addingCount; i++)
            {
                var key = "removeAllKey" + i;
                _dbQueryCacheStore.AddToCache(fakeGuid, typeof(object) /* any type */, key, i);
            }

            Assert.Equal(startEntryCount + addingCount, GetEntryCount());

            _dbQueryCacheStore.RemoveAll();

            Assert.Equal(0, GetEntryCount());

            long GetEntryCount()
            {
                var entryCount = _dbQueryCacheStoreMemoryCache.GetCurrentStatistics()!.CurrentEntryCount;
                return entryCount;
            }
        }

        public void Dispose()
        {
            _dbQueryCacheStoreMemoryCache.Dispose();
        }
    }
}