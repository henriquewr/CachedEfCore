using CachedEfCore.Cache.Store;
using CachedEfCore.Caching.InMemory.Configuration;
using CachedEfCore.Caching.InMemory.Store;
using CachedEfCore.Caching.Specification.Tests.Common;
using CachedEfCore.Caching.Specification.Tests.DbQueryCacheStoreTests;
using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlServer.Configuration;
using CachedEfCore.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CachedEfCore.Caching.InMemory.Tests.DbQueryCacheStoreTests
{
    public class InMemoryDbQueryCacheStoreTest : DbQueryCacheStoreSpecificationTest, IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;

        public InMemoryDbQueryCacheStoreTest(ServiceProviderFixture serviceProviderFixture)
        {
            _serviceProviderFixture = serviceProviderFixture;
        }

        protected override IServiceProvider CreateProvider(bool withLazyLoading)
           => _serviceProviderFixture.CreateProvider(services =>
               {
                   services.AddCachedEfCore();

                   services.AddDbContext<TestDbContext>((serviceProvider, options) =>
                   {
                       options.UseLazyLoadingProxies(withLazyLoading);

                       options.UseInMemoryDatabase("test");

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
        public override void AddToCache_Adds_To_Cache(object? valueToCache, bool isDbContextDependent)
        {
            var serviceProvider = CreateProvider(true);

            var dbContext = serviceProvider.GetRequiredService<TestDbContext>();

            var dbQueryCacheInternalStore = (DbQueryCacheInMemoryInternalStore)dbContext.GetService<IDbQueryCacheInMemoryInternalStore>();
            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

            dbQueryCacheStore.RemoveAll();

            var cacheKey = new TestCacheKey
            {
                Key = "cacheKeyAddToCache",
                DependentDbContext = isDbContextDependent ? dbContext.ContextId : null,
            };
            var rootType = typeof(object); // any type

            dbQueryCacheStore.AddToCache(rootType, cacheKey, valueToCache);

            if (isDbContextDependent)
            {
                Assert.Single(dbQueryCacheInternalStore._dbContextDependentKeys);
            }
            else
            {
                Assert.Empty(dbQueryCacheInternalStore._dbContextDependentKeys);
            }

            Assert.Single(dbQueryCacheInternalStore._typeKeys);

            var cached = dbQueryCacheStore.GetCached<object>(cacheKey);
            Assert.Same(valueToCache, cached);
        }

        [Fact]
        public override async Task DbContextDependent_Entry_Should_Not_Be_Returned_To_Other_DbContext()
        {
            var serviceProvider = CreateProvider(true);

            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var dbQueryCacheInternalStore = (DbQueryCacheInMemoryInternalStore)dbContext.GetService<IDbQueryCacheInMemoryInternalStore>();
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

            Assert.Single(dbQueryCacheInternalStore._dbContextDependentKeys);

            Assert.Single(dbQueryCacheInternalStore._typeKeys);

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

        [Fact]
        public override void AddToCache_Is_Thread_Safe()
        {
            var serviceProvider = CreateProvider(true);

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
                Key = "cacheKeyAddToCache" + x, DependentDbContext = dbContext.ContextId
            }).ToArray();

            Parallel.ForEach(keys, parallelOptions, key =>
            {
                dbQueryCacheStore.AddToCache(rootType, key, dataToCache);
            });

            Assert.Single(dbQueryCacheInternalStore._dbContextDependentKeys);

            Assert.Single(dbQueryCacheInternalStore._typeKeys);

            AssertContainsAllKeys<TestCacheKey, LazyLoadEntity>(keys, dbQueryCacheStore);
        }

        [Fact]
        public override void RemoveAll_Removes_All_Entries()
        {
            var serviceProvider = CreateProvider(true);

            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var dbQueryCacheInternalStore = (DbQueryCacheInMemoryInternalStore)dbContext.GetService<IDbQueryCacheInMemoryInternalStore>();
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

            Assert.Single(dbQueryCacheInternalStore._dbContextDependentKeys);
            Assert.Single(dbQueryCacheInternalStore._typeKeys);

            AssertContainsAllKeys<TestCacheKey, LazyLoadEntity>(keys, dbQueryCacheStore);

            dbQueryCacheStore.RemoveAll();

            Assert.Empty(dbQueryCacheInternalStore._dbContextDependentKeys);
            Assert.Empty(dbQueryCacheInternalStore._typeKeys);

            AssertDoesNotContainAnyKeys<TestCacheKey, LazyLoadEntity>(keys, dbQueryCacheStore);
        }

        [Theory]
        [MemberData(nameof(GetReportsCacheMetricsData))]
        public override async Task Reports_Cache_Metrics(Func<DbContext, IDbQueryCacheStore, IDbQueryCacheKey, Type, ValueTask> getFromCache)
        {
            await base.Reports_Cache_Metrics(getFromCache);
        }
    }
}