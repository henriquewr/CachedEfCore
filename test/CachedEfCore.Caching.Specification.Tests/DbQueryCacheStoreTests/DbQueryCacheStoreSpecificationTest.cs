using CachedEfCore.Cache.Metrics;
using CachedEfCore.Cache.Store;
using CachedEfCore.Caching.Specification.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CachedEfCore.Caching.Specification.Tests.DbQueryCacheStoreTests
{
    public abstract class DbQueryCacheStoreSpecificationTest
    {
        protected abstract IServiceProvider CreateProvider(bool withLazyLoading);

        public record TestCacheKey : IDbQueryCacheKey
        {
            public object? Key { get; set; }
            public DbContextId? DependentDbContext { get; set; }

            public string Stringify()
            {
                return $"{Key}:{DependentDbContext}";
            }
        }

        public virtual async Task DbContextDependent_Entry_Should_Not_Be_Returned_To_Other_DbContext()
        {
            var serviceProvider = CreateProvider(true);

            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

            dbQueryCacheStore.RemoveAll();

            var key = "cacheKeyAddToCache";

            var dependentCacheKey = new TestCacheKey
            {
                Key = key,
                DependentDbContext = dbContext.ContextId,
            };
            var rootType = typeof(object); // any type

            object dbContextDependentValue = "someValue";

            dbQueryCacheStore.AddToCache(rootType, dependentCacheKey, dbContextDependentValue);

            var cached = dbQueryCacheStore.GetCached<object>(dependentCacheKey);
            Assert.Same(dbContextDependentValue, cached);

            var otherDbContextCacheKey = new TestCacheKey
            {
                Key = key,
                DependentDbContext = new DbContextId(Guid.NewGuid(), 0),
            };

            var cachedToOtherDb = dbQueryCacheStore.GetCached<object>(otherDbContextCacheKey);
            Assert.Null(cachedToOtherDb);
        }

        public virtual void AddToCache_Adds_To_Cache(object? valueToCache, bool isDbContextDependent)
        {
            var serviceProvider = CreateProvider(true);

            var dbContext = serviceProvider.GetRequiredService<TestDbContext>();

            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();
            dbQueryCacheStore.RemoveAll();

            var cacheKey = new TestCacheKey
            {
                Key = "cacheKeyAddToCache",
                DependentDbContext = isDbContextDependent ? dbContext.ContextId : null,
            };
            var rootType = typeof(object); // any type

            dbQueryCacheStore.AddToCache(rootType, cacheKey, valueToCache);

            var cached = dbQueryCacheStore.GetCached<object>(cacheKey);
            Assert.Same(valueToCache, cached);
        }

        public virtual void AddToCache_Is_Thread_Safe()
        {
            var serviceProvider = CreateProvider(true);

            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

            var dataToCache = new LazyLoadEntity();

            var rootType = typeof(object); // any type

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 16
            };

            var keys = Enumerable.Range(0, 100000).Select(x => new TestCacheKey
            {
                Key = "cacheKeyAddToCache" + x,
                DependentDbContext = dbContext.ContextId
            }).ToArray();

            Parallel.ForEach(keys, parallelOptions, key =>
            {
                dbQueryCacheStore.AddToCache(rootType, key, dataToCache);
            });

            AssertContainsAllKeys<TestCacheKey, LazyLoadEntity>(keys, dbQueryCacheStore);
        }

        public virtual void RemoveAll_Removes_All_Entries()
        {
            var serviceProvider = CreateProvider(true);

            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

            var dataToCache = new LazyLoadEntity();

            var keys = Enumerable.Range(0, 1000).Select(i => new TestCacheKey
            {
                Key = "removeAllKey" + i,
                DependentDbContext = dbContext.ContextId
            }).ToArray();

            foreach (var key in keys)
            {
                dbQueryCacheStore.AddToCache(typeof(object) /* any type */, key, dataToCache);
            }

            AssertContainsAllKeys<TestCacheKey, LazyLoadEntity>(keys, dbQueryCacheStore);

            dbQueryCacheStore.RemoveAll();

            AssertDoesNotContainAnyKeys<TestCacheKey, LazyLoadEntity>(keys, dbQueryCacheStore);
        }

        protected static void AssertContainsAllKeys<TKey, TCached>(IEnumerable<TKey> keys, IDbQueryCacheStore dbQueryCacheStore)
            where TKey : IDbQueryCacheKey
        {
            Assert.DoesNotContain(keys, k => dbQueryCacheStore.GetCached<TCached>(k) is null);
        }

        protected static void AssertDoesNotContainAnyKeys<TKey, TCached>(IEnumerable<TKey> keys, IDbQueryCacheStore dbQueryCacheStore)
            where TKey : IDbQueryCacheKey
        {
            Assert.DoesNotContain(keys, k => dbQueryCacheStore.GetCached<TCached>(k) is not null);
        }

        public static TheoryData<Func<DbContext, IDbQueryCacheStore, IDbQueryCacheKey, Type, ValueTask>> GetReportsCacheMetricsData()
        {
            return new TheoryData<Func<DbContext, IDbQueryCacheStore, IDbQueryCacheKey, Type, ValueTask>>
            {
                { async (cachedDbContext, store, key, rootEntityType) => await ValueTask.FromResult(store.GetCached<object>(key)) },
                { async (cachedDbContext, store, key, rootEntityType) => await ValueTask.FromResult(store.GetOrAdd<object>(rootEntityType, key, () => default!)) },
                { async (cachedDbContext, store, key, rootEntityType) => await store.GetOrAddAsync<object>(rootEntityType, key, () => Task.FromResult<object>(default!)) },
            };
        }
        public virtual async Task Reports_Cache_Metrics(Func<DbContext, IDbQueryCacheStore, IDbQueryCacheKey, Type, ValueTask> getFromCache)
        {
            var serviceProvider = CreateProvider(true);

            using var scope = serviceProvider.CreateScope();

            var applicationMetrics = scope.ServiceProvider.GetRequiredService<IDbQueryCacheMetrics>();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var internalMetrics = dbContext.GetService<IDbQueryCacheMetrics>();

            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

            var dataToCache = new LazyLoadEntity();

            var rootType = typeof(object); // any type

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 16
            };

            const int range = 100000;

            var keys = Enumerable.Range(0, range).Select(x => new TestCacheKey
            {
                Key = "cacheKeyAddToCache" + x,
                DependentDbContext = dbContext.ContextId
            }).ToArray();

            internalMetrics.Reset();

            Parallel.ForEach(keys, parallelOptions, key =>
            {
                dbQueryCacheStore.AddToCache(rootType, key, dataToCache);
            });

            Assert.Equal(0UL, internalMetrics.GetCacheMetrics().All);

            await Parallel.ForEachAsync(keys, parallelOptions, async (key, ct) =>
            {
                await getFromCache(dbContext, dbQueryCacheStore, key, rootType);
            });

            Assert.Equal((uint)range, internalMetrics.GetCacheMetrics().Hits);

            Assert.Equal(0U, internalMetrics.GetCacheMetrics().Misses);

            await Parallel.ForAsync(0, range, parallelOptions, async (i, ct) =>
            {
                var nonExistingKey = new TestCacheKey
                {
                    Key = "NonExisntingKey" + i,
                    DependentDbContext = dbContext.ContextId,
                };
                await getFromCache(dbContext, dbQueryCacheStore, nonExistingKey, rootType);
            });

            Assert.Equal((uint)range, internalMetrics.GetCacheMetrics().Hits);

            Assert.Equal((uint)range, internalMetrics.GetCacheMetrics().Misses);
        }
    }
}
