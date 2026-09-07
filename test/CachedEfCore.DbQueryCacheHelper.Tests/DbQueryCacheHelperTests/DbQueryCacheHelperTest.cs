using CachedEfCore.Cache.Helper;
using CachedEfCore.Cache.Metrics;
using CachedEfCore.Caching.InMemory.Configuration;
using CachedEfCore.Caching.InMemory.Store;
using CachedEfCore.Caching.InMemory.Tests.Common;
using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlServer.Configuration;
using CachedEfCore.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;

namespace CachedEfCore.Caching.InMemory.Tests.DbQueryCacheHelperTests
{
    public class DbQueryCacheHelperTest : IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;

        public DbQueryCacheHelperTest(ServiceProviderFixture serviceProviderFixture)
        {
            _serviceProviderFixture = serviceProviderFixture;
        }

        protected virtual IServiceProvider CreateProvider()
            => _serviceProviderFixture.CreateProvider(services =>
            {
                services.AddCachedEfCore();

                services.AddDbContext<TestDbContext>((serviceProvider, options) =>
                {
                    options.UseLazyLoadingProxies();

                    options.UseInMemoryDatabase(Guid.NewGuid().ToString());

                    options.UseCachedEfCore(cachedEfCoreOptions =>
                    {
                        cachedEfCoreOptions.UseInMemoryCacheStore();

                        cachedEfCoreOptions.UseSqlServer();
                    });
                });
            });

        public static TheoryData<object, bool> GetGetOrAddToCacheData()
        {
            return new()
            {
                { new LazyLoadEntity(), true },
                { new NonLazyLoadEntity(), false },
            };
        }

        [Theory]
        [MemberData(nameof(GetGetOrAddToCacheData))]
        public void GetOrAdd_Adds_And_Gets_From_Cache(object valueToCache, bool isDbContextDependent)
        {
            var serviceProvider = CreateProvider();

            var dbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var dbQueryCacheMetrics = serviceProvider.GetRequiredService<IDbQueryCacheMetrics>();
            var dbQueryCacheHelper = serviceProvider.GetRequiredService<IDbQueryCacheHelper>();
            var dbQueryCacheInternalStore = (DbQueryCacheInMemoryInternalStore)dbContext.GetService<IDbQueryCacheInMemoryInternalStore>();

            dbQueryCacheInternalStore.TestDbContextDependentKeys.Clear();
            dbQueryCacheInternalStore.TestTypeKeys.Clear();

            const string cacheKey = "cacheKeyAddToCache";

            bool created = false;
            object result;

            if (isDbContextDependent)
            {
                result = dbQueryCacheHelper.GetOrAdd<LazyLoadEntity, object/*any type*/>(dbContext, DbContextDependentCreateFunc, cacheKey);
                Assert.Single(dbQueryCacheInternalStore.TestDbContextDependentKeys);
            }
            else
            {
                result = dbQueryCacheHelper.GetOrAdd<NonLazyLoadEntity, object/*any type*/>(dbContext, NonDbContextDependentCreateFunc, cacheKey);
                Assert.Empty(dbQueryCacheInternalStore.TestDbContextDependentKeys);
            }
            dbQueryCacheMetrics.Reset();
            Assert.Single(dbQueryCacheInternalStore.TestTypeKeys);

            Assert.True(created);
            Assert.Same(valueToCache, result);

            created = false;
            object cached;

            if (isDbContextDependent)
            {
                cached = dbQueryCacheHelper.GetOrAdd<LazyLoadEntity, object/*any type*/>(dbContext, DbContextDependentCreateFunc, cacheKey);
            }
            else
            {
                cached = dbQueryCacheHelper.GetOrAdd<NonLazyLoadEntity, object/*any type*/>(dbContext, NonDbContextDependentCreateFunc, cacheKey);
            }
            dbQueryCacheMetrics.Reset();

            Assert.False(created);
            Assert.Same(valueToCache, cached);

            LazyLoadEntity DbContextDependentCreateFunc()
            {
                created = true;
                return (LazyLoadEntity)valueToCache!;
            }
            NonLazyLoadEntity NonDbContextDependentCreateFunc()
            {
                created = true;
                return (NonLazyLoadEntity)valueToCache!;
            }
        }
    }
}
