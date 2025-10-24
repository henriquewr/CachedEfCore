using Microsoft.Extensions.Caching.Memory;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CachedEfCore.DbQueryCacheStore.Tests
{
    public class DbQueryCacheStoreTest
    {
        private static IMemoryCache CreateMemoryCache()
        {
            return new MemoryCache(new MemoryCacheOptions()
            {
                TrackStatistics = true
            });
        }

        public DbQueryCacheStoreTest()
        {
        }

        [Fact]
        public void AddToCache_Adds_To_Cache()
        {
            var dbQueryCacheStore = new Cache.DbQueryCacheStore(CreateMemoryCache());
            object cacheKey = "cacheKeyAddToCache";
            const string valueToCache = "cachedString";
            var contextId = Guid.NewGuid();
            var rootType = typeof(object); // any type

            dbQueryCacheStore.AddToCache(contextId, rootType, cacheKey, valueToCache);

            Assert.Single(dbQueryCacheStore.TestCacheKeysByContextId);
            var contextIdKeys = dbQueryCacheStore.TestCacheKeysByContextId[contextId];
            Assert.True(contextIdKeys.Count == 1 && contextIdKeys.All(x => (string)x == (string)cacheKey));

            Assert.Single(dbQueryCacheStore.TestCacheKeysByType);
            var typeKeys = dbQueryCacheStore.TestCacheKeysByType[rootType];
            Assert.True(typeKeys.Count == 1 && typeKeys.All(x => (string)x == (string)cacheKey));

            var cached = dbQueryCacheStore.GetCached<string>(cacheKey);
            Assert.Equal(valueToCache, cached);
        }

        [Fact]
        public void AddToCache_Is_Thread_Safe()
        {
            using var memoryCache = CreateMemoryCache();
            var dbQueryCacheStore = new Cache.DbQueryCacheStore(memoryCache);
            const string valueToCache = "cachedString";
            const int totalAdds = 100000;

            var contextId = Guid.NewGuid();
            var rootType = typeof(object); // any type

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 16
            };

            var keys = Enumerable.Range(0, totalAdds).Select(x => "cacheKeyAddToCache" + x).ToArray();

            Parallel.ForEach(keys, parallelOptions, key =>
            {
                dbQueryCacheStore.AddToCache(contextId, rootType, key, valueToCache);
            });

            Assert.Single(dbQueryCacheStore.TestCacheKeysByContextId);
            var contextIdKeys = dbQueryCacheStore.TestCacheKeysByContextId[contextId];
            Assert.True(contextIdKeys.Count == totalAdds);

            Assert.Single(dbQueryCacheStore.TestCacheKeysByType);
            var typeKeys = dbQueryCacheStore.TestCacheKeysByType[rootType];
            Assert.True(typeKeys.Count == totalAdds);

            var contextIdKeysHashSet = contextIdKeys.Select(x => (string)x).ToHashSet();
            var typeKeysHashSet = typeKeys.Select(x => (string)x).ToHashSet();

            Assert.All(keys, key =>
            {
                contextIdKeysHashSet.Contains(key);
                typeKeysHashSet.Contains(key);
            });

            Assert.Equal(totalAdds, GetEntryCount(memoryCache));
        }

        [Fact]
        public void RemoveAll_Removes_All_Entries()
        {
            using var memoryCache = CreateMemoryCache();
            var dbQueryCacheStore = new Cache.DbQueryCacheStore(memoryCache);

            var startEntryCount = GetEntryCount(memoryCache);

            var addingCount = 1000;
            var fakeGuid = Guid.NewGuid();
            for (int i = 0; i < addingCount; i++)
            {
                var key = "removeAllKey" + i;
                dbQueryCacheStore.AddToCache(fakeGuid, typeof(object) /* any type */, key, i);
            }

            Assert.Equal(startEntryCount + addingCount, GetEntryCount(memoryCache));

            dbQueryCacheStore.RemoveAll();

            Assert.Empty(dbQueryCacheStore.TestCacheKeysByContextId);
            Assert.Empty(dbQueryCacheStore.TestCacheKeysByType);

            Assert.Equal(0, GetEntryCount(memoryCache));
        }

        private static long GetEntryCount(IMemoryCache memoryCache)
        {
            var entryCount = memoryCache.GetCurrentStatistics()!.CurrentEntryCount;
            return entryCount;
        }
    }
}