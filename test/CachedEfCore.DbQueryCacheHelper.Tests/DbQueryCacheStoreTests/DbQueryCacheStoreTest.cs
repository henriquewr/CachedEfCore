using CachedEfCore.Cache.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CachedEfCore.Cache.Tests.DbQueryCacheStoreTests
{
    public class DbQueryCacheStoreTest
    {
        private record TestCacheKey : IDbQueryCacheKey
        {
            public object? Key { get; set; }
            public Guid? DependentDbContext { get; set; }
        }

        private static IMemoryCache CreateMemoryCache()
        {
            return new MemoryCache(new MemoryCacheOptions()
            {
                TrackStatistics = true,
            });
        }

        private static TestDbContext CreateDbContext(IMemoryCache memoryCache)
        {
            var dbQueryCacheStore = new Cache.DbQueryCacheStore(memoryCache);
            var dbContext = new TestDbContext(dbQueryCacheStore);

            return dbContext;
        }

        public DbQueryCacheStoreTest()
        {
        }

        public static TheoryData<TestDbContext, object?, bool> GetAddToCacheData()
        {
            var dbContext = CreateDbContext(CreateMemoryCache());

            return new()
            {
                { dbContext, "someData", false },
                { dbContext, new LazyLoadEntity(), true },
                { dbContext, (LazyLoadEntity?)null, false },
                { dbContext, new NonLazyLoadEntity(), false },
                { dbContext, (NonLazyLoadEntity?)null, false },
            };
        }

        [Theory]
        [MemberData(nameof(GetAddToCacheData))]
        public void AddToCache_Adds_To_Cache(TestDbContext dbContext, object? valueToCache, bool isDbContextDependent)
        {
            dbContext.TestDbQueryCacheStore.TestDbContextDependentKeys.Clear();
            dbContext.TestDbQueryCacheStore.TestTypeKeys.Clear();

            var cacheKey = new TestCacheKey
            {
                Key = "cacheKeyAddToCache",
                DependentDbContext = isDbContextDependent ? dbContext.Id : null,
            };
            var rootType = typeof(object); // any type

            dbContext.TestDbQueryCacheStore.AddToCache(dbContext, rootType, cacheKey, valueToCache);

            if (isDbContextDependent)
            {
                Assert.Single(dbContext.TestDbQueryCacheStore.TestDbContextDependentKeys);
            }
            else
            {
                Assert.Empty(dbContext.TestDbQueryCacheStore.TestDbContextDependentKeys);
            }

            Assert.Single(dbContext.TestDbQueryCacheStore.TestTypeKeys);
            
            var cached = dbContext.TestDbQueryCacheStore.GetCached<object>(cacheKey);
            Assert.Equal(valueToCache, cached);
        }

        [Fact]
        public void DbContextDependent_Entry_Should_Not_Be_Returned_To_Other_DbContext()
        {
            using var memoryCache = CreateMemoryCache();

            using var dbContext = CreateDbContext(memoryCache);

            var key = "cacheKeyAddToCache";

            var dependentCacheKey = new TestCacheKey
            {
                Key = key,
                DependentDbContext = dbContext.Id,
            };
            var rootType = typeof(object); // any type

            object dbContextDependentValue = "someValue";

            dbContext.TestDbQueryCacheStore.AddToCache(dbContext, rootType, dependentCacheKey, dbContextDependentValue);

            Assert.Single(dbContext.TestDbQueryCacheStore.TestDbContextDependentKeys);

            Assert.Single(dbContext.TestDbQueryCacheStore.TestTypeKeys);

            var cached = dbContext.TestDbQueryCacheStore.GetCached<object>(dependentCacheKey);
            Assert.Equal(dbContextDependentValue, cached);

            var otherDbContextCacheKey = new TestCacheKey
            {
                Key = key,
                DependentDbContext = Guid.NewGuid(),
            };

            var cachedToOtherDb = dbContext.TestDbQueryCacheStore.GetCached<object>(otherDbContextCacheKey);
            Assert.Null(cachedToOtherDb);
        }

        [Fact]
        public void AddToCache_Is_Thread_Safe()
        {
            using var memoryCache = CreateMemoryCache();
            var dbQueryCacheStore = new Cache.DbQueryCacheStore(memoryCache);
            var dbContext = new TestDbContext(dbQueryCacheStore);

            var dataToCache = new LazyLoadEntity();

            const int totalAdds = 100000;

            var rootType = typeof(object); // any type

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 16
            };

            var keys = Enumerable.Range(0, totalAdds).Select(x => new TestCacheKey 
            { 
                Key = "cacheKeyAddToCache" + x, DependentDbContext = dbContext.Id 
            }).ToArray();

            Parallel.ForEach(keys, parallelOptions, key =>
            {
                dbQueryCacheStore.AddToCache(dbContext, rootType, key, dataToCache);
            });

            Assert.Single(dbQueryCacheStore.TestDbContextDependentKeys);

            Assert.Single(dbQueryCacheStore.TestTypeKeys);

            Assert.Equal(totalAdds, GetEntryCount(memoryCache));
        }

        [Fact]
        public static void RemoveAll_Removes_All_Entries()
        {
            using var memoryCache = CreateMemoryCache();
            var dbQueryCacheStore = new Cache.DbQueryCacheStore(memoryCache);

            var dbContext = new TestDbContext(dbQueryCacheStore);

            var startEntryCount = GetEntryCount(memoryCache);

            Assert.Equal(0, startEntryCount);

            var dataToCache = new LazyLoadEntity();

            const int addingCount = 1000;
            var keys = Enumerable.Range(0, addingCount).Select(i => new TestCacheKey 
            {
                Key = "removeAllKey" + i,
                DependentDbContext = dbContext.Id 
            }).ToArray();

            foreach (var key in keys)
            {
                dbQueryCacheStore.AddToCache(dbContext, typeof(object) /* any type */, key, dataToCache);
            }

            Assert.Single(dbQueryCacheStore.TestDbContextDependentKeys);
            Assert.Single(dbQueryCacheStore.TestTypeKeys);

            Assert.Equal(startEntryCount + addingCount, GetEntryCount(memoryCache));

            dbQueryCacheStore.RemoveAll();

            Assert.Empty(dbQueryCacheStore.TestDbContextDependentKeys);
            Assert.Empty(dbQueryCacheStore.TestTypeKeys);

            Assert.DoesNotContain(keys, k => memoryCache.TryGetValue(k, out _));
        }


        private static long GetEntryCount(IMemoryCache memoryCache)
        {
            var entryCount = memoryCache.GetCurrentStatistics()!.CurrentEntryCount;
            return entryCount;
        }
    }
}