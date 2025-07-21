using CachedEfCore.Cache;
using Microsoft.Extensions.Caching.Memory;

namespace CachedEfCore.DbQueryCacheStore.Tests
{
    public class DbQueryCacheStoreTest
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

        [Test]
        public void AddToCache_Adds_To_Cache()
        {
            object cacheKey = "cacheKeyAddToCache";
            var existing = _dbQueryCacheStore.GetCached<string>(cacheKey);
            Assert.That(existing, Is.EqualTo(null), "Key already existing");

            _dbQueryCacheStore.AddToCache(Guid.NewGuid(), typeof(object) /* any type */, cacheKey, "cachedString");

            var cached = _dbQueryCacheStore.GetCached<string>(cacheKey);
            Assert.That(cached, Is.EqualTo("cachedString"), "Value is not added");
        }

        [Test]
        [NonParallelizable]
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

            Assert.That(GetEntryCount(), Is.EqualTo(startEntryCount + addingCount));

            _dbQueryCacheStore.RemoveAll();

            Assert.That(GetEntryCount(), Is.EqualTo(0));

            long GetEntryCount()
            {
                var entryCount = _dbQueryCacheStoreMemoryCache.GetCurrentStatistics()!.CurrentEntryCount;
                return entryCount;
            }
        }


        [OneTimeTearDown]
        public void Dispose()
        {
            _dbQueryCacheStoreMemoryCache.Dispose();
        }
    }
}