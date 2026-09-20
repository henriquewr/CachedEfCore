using CachedEfCore.Cache.Helper;
using CachedEfCore.Cache.Metrics;
using CachedEfCore.Caching.InMemory.Configuration;
using CachedEfCore.Caching.InMemory.Store;
using CachedEfCore.Caching.Specification.Tests.Common;
using CachedEfCore.Caching.Specification.Tests.DbQueryCacheHelperTests;
using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlServer.Configuration;
using CachedEfCore.Tests.Common.Fixtures;
using CachedEfCore.Tests.Common.TestContainers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Xunit;

namespace CachedEfCore.Caching.InMemory.Tests.DbQueryCacheHelperTests
{
    public class InMemoryDbQueryCacheHelperTest : DbQueryCacheHelperSpecificationTest, IClassFixture<ServiceProviderFixture>, IClassFixture<SqlServerTestContainer>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;
        private readonly SqlServerTestContainer _sqlServerTestContainer;

        public InMemoryDbQueryCacheHelperTest(ServiceProviderFixture serviceProviderFixture, 
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
                    options.UseLazyLoadingProxies(withLazyLoading);

                    options.UseSqlServer(_sqlServerTestContainer.ConnectionString);

                    options.UseCachedEfCore(cachedEfCoreOptions =>
                    {
                        cachedEfCoreOptions.UseInMemoryCacheStore();

                        cachedEfCoreOptions.UseSqlServer();
                    });
                });
            });

        protected override IServiceProvider CreatePooledProvider(bool withLazyLoading)
            => _serviceProviderFixture.CreateProvider(services =>
            {
                services.AddCachedEfCore();

                services.AddDbContextPool<TestDbContext>((serviceProvider, options) =>
                {
                    options.UseLazyLoadingProxies(withLazyLoading);

                    options.UseSqlServer(_sqlServerTestContainer.ConnectionString);

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
        public override async Task GetOrAdd_Adds_And_Gets_From_Cache(object valueToCache, bool isDbContextDependent)
        {
            var serviceProvider = CreateProvider(true);

            var dbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var dbQueryCacheMetrics = serviceProvider.GetRequiredService<IDbQueryCacheMetrics>();
            var dbQueryCacheHelper = serviceProvider.GetRequiredService<IDbQueryCacheHelper>();
            var dbQueryCacheInternalStore = (DbQueryCacheInMemoryInternalStore)dbContext.GetService<IDbQueryCacheInMemoryInternalStore>();

            dbQueryCacheInternalStore._dbContextDependentKeys.Clear();
            dbQueryCacheInternalStore._typeKeys.Clear();

            const string cacheKey = "cacheKeyAddToCache";

            bool created = false;
            object result;

            if (isDbContextDependent)
            {
                result = dbQueryCacheHelper.GetOrAdd<LazyLoadEntity, object/*any type*/>(dbContext, DbContextDependentCreateFunc, cacheKey);
                Assert.Single(dbQueryCacheInternalStore._dbContextDependentKeys);
            }
            else
            {
                result = dbQueryCacheHelper.GetOrAdd<NonLazyLoadEntity, object/*any type*/>(dbContext, NonDbContextDependentCreateFunc, cacheKey);
                Assert.Empty(dbQueryCacheInternalStore._dbContextDependentKeys);
            }
            dbQueryCacheMetrics.Reset();
            Assert.Single(dbQueryCacheInternalStore._typeKeys);

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

            {
                var serviceProvider2 = CreateProvider(true);

                var dbContext2 = serviceProvider2.GetRequiredService<TestDbContext>();

                var dbQueryCacheHelper2 = serviceProvider2.GetRequiredService<IDbQueryCacheHelper>();
                created = false;
                if (isDbContextDependent)
                {
                    var result2 = dbQueryCacheHelper2.GetOrAdd<LazyLoadEntity, object/*any type*/>(dbContext2, DbContextDependentCreateFunc, cacheKey);
                    Assert.True(created);
                }
                else
                {
                    var result2 = dbQueryCacheHelper2.GetOrAdd<NonLazyLoadEntity, object/*any type*/>(dbContext2, NonDbContextDependentCreateFunc, cacheKey);
                    Assert.False(created);
                }
            }

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
