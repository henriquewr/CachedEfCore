using CachedEfCore.Cache.Metrics;
using CachedEfCore.Cache.Tests.Common;
using CachedEfCore.Context;
using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlAnalysis.SqlServer;
using CachedEfCore.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CachedEfCore.Cache.Tests.DbQueryCacheStoreTests
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
                       options.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

                       options.UseCachedEfCore(cachedEfCoreOptions =>
                       {
                           cachedEfCoreOptions.WithSqlQueryEntityExtractor<SqlServerQueryEntityExtractor>();
                       });
                   });
               });

        protected TestDbContext GetDbContext()
        {
            return CreateProvider().GetRequiredService<TestDbContext>();
        }

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
            var dbContext = GetDbContext();

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
            Assert.Same(valueToCache, cached);
        }

        [Fact]
        public void DbContextDependent_Entry_Should_Not_Be_Returned_To_Other_DbContext()
        {
            var serviceProvider = CreateProvider();

            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

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
            Assert.Same(dbContextDependentValue, cached);

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
            var serviceProvider = CreateProvider();

            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

            var dbQueryCacheStore = (DbQueryCacheStore)dbContext.DbQueryCacheStore;

            var dataToCache = new LazyLoadEntity();

            var rootType = typeof(object); // any type

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 16
            };

            var keys = Enumerable.Range(0, 100000).Select(x => new TestCacheKey 
            { 
                Key = "cacheKeyAddToCache" + x, DependentDbContext = dbContext.Id 
            }).ToArray();

            Parallel.ForEach(keys, parallelOptions, key =>
            {
                dbQueryCacheStore.AddToCache(dbContext, rootType, key, dataToCache);
            });

            Assert.Single(dbQueryCacheStore.TestDbContextDependentKeys);

            Assert.Single(dbQueryCacheStore.TestTypeKeys);

            AssertContainsAllKeys<TestCacheKey, LazyLoadEntity>(keys, dbQueryCacheStore);
        }

        [Fact]
        public void RemoveAll_Removes_All_Entries()
        {
            var serviceProvider = CreateProvider();

            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

            var dbQueryCacheStore = (DbQueryCacheStore)dbContext.DbQueryCacheStore;

            var dataToCache = new LazyLoadEntity();

            var keys = Enumerable.Range(0, 1000).Select(i => new TestCacheKey 
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

            AssertContainsAllKeys<TestCacheKey, LazyLoadEntity>(keys, dbQueryCacheStore);

            dbContext.DbQueryCacheStore.RemoveAll();

            Assert.Empty(dbQueryCacheStore.TestDbContextDependentKeys);
            Assert.Empty(dbQueryCacheStore.TestTypeKeys);

            AssertDoesNotContainAnyKeys<TestCacheKey, LazyLoadEntity>(keys, dbQueryCacheStore);
        }

        [Fact]
        public async Task GetCached_Reports_Cache_Metrics()
        {
            await Reports_Cache_Metrics_Impl(async (cachedDbContext, store, key, rootEntityType) => await ValueTask.FromResult(store.GetCached<object>(key)));
        }

        [Fact]
        public async Task GetOrAdd_Reports_Cache_Metrics()
        {
            await Reports_Cache_Metrics_Impl(async (cachedDbContext, store, key, rootEntityType) => await ValueTask.FromResult(store.GetOrAdd<object>(cachedDbContext, rootEntityType, key, () => default!)));
        }

        [Fact]
        public async Task GetOrAddAsync_Reports_Cache_Metrics()
        {
            await Reports_Cache_Metrics_Impl(async (cachedDbContext, store, key, rootEntityType) => await store.GetOrAddAsync<object>(cachedDbContext, rootEntityType, key, () => Task.FromResult<object>(default!)));
        }

        private async Task Reports_Cache_Metrics_Impl(Func<ICachedDbContext, IDbQueryCacheStore, IDbQueryCacheKey, Type, ValueTask> getFromCache)
        {
            var serviceProvider = CreateProvider();

            using var scope = serviceProvider.CreateScope();

            var applicationMetrics = scope.ServiceProvider.GetRequiredService<IDbQueryCacheMetrics>();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var internalMetrics = dbContext.GetService<IDbQueryCacheMetrics>();

            applicationMetrics.Reset();
            internalMetrics.Reset();

            var dbQueryCacheStore = (DbQueryCacheStore)dbContext.DbQueryCacheStore;

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
                DependentDbContext = dbContext.Id
            }).ToArray();

            Parallel.ForEach(keys, parallelOptions, key =>
            {
                dbQueryCacheStore.AddToCache(dbContext, rootType, key, dataToCache);
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
                    DependentDbContext = dbContext.Id
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