using CachedEfCore.Cache.Metrics;
using CachedEfCore.Cache.Store;
using CachedEfCore.Caching.InMemory.Configuration;
using CachedEfCore.Caching.InMemory.Store;
using CachedEfCore.Caching.InMemory.Tests.Common;
using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlServer.Configuration;
using CachedEfCore.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CachedEfCore.Caching.InMemory.Tests.DbQueryCacheStoreTests
{
    public class DbQueryCacheStoreTest : IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;

        public DbQueryCacheStoreTest(ServiceProviderFixture serviceProviderFixture)
        {
            _serviceProviderFixture = serviceProviderFixture;
        }

        private record TestCacheKey : IDbQueryCacheKey
        {
            public object? Key { get; set; }
            public Guid? DependentDbContext { get; set; }
        }

        protected virtual IServiceProvider CreateProvider()
           => _serviceProviderFixture.CreateProvider(services =>
               {
                   services.AddCachedEfCore();

                   services.AddDbContext<TestDbContext>((serviceProvider, options) =>
                   {
                       options.UseLazyLoadingProxies();

                       options.UseInMemoryDatabase(Guid.NewGuid().ToString());

                       options.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

                       options.UseCachedEfCore(cachedEfCoreOptions =>
                       {
                           cachedEfCoreOptions.UseInMemoryCacheStore();

                           cachedEfCoreOptions.UseSqlServer();
                       });
                   });
               });

        public static TheoryData<object?, bool> GetAddToCacheData()
        {
            return new()
            {
                { "someData", false },
                { new LazyLoadEntity(), true },
                { (LazyLoadEntity?)null, false },
                { new NonLazyLoadEntity(), false },
                { (NonLazyLoadEntity?)null, false },
            };
        }

        [Theory]
        [MemberData(nameof(GetAddToCacheData))]
        public void AddToCache_Adds_To_Cache(object? valueToCache, bool isDbContextDependent)
        {
            var serviceProvider = CreateProvider();

            var dbContext = serviceProvider.GetRequiredService<TestDbContext>();

            var dbQueryCacheInternalStore = (DbQueryCacheInMemoryInternalStore)dbContext.GetService<IDbQueryCacheInMemoryInternalStore>();
            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

            dbQueryCacheInternalStore.TestDbContextDependentKeys.Clear();
            dbQueryCacheInternalStore.TestTypeKeys.Clear();

            var cacheKey = new TestCacheKey
            {
                Key = "cacheKeyAddToCache",
                DependentDbContext = isDbContextDependent ? dbContext.ContextId.InstanceId : null,
            };
            var rootType = typeof(object); // any type

            dbQueryCacheStore.AddToCache(rootType, cacheKey, valueToCache);

            if (isDbContextDependent)
            {
                Assert.Single(dbQueryCacheInternalStore.TestDbContextDependentKeys);
            }
            else
            {
                Assert.Empty(dbQueryCacheInternalStore.TestDbContextDependentKeys);
            }

            Assert.Single(dbQueryCacheInternalStore.TestTypeKeys);

            var cached = dbQueryCacheStore.GetCached<object>(cacheKey);
            Assert.Same(valueToCache, cached);
        }

        [Fact]
        public void DbContextDependent_Entry_Should_Not_Be_Returned_To_Other_DbContext()
        {
            var serviceProvider = CreateProvider();

            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var dbQueryCacheInternalStore = (DbQueryCacheInMemoryInternalStore)dbContext.GetService<IDbQueryCacheInMemoryInternalStore>();
            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

            var key = "cacheKeyAddToCache";

            var dependentCacheKey = new TestCacheKey
            {
                Key = key,
                DependentDbContext = dbContext.ContextId.InstanceId,
            };
            var rootType = typeof(object); // any type

            object dbContextDependentValue = "someValue";

            dbQueryCacheStore.AddToCache(rootType, dependentCacheKey, dbContextDependentValue);

            Assert.Single(dbQueryCacheInternalStore.TestDbContextDependentKeys);

            Assert.Single(dbQueryCacheInternalStore.TestTypeKeys);

            var cached = dbQueryCacheStore.GetCached<object>(dependentCacheKey);
            Assert.Same(dbContextDependentValue, cached);

            var otherDbContextCacheKey = new TestCacheKey
            {
                Key = key,
                DependentDbContext = Guid.NewGuid(),
            };

            var cachedToOtherDb = dbQueryCacheStore.GetCached<object>(otherDbContextCacheKey);
            Assert.Null(cachedToOtherDb);
        }

        [Fact]
        public void AddToCache_Is_Thread_Safe()
        {
            var serviceProvider = CreateProvider();

            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var dbQueryCacheInternalStore = (DbQueryCacheInMemoryInternalStore)dbContext.GetService<IDbQueryCacheInMemoryInternalStore>();
            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

            var dataToCache = new LazyLoadEntity();

            var rootType = typeof(object); // any type

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 16
            };

            var keys = Enumerable.Range(0, 100000).Select(x => new TestCacheKey 
            { 
                Key = "cacheKeyAddToCache" + x, DependentDbContext = dbContext.ContextId.InstanceId
            }).ToArray();

            Parallel.ForEach(keys, parallelOptions, key =>
            {
                dbQueryCacheStore.AddToCache(rootType, key, dataToCache);
            });

            Assert.Single(dbQueryCacheInternalStore.TestDbContextDependentKeys);

            Assert.Single(dbQueryCacheInternalStore.TestTypeKeys);

            AssertContainsAllKeys<TestCacheKey, LazyLoadEntity>(keys, dbQueryCacheStore);
        }

        [Fact]
        public void RemoveAll_Removes_All_Entries()
        {
            var serviceProvider = CreateProvider();

            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var dbQueryCacheInternalStore = (DbQueryCacheInMemoryInternalStore)dbContext.GetService<IDbQueryCacheInMemoryInternalStore>();
            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

            var dataToCache = new LazyLoadEntity();

            var keys = Enumerable.Range(0, 1000).Select(i => new TestCacheKey 
            {
                Key = "removeAllKey" + i,
                DependentDbContext = dbContext.ContextId.InstanceId
            }).ToArray();

            foreach (var key in keys)
            {
                dbQueryCacheStore.AddToCache(typeof(object) /* any type */, key, dataToCache);
            }

            Assert.Single(dbQueryCacheInternalStore.TestDbContextDependentKeys);
            Assert.Single(dbQueryCacheInternalStore.TestTypeKeys);

            AssertContainsAllKeys<TestCacheKey, LazyLoadEntity>(keys, dbQueryCacheStore);

            dbQueryCacheStore.RemoveAll();

            Assert.Empty(dbQueryCacheInternalStore.TestDbContextDependentKeys);
            Assert.Empty(dbQueryCacheInternalStore.TestTypeKeys);

            AssertDoesNotContainAnyKeys<TestCacheKey, LazyLoadEntity>(keys, dbQueryCacheStore);
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

        [Theory]
        [MemberData(nameof(GetReportsCacheMetricsData))]
        public async Task Reports_Cache_Metrics(Func<DbContext, IDbQueryCacheStore, IDbQueryCacheKey, Type, ValueTask> getFromCache)
        {
            var serviceProvider = CreateProvider();

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
                DependentDbContext = dbContext.ContextId.InstanceId
            }).ToArray();

            applicationMetrics.Reset();
            internalMetrics.Reset();

            Parallel.ForEach(keys, parallelOptions, key =>
            {
                dbQueryCacheStore.AddToCache(rootType, key, dataToCache);
            });

            Assert.Equal(0UL, internalMetrics.GetCacheMetrics().All);
            Assert.Equal(0UL, applicationMetrics.GetCacheMetrics().All);

            await Parallel.ForEachAsync(keys, parallelOptions, async (key, ct) =>
            {
                await getFromCache(dbContext, dbQueryCacheStore, key, rootType);
            });

            Assert.Equal((uint)range, internalMetrics.GetCacheMetrics().Hits);
            Assert.Equal((uint)range, applicationMetrics.GetCacheMetrics().Hits);

            Assert.Equal(0U, internalMetrics.GetCacheMetrics().Misses);
            Assert.Equal(0U, applicationMetrics.GetCacheMetrics().Misses);

            await Parallel.ForAsync(0, range, parallelOptions, async (i, ct) =>
            {
                var nonExistingKey = new TestCacheKey
                {
                    Key = "NonExisntingKey" + i,
                    DependentDbContext = dbContext.ContextId.InstanceId,
                };
                await getFromCache(dbContext, dbQueryCacheStore, nonExistingKey, rootType);
            });

            Assert.Equal((uint)range, internalMetrics.GetCacheMetrics().Hits);
            Assert.Equal((uint)range, applicationMetrics.GetCacheMetrics().Hits);

            Assert.Equal((uint)range, internalMetrics.GetCacheMetrics().Misses);
            Assert.Equal((uint)range, applicationMetrics.GetCacheMetrics().Misses);
        }

        private static void AssertContainsAllKeys<TKey, TCached>(IEnumerable<TKey> keys, IDbQueryCacheStore dbQueryCacheStore)
            where TKey : IDbQueryCacheKey
        {
            Assert.DoesNotContain(keys, k => dbQueryCacheStore.GetCached<TCached>(k) is null);
        }

        private static void AssertDoesNotContainAnyKeys<TKey, TCached>(IEnumerable<TKey> keys, IDbQueryCacheStore dbQueryCacheStore)
            where TKey : IDbQueryCacheKey
        {
            Assert.DoesNotContain(keys, k => dbQueryCacheStore.GetCached<TCached>(k) is not null);
        }
    }
}