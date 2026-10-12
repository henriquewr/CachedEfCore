using CachedEfCore.Cache.Store;
using CachedEfCore.Tests.Common.Fixtures;
using CachedEfCore.Tests.Common.TestContainers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Xunit;

namespace CachedEfCore.PostgreSQL.Tests.Caching
{
    public class PostgreSqlCachingInvalidationTests : PostgreSqlCachingTestsBase
    {
        public PostgreSqlCachingInvalidationTests(PostgreSqlTestContainer postgreSqlTestContainer, ServiceProviderFixture serviceProviderFixture) : base(postgreSqlTestContainer, serviceProviderFixture)
        {
        }

        public record class CacheKey : IDbQueryCacheKey
        {
            public required string Key { get; set; }
            public DbContextId? DependentDbContext { get; set; }

            public string Stringify()
            {
                return $"{Key}:{DependentDbContext}";
            }
        }

        [Fact]
        public async Task SaveChanges_With_Related_Entity_Should_Invalidate_Cached()
        {
            using var scope = ServiceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

            var cacheStore = dbContext.GetService<IDbQueryCacheStore>();

            var cacheKey = new CacheKey
            {
                Key = "Test" 
            };

            var valueToCache = "ValueToCache";

            cacheStore.AddToCache(typeof(TestEntity), cacheKey, valueToCache);

            var cachedValue = cacheStore.GetCached<string>(cacheKey);

            Assert.Same(valueToCache, cachedValue);

            dbContext.TestEntities.Add(new TestEntity());

            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            var afterSaveChangesCachedValue = cacheStore.GetCached<string>(cacheKey);

            Assert.Null(afterSaveChangesCachedValue);
        }

        [Fact]
        public async Task ExecuteDelete_With_Related_Entity_Should_Invalidate_Cached()
        {
            using var scope = ServiceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

            var cacheStore = dbContext.GetService<IDbQueryCacheStore>();

            var cacheKey = new CacheKey
            {
                Key = "Test"
            };

            var valueToCache = "ValueToCache";

            cacheStore.AddToCache(typeof(TestEntity), cacheKey, valueToCache);

            var cachedValue = cacheStore.GetCached<string>(cacheKey);

            Assert.Same(valueToCache, cachedValue);

            await dbContext.TestEntities.ExecuteDeleteAsync(TestContext.Current.CancellationToken);

            var afterSaveChangesCachedValue = cacheStore.GetCached<string>(cacheKey);

            Assert.Null(afterSaveChangesCachedValue);
        }
    }
}
