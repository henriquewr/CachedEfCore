using CachedEfCore.Cache.Store;
using CachedEfCore.Caching.FusionCache.Configuration;
using CachedEfCore.Caching.Specification.Tests.Common;
using CachedEfCore.Caching.Specification.Tests.DbQueryCacheStoreTests;
using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlServer.Configuration;
using CachedEfCore.Tests.Common.Fixtures;
using CachedEfCore.Tests.Common.TestContainers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;

namespace CachedEfCore.Caching.FusionCache.Tests.DbQueryCacheStoreTests
{
    public class FusionCacheDbQueryCacheStoreTest : DbQueryCacheStoreSpecificationTest, IClassFixture<ServiceProviderFixture>, IClassFixture<SqlServerTestContainer>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;
        private readonly SqlServerTestContainer _sqlServerTestContainer;

        public FusionCacheDbQueryCacheStoreTest(ServiceProviderFixture serviceProviderFixture,
            SqlServerTestContainer sqlServerTestContainer)
        {
            _serviceProviderFixture = serviceProviderFixture;
            _sqlServerTestContainer = sqlServerTestContainer;
        }

        protected override IServiceProvider CreateProvider(bool withLazyLoading)
           => _serviceProviderFixture.CreateProvider(services =>
               {
                   services.AddCachedEfCore();

                   services.AddDbContext<TestDbContext>((serviceProvider, options) =>
                   {
                       options.EnableServiceProviderCaching(false);

                       options.UseLazyLoadingProxies(withLazyLoading);

                       options.UseSqlServer(_sqlServerTestContainer.ConnectionString);

                       options.UseCachedEfCore(cachedEfCoreOptions =>
                       {
                           cachedEfCoreOptions.UseFusionCacheStore(options =>
                           {
                               options.ConfigureRegistration(fusionCacheServices =>
                               {
                                   fusionCacheServices.AddFusionCache();
                                   fusionCacheServices.AddFusionCacheMemoryBackplane();
                               });
                           });

                           cachedEfCoreOptions.UseSqlServer();
                       });
                   });
               });

