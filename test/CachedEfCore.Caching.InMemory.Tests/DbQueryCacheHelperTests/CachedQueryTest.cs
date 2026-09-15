using CachedEfCore.Cache.Metrics;
using CachedEfCore.Cache.Query;
using CachedEfCore.Cache.Store;
using CachedEfCore.Caching.InMemory.Configuration;
using CachedEfCore.Caching.InMemory.Store;
using CachedEfCore.Caching.InMemory.Tests.Common;
using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlServer.Configuration;
using CachedEfCore.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CachedEfCore.Caching.InMemory.Tests.DbQueryCacheHelperTests
{
    [Collection(CacheTestCollection.Name)]
    public class CachedQueryTest : IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;

        public CachedQueryTest(ServiceProviderFixture serviceProviderFixture)
        {
            _serviceProviderFixture = serviceProviderFixture;
        }

        protected virtual IServiceProvider CreateProvider()
        {
            var databaseName = Guid.NewGuid().ToString();

            return _serviceProviderFixture.CreateProvider(services =>
            {
                services.AddCachedEfCore();

                services.AddDbContextPool<TestDbContext>((serviceProvider, options) =>
                {
                    options.UseLazyLoadingProxies();
                    options.UseInMemoryDatabase(databaseName);
                    options.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

                    options.UseCachedEfCore(cachedEfCoreOptions =>
                    {
                        cachedEfCoreOptions.UseInMemoryCacheStore();
                        cachedEfCoreOptions.UseSqlServer();
                    });
                });
            });
        }

        [Fact]
        public void GetOrAdd_Gets_From_Cache_By_Typed_Parameter()
        {
            var serviceProvider = CreateProvider();
            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            Seed(dbContext);

            var query = CachedQuery.For<NonLazyLoadEntity>().Compile(
                (TestDbContext context, int id) => context.NonLazyLoadEntity
                    .AsNoTracking()
                    .Where(entity => entity.Id == id)
                    .Select(entity => new QueryResult(entity.Id, entity.StringData))
                    .SingleOrDefault()
            );

            var metrics = dbContext.GetService<IDbQueryCacheMetrics>();
            metrics.Reset();

            var first = query.GetOrAdd(dbContext, 1);
            var cached = query.GetOrAdd(dbContext, 1);
            var otherParameter = query.GetOrAdd(dbContext, 2);

            Assert.Same(first, cached);
            Assert.NotSame(first, otherParameter);
            Assert.Equal(1, first!.Id);
            Assert.Equal(2, otherParameter!.Id);
            Assert.Equal(1U, metrics.GetCacheMetrics().Hits);
            Assert.Equal(2U, metrics.GetCacheMetrics().Misses);
        }

        [Fact]
        public void GetOrAdd_Separates_Compiled_Query_Identities()
        {
            var serviceProvider = CreateProvider();
            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            Seed(dbContext);

            var queryById = CachedQuery.For<NonLazyLoadEntity>().Compile(
                (TestDbContext context, int id) => context.NonLazyLoadEntity
                    .AsNoTracking()
                    .Where(entity => entity.Id == id)
                    .Select(entity => new QueryResult(entity.Id, entity.StringData))
                    .SingleOrDefault()
            );
            var queryByOtherId = CachedQuery.For<NonLazyLoadEntity>().Compile(
                (TestDbContext context, int id) => context.NonLazyLoadEntity
                    .AsNoTracking()
                    .Where(entity => entity.Id != id)
                    .Select(entity => new QueryResult(entity.Id, entity.StringData))
                    .SingleOrDefault()
            );

            var first = queryById.GetOrAdd(dbContext, 1);
            var second = queryByOtherId.GetOrAdd(dbContext, 1);

            Assert.Equal(1, first!.Id);
            Assert.Equal(2, second!.Id);
        }

        [Fact]
        public void GetOrAdd_Separates_Cache_Partitions()
        {
            var serviceProvider = CreateProvider();
            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            Seed(dbContext);

            var query = CachedQuery.For<NonLazyLoadEntity>().Compile(
                (TestDbContext context, int id) => context.NonLazyLoadEntity
                    .AsNoTracking()
                    .Where(entity => entity.Id == id)
                    .Select(entity => new QueryResult(entity.Id, entity.StringData))
                    .SingleOrDefault()
            );

            var firstPartition = query.GetOrAdd(dbContext, 1, "tenant-1");
            var secondPartition = query.GetOrAdd(dbContext, 1, "tenant-2");
            var cached = query.GetOrAdd(dbContext, 1, "tenant-1");

            Assert.NotSame(firstPartition, secondPartition);
            Assert.Same(firstPartition, cached);
        }

        [Fact]
        public void GetOrAdd_Shares_Cache_Between_DbContext_Scopes()
        {
            var serviceProvider = CreateProvider();

            using var firstScope = serviceProvider.CreateScope();
            using var secondScope = serviceProvider.CreateScope();

            var firstDbContext = firstScope.ServiceProvider.GetRequiredService<TestDbContext>();
            var secondDbContext = secondScope.ServiceProvider.GetRequiredService<TestDbContext>();
            Seed(firstDbContext);

            var query = CachedQuery.For<NonLazyLoadEntity>().Compile(
                (TestDbContext context, int id) => context.NonLazyLoadEntity
                    .AsNoTracking()
                    .Where(entity => entity.Id == id)
                    .Select(entity => new QueryResult(entity.Id, entity.StringData))
                    .SingleOrDefault()
            );

            var first = query.GetOrAdd(firstDbContext, 1);
            var cached = query.GetOrAdd(secondDbContext, 1);

            Assert.NotEqual(firstDbContext.ContextId.InstanceId, secondDbContext.ContextId.InstanceId);
            Assert.Same(first, cached);
        }

        [Fact]
        public void GetOrAdd_Supports_Null_Typed_Parameter()
        {
            var serviceProvider = CreateProvider();
            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            Seed(dbContext);

            var query = CachedQuery.For<NonLazyLoadEntity>().Compile(
                (TestDbContext context, string? value) => context.NonLazyLoadEntity
                    .AsNoTracking()
                    .Where(entity => entity.StringData == value)
                    .Select(entity => new QueryResult(entity.Id, entity.StringData))
                    .SingleOrDefault()
            );

            var first = query.GetOrAdd(dbContext, null);
            var cached = query.GetOrAdd(dbContext, null);

            Assert.Same(first, cached);
            Assert.Equal(2, first!.Id);
        }

        [Fact]
        public async Task GetOrAddAsync_Gets_From_Cache_By_Typed_Parameter()
        {
            var serviceProvider = CreateProvider();
            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            Seed(dbContext);

            var query = CachedQuery.For<NonLazyLoadEntity>().CompileAsync(
                (TestDbContext context, int id, CancellationToken cancellationToken) => context.NonLazyLoadEntity
                    .AsNoTracking()
                    .Where(entity => entity.Id == id)
                    .Select(entity => new QueryResult(entity.Id, entity.StringData))
                    .SingleOrDefault()
            );

            var metrics = dbContext.GetService<IDbQueryCacheMetrics>();
            metrics.Reset();

            var first = await query.GetOrAddAsync(dbContext, 1, TestContext.Current.CancellationToken);
            var cached = await query.GetOrAddAsync(dbContext, 1, TestContext.Current.CancellationToken);

            Assert.Same(first, cached);
            Assert.Equal(1U, metrics.GetCacheMetrics().Hits);
            Assert.Equal(1U, metrics.GetCacheMetrics().Misses);

            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await query.GetOrAddAsync(dbContext, 1, cancellationTokenSource.Token)
            );
        }

        [Fact]
        public void GetOrAdd_Registers_Entity_Invalidation()
        {
            var serviceProvider = CreateProvider();
            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            Seed(dbContext);

            var query = CachedQuery.For<NonLazyLoadEntity>().Compile(
                (TestDbContext context, int id) => context.NonLazyLoadEntity
                    .AsNoTracking()
                    .Where(entity => entity.Id == id)
                    .Select(entity => new QueryResult(entity.Id, entity.StringData))
                    .SingleOrDefault()
            );

            var first = query.GetOrAdd(dbContext, 1);

            var entity = dbContext.NonLazyLoadEntity.Single(item => item.Id == 1);
            entity.StringData = "updated";
            dbContext.SaveChanges();

            var entityType = dbContext.Model.FindEntityType(typeof(NonLazyLoadEntity))!;
            var cacheStore = dbContext.GetService<IDbQueryCacheStore>();
            cacheStore.RemoveDependentEntities(new HashSet<IEntityType> { entityType });

            var updated = query.GetOrAdd(dbContext, 1);

            Assert.NotSame(first, updated);
            Assert.Equal("updated", updated!.Value);
        }

        [Fact]
        public void GetOrAdd_Registers_DbContext_Dependent_Result()
        {
            var serviceProvider = CreateProvider();
            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            dbContext.LazyLoadEntity.Add(new LazyLoadEntity { Id = 1 });
            dbContext.SaveChanges();

            var query = CachedQuery.For<LazyLoadEntity>().Compile(
                (TestDbContext context, int id) => context.LazyLoadEntity.Single(entity => entity.Id == id)
            );

            var internalStore = (DbQueryCacheInMemoryInternalStore)dbContext.GetService<IDbQueryCacheInMemoryInternalStore>();
            internalStore._dbContextDependentKeys.Clear();

            query.GetOrAdd(dbContext, 1);

            Assert.Single(internalStore._dbContextDependentKeys);
        }

        [Fact]
        public void GetOrAdd_Does_Not_Share_DbContext_Dependent_Result_Between_Pooled_Leases()
        {
            var serviceProvider = CreateProvider();
            var query = CachedQuery.For<LazyLoadEntity>().Compile(
                (TestDbContext context, int id) => context.LazyLoadEntity.Single(entity => entity.Id == id)
            );

            LazyLoadEntity first;
            DbContextId firstContextId;
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                dbContext.LazyLoadEntity.Add(new LazyLoadEntity { Id = 1 });
                dbContext.SaveChanges();

                firstContextId = dbContext.ContextId;
                first = query.GetOrAdd(dbContext, 1);
            }

            using var nextScope = serviceProvider.CreateScope();
            var nextDbContext = nextScope.ServiceProvider.GetRequiredService<TestDbContext>();

            var second = query.GetOrAdd(nextDbContext, 1);

            Assert.Equal(firstContextId.InstanceId, nextDbContext.ContextId.InstanceId);
            Assert.NotEqual(firstContextId.Lease, nextDbContext.ContextId.Lease);
            Assert.NotSame(first, second);
        }

        private static void Seed(TestDbContext dbContext)
        {
            dbContext.NonLazyLoadEntity.AddRange(
                new NonLazyLoadEntity { Id = 1, StringData = "first" },
                new NonLazyLoadEntity { Id = 2, StringData = null }
            );
            dbContext.SaveChanges();
        }

        private sealed record QueryResult(int Id, string? Value);
    }
}
