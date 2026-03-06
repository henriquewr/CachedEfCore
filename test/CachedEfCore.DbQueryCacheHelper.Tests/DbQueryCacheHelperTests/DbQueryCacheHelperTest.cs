using CachedEfCore.Cache.Helper;
using CachedEfCore.Cache.Tests.Common;
using CachedEfCore.DependencyInjection;
using CachedEfCore.KeyGeneration;
using CachedEfCore.KeyGeneration.EvalTypeChecker;
using CachedEfCore.KeyGeneration.ExpressionKeyGen;
using CachedEfCore.SqlAnalysis;
using CachedEfCore.Tests.Common.Fixtures;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using Xunit;

namespace CachedEfCore.Cache.Tests.DbQueryCacheHelperTests
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
                services.AddDbContext<TestDbContext>((serviceProvider, options) =>
                {
                });

                services.AddCachedEfCore<SqlServerQueryEntityExtractor>();

                services.Replace(ServiceDescriptor.Singleton<IMemoryCache>(x => new MemoryCache(new MemoryCacheOptions()
                {
                    TrackStatistics = true,
                })));
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
            var dbQueryCacheHelper = serviceProvider.GetRequiredService<IDbQueryCacheHelper>();

            dbContext.TestDbQueryCacheStore.TestDbContextDependentKeys.Clear();
            dbContext.TestDbQueryCacheStore.TestTypeKeys.Clear();

            const string cacheKey = "cacheKeyAddToCache";

            bool created = false;
            object result;

            if (isDbContextDependent)
            {
                result = dbQueryCacheHelper.GetOrAdd<LazyLoadEntity, object/*any type*/>(dbContext, DbContextDependentCreateFunc, cacheKey);
                Assert.Single(dbContext.TestDbQueryCacheStore.TestDbContextDependentKeys);
            }
            else
            {
                result = dbQueryCacheHelper.GetOrAdd<NonLazyLoadEntity, object/*any type*/>(dbContext, NonDbContextDependentCreateFunc, cacheKey);
                Assert.Empty(dbContext.TestDbQueryCacheStore.TestDbContextDependentKeys);
            }
            Assert.Single(dbContext.TestDbQueryCacheStore.TestTypeKeys);

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
