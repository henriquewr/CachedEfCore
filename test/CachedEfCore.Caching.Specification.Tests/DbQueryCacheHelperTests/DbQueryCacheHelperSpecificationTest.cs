using CachedEfCore.Cache.Helper;
using CachedEfCore.Cache.Metrics;
using CachedEfCore.Caching.Specification.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace CachedEfCore.Caching.Specification.Tests.DbQueryCacheHelperTests
{
    public abstract class DbQueryCacheHelperSpecificationTest
    {
        protected abstract IServiceProvider CreatePooledProvider(bool withLazyLoading);
        protected abstract IServiceProvider CreateProvider(bool withLazyLoading);

        public abstract Task GetOrAdd_Adds_And_Gets_From_Cache(object valueToCache, bool isDbContextDependent);

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public virtual async Task DbContextDependent_Entry_Should_Not_Be_Returned_To_Other_DbContext_Multiple_Threads(bool pooledDbContext)
        {
            var serviceProvider = pooledDbContext ? CreatePooledProvider(true) : CreateProvider(true);

            {
                using var scope = serviceProvider.CreateScope();

                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

                await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

                dbContext.LazyLoadEntity.Add(new LazyLoadEntity
                {
                    LazyLoadProp = new NonLazyLoadEntity()
                    {
                        StringData = "Test",
                    },
                });

                await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
                Assert.True(await dbContext.LazyLoadEntity.AnyAsync(cancellationToken: TestContext.Current.CancellationToken));
            }

            await Parallel.ForAsync(0, 10000, async (i, ct) =>
            {
                const string cacheKey = "cacheKeyAddToCache";
                using var scope = serviceProvider.CreateScope();

                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var dbQueryCacheMetrics = scope.ServiceProvider.GetRequiredService<IDbQueryCacheMetrics>();
                var dbQueryCacheHelper = scope.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();

                bool cached = true;

                var result = await dbQueryCacheHelper.GetOrAddAsync(typeof(LazyLoadEntity), dbContext, () =>
                {
                    cached = false;
                    return dbContext.LazyLoadEntity.FirstAsync();
                }, cacheKey);

                Assert.False(cached, "freshly created scope has a dbcontext dependent cache");

                var lazyLoadProp = result.LazyLoadProp;
                Assert.NotNull(lazyLoadProp);
                Assert.NotNull(lazyLoadProp.StringData);
            });
        }
    }
}
