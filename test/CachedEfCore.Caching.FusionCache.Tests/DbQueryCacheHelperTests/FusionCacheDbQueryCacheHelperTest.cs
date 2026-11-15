using CachedEfCore.Cache.Helper;
using CachedEfCore.Cache.Metrics;
using CachedEfCore.Caching.FusionCache.Configuration;
using CachedEfCore.Caching.Specification.Tests.Common;
using CachedEfCore.Caching.Specification.Tests.DbQueryCacheHelperTests;
using CachedEfCore.DependencyInjection;
using CachedEfCore.PostgreSQL.Configuration;
using CachedEfCore.Tests.Common.Fixtures;
using CachedEfCore.Tests.Common.TestContainers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace CachedEfCore.Caching.FusionCache.Tests.DbQueryCacheHelperTests
{
    public class FusionCacheDbQueryCacheHelperTest : DbQueryCacheHelperSpecificationTest, IClassFixture<ServiceProviderFixture>, IClassFixture<PostgreSqlTestContainer>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;
        private readonly PostgreSqlTestContainer _postgreSqlTestContainer;
        private readonly IFusionCache _fusionCacheInstance;

        public FusionCacheDbQueryCacheHelperTest(ServiceProviderFixture serviceProviderFixture,
            PostgreSqlTestContainer postgreSqlTestContainer)
        {
            _serviceProviderFixture = serviceProviderFixture;
            _postgreSqlTestContainer = postgreSqlTestContainer;

            _fusionCacheInstance = new ZiggyCreatures.Caching.Fusion.FusionCache(new FusionCacheOptions
            {
                CacheName = Guid.NewGuid().ToString(),
                RemoveByTagBehavior = RemoveByTagBehavior.Remove,
                DefaultEntryOptions = new FusionCacheEntryOptions()
                {
                    Duration = TimeSpan.FromMinutes(5),
                }
            });
        }

        protected override IServiceProvider CreateProvider(bool withLazyLoading, [CallerMemberName] string caller = "")
            => _serviceProviderFixture.CreateProvider(services =>
            {
                services.AddCachedEfCore();

                services.AddDbContext<TestDbContext>((serviceProvider, options) =>
                {
                    options.UseLazyLoadingProxies(withLazyLoading);

                    options.UseNpgsql(_postgreSqlTestContainer.ConnectionString);

                    options.UseCachedEfCore(cachedEfCoreOptions =>
                    {
                        cachedEfCoreOptions.UseFusionCacheStore(options =>
                        {
                            options.UseInstance(_fusionCacheInstance);
                        });

                        cachedEfCoreOptions.UsePostgreSql();
                    });
                });
            });

        protected override IServiceProvider CreatePooledProvider(bool withLazyLoading, [CallerMemberName] string caller = "")
            => _serviceProviderFixture.CreateProvider(services =>
            {
                services.AddCachedEfCore();

                services.AddDbContextPool<TestDbContext>((serviceProvider, options) =>
                {
                    options.UseLazyLoadingProxies(withLazyLoading);

                    options.UseNpgsql(_postgreSqlTestContainer.ConnectionString);

                    options.UseCachedEfCore(cachedEfCoreOptions =>
                    {
                        cachedEfCoreOptions.UseFusionCacheStore(options =>
                        {
                            options.UseInstance(_fusionCacheInstance);
                        });

                        cachedEfCoreOptions.UsePostgreSql();
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
            var serviceProvider2 = CreateProvider(true);

            var dbContext = serviceProvider.GetRequiredService<TestDbContext>();

            var dbQueryCacheMetrics = serviceProvider.GetRequiredService<IDbQueryCacheMetrics>();
            var dbQueryCacheHelper = serviceProvider.GetRequiredService<IDbQueryCacheHelper>();

            const string cacheKey = "cacheKeyAddToCache";

            bool created = false;
            object result;

            if (isDbContextDependent)
            {
                result = dbQueryCacheHelper.GetOrAdd<LazyLoadEntity, object/*any type*/>(dbContext, DbContextDependentCreateFunc, cacheKey);
            }
            else
            {
                result = dbQueryCacheHelper.GetOrAdd<NonLazyLoadEntity, object/*any type*/>(dbContext, NonDbContextDependentCreateFunc, cacheKey);
            }
            dbQueryCacheMetrics.Reset();

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