        protected virtual IServiceProvider CreateProvider(bool withLazyLoading, IDistributedCache distributedCache, MemoryBackplane memoryBackplane, bool skipMemoryCache)
           => _serviceProviderFixture.CreateProvider(services =>
           {
               services.AddCachedEfCore();

               services.AddDbContext<TestDbContext>((serviceProvider, options) =>
               {
                   options.EnableServiceProviderCaching(false);

                   options.UseLazyLoadingProxies(withLazyLoading);

                   options.UseSqlServer(_sqlServerTestContainer.ConnectionString);

                   options.UseCachedEfCore(cachedEfCoreOptions =>
                   {
                       cachedEfCoreOptions.UseFusionCacheStore(options =>
                       {
                           options.ConfigureRegistration(fusionCacheServices =>
                           {
                               fusionCacheServices.AddFusionCache()
                                .WithBackplane(memoryBackplane)
                                .WithDistributedCache(distributedCache)
                                .WithSystemTextJsonSerializer()
                                .WithDefaultEntryOptions(x => x.SetSkipMemoryCache(skipMemoryCache));
                           });
                       });

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
            base.AddToCache_Adds_To_Cache(valueToCache, isDbContextDependent);
        }

        [Fact]
        public override Task DbContextDependent_Entry_Should_Not_Be_Returned_To_Other_DbContext()
        {
            return base.DbContextDependent_Entry_Should_Not_Be_Returned_To_Other_DbContext();
        }

        [Fact]
        public override void AddToCache_Is_Thread_Safe()
        {
            base.AddToCache_Is_Thread_Safe();
        }

        [Fact]
        public override void RemoveAll_Removes_All_Entries()
        {
            base.RemoveAll_Removes_All_Entries();
        }

        [Theory]
        [MemberData(nameof(GetReportsCacheMetricsData))]
        public override async Task Reports_Cache_Metrics(Func<DbContext, IDbQueryCacheStore, IDbQueryCacheKey, Type, ValueTask> getFromCache)
        {
            await base.Reports_Cache_Metrics(getFromCache);
        }

        [Fact]
        public async Task Should_Not_Get_From_Distributed_Cache_When_Is_DbContext_Dependent()
        {
            var memoryBackplane = new MemoryBackplane(Options.Create(new MemoryBackplaneOptions()));
            var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            var provider1 = CreateProvider(true, distributedCache, memoryBackplane, false);

            var dbContext1 = provider1.GetRequiredService<TestDbContext>();

            var dbQueryCacheStore1 = dbContext1.GetService<IDbQueryCacheStore>();
            dbQueryCacheStore1.RemoveAll();

            string valueToCache = nameof(Should_Not_Get_From_Distributed_Cache_When_Is_DbContext_Dependent);

            var cacheKey = new TestCacheKey
            {
                Key = $"{nameof(Should_Not_Get_From_Distributed_Cache_When_Is_DbContext_Dependent)}BackplaneCacheKeyAddToCache",
                DependentDbContext = dbContext1.ContextId,
            };
            var rootType = typeof(object); // any type

            dbQueryCacheStore1.AddToCache(rootType, cacheKey, valueToCache);

            var cached = dbQueryCacheStore1.GetCached<string>(cacheKey);
            Assert.Same(valueToCache, cached);

            var provider2 = CreateProvider(true, distributedCache, memoryBackplane, true);

            var dbContext2 = provider2.GetRequiredService<TestDbContext>();

            var dbQueryCacheStore2 = dbContext2.GetService<IDbQueryCacheStore>();

            var cached2 = dbQueryCacheStore2.GetCached<string>(cacheKey);
            Assert.Null(cached2);
        }

        [Fact]
        public async Task Should_Get_From_Distributed_Cache_When_Is_Not_DbContext_Dependent_And_Backplane_Should_Notify_Changes()
        {
            var memoryBackplane = new MemoryBackplane(Options.Create(new MemoryBackplaneOptions()));
            var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

            var provider1 = CreateProvider(true, distributedCache, memoryBackplane, false);

            var dbContext1 = provider1.GetRequiredService<TestDbContext>();
            await dbContext1.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

            var dbQueryCacheStore1 = dbContext1.GetService<IDbQueryCacheStore>();
            dbQueryCacheStore1.RemoveAll();

            string valueToCache = nameof(Should_Get_From_Distributed_Cache_When_Is_Not_DbContext_Dependent_And_Backplane_Should_Notify_Changes);

            var cacheKey = new TestCacheKey
            {
                Key = $"{nameof(Should_Get_From_Distributed_Cache_When_Is_Not_DbContext_Dependent_And_Backplane_Should_Notify_Changes)}CacheKeyAddToCache",
                DependentDbContext = null,
            };
            var rootType = typeof(NonLazyLoadEntity);

            dbQueryCacheStore1.AddToCache(rootType, cacheKey, valueToCache);

            var cached = dbQueryCacheStore1.GetCached<string>(cacheKey);
            Assert.Same(valueToCache, cached);

            var provider2 = CreateProvider(true, distributedCache, memoryBackplane, true);

            var dbContext2 = provider2.GetRequiredService<TestDbContext>();

            var dbQueryCacheStore2 = dbContext2.GetService<IDbQueryCacheStore>();

            var cached2 = dbQueryCacheStore2.GetCached<string>(cacheKey);
            Assert.Equal(valueToCache, cached2);

            dbContext1.NonLazyLoadEntity.Add(new NonLazyLoadEntity
            {
                StringData = "test",
            });

            await dbContext1.SaveChangesAsync(TestContext.Current.CancellationToken);

            var afterSaveCached1 = dbQueryCacheStore1.GetCached<string>(cacheKey);
            var afterSaveCached2 = dbQueryCacheStore2.GetCached<string>(cacheKey);

            Assert.Null(afterSaveCached1);
            Assert.Null(afterSaveCached2);
        }
    }
}