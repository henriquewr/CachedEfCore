using CachedEfCore.Context;
using CachedEfCore.Interceptors;
using CachedEfCore.SqlAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

            object cacheKey = "cacheKeyAddToCache";
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

            var keys = Enumerable.Range(0, totalAdds).Select(x => "cacheKeyAddToCache" + x).ToArray();

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
            var keys = Enumerable.Range(0, addingCount).Select(i => "removeAllKey" + i).ToArray();

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

        public class TestDbContext : CachedDbContext
        {
            public TestDbContext(Cache.DbQueryCacheStore dbQueryCacheStore) : base(dbQueryCacheStore)
            {
            }

            public Cache.DbQueryCacheStore TestDbQueryCacheStore => (Cache.DbQueryCacheStore)this.DbQueryCacheStore;

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseLazyLoadingProxies();

                optionsBuilder.UseInMemoryDatabase("test").AddInterceptors(new DbStateInterceptor(new SqlServerQueryEntityExtractor()));
                base.OnConfiguring(optionsBuilder);
            }

            public DbSet<LazyLoadEntity> LazyLoadEntity { get; set; }
            public DbSet<NonLazyLoadEntity> NonLazyLoadEntity { get; set; }
        }

        public class LazyLoadEntity
        {
            [Key]
            public int Id { get; set; }

            [ForeignKey(nameof(LazyLoadProp))]
            public int? LazyLoadPropId { get; set; }

            [ForeignKey(nameof(LazyLoadPropId))]
            public virtual NonLazyLoadEntity? LazyLoadProp { get; set; }
        }

        public class NonLazyLoadEntity
        {
            [Key]
            public int Id { get; set; }

            public string? StringData { get; set; }
        }
    }
}