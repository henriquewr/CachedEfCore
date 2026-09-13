using CachedEfCore.Cache.Store;
using CachedEfCore.SqlServer.Tests.Testing;
using CachedEfCore.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Xunit;

namespace CachedEfCore.SqlServer.Tests.Caching
{
    public class SqlServerCachingInvalidationTests : SqlServerCachingTestsBase
    {
        public SqlServerCachingInvalidationTests(SqlServerTestContainer sqlServerTestContainer, ServiceProviderFixture serviceProviderFixture) : base(sqlServerTestContainer, serviceProviderFixture)
        {
        }

        public record class CacheKey : IDbQueryCacheKey
        {
            public required string Key { get; set; }
            public Guid? DependentDbContext { get; set; }
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
