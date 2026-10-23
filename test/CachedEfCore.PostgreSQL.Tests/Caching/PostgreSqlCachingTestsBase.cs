using CachedEfCore.Caching.InMemory.Configuration;
using CachedEfCore.DependencyInjection;
using CachedEfCore.PostgreSQL.Configuration;
using CachedEfCore.Tests.Common.Fixtures;
using CachedEfCore.Tests.Common.TestContainers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Xunit;

namespace CachedEfCore.PostgreSQL.Tests.Caching
{
    public class PostgreSqlCachingTestsBase : IClassFixture<PostgreSqlTestContainer>, IClassFixture<ServiceProviderFixture>, IAsyncLifetime
    {
        protected readonly PostgreSqlTestContainer _postgreSqlTestContainer;
        protected readonly ServiceProviderFixture _serviceProviderFixture;
        protected IServiceProvider ServiceProvider { get; private set; } = null!;

        public PostgreSqlCachingTestsBase(PostgreSqlTestContainer postgreSqlTestContainer,
            ServiceProviderFixture serviceProviderFixture)
        {
            _postgreSqlTestContainer = postgreSqlTestContainer;
            _serviceProviderFixture = serviceProviderFixture;
        }

        public virtual async ValueTask DisposeAsync()
        {
            await _postgreSqlTestContainer.DisposeAsync();
        }

        public virtual async ValueTask InitializeAsync()
        {
            await _postgreSqlTestContainer.InitializeAsync();

            ServiceProvider = CreateProvider(_postgreSqlTestContainer.ConnectionString);

            using var scope = ServiceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

            await dbContext.Database.EnsureCreatedAsync();
        }

        protected virtual IServiceProvider CreateProvider(string connectionString)
        {
            var provider = _serviceProviderFixture.CreateProvider(services =>
            {
                services.AddCachedEfCore();

                services.AddDbContext<TestDbContext>(options =>
                {
                    options.UseNpgsql(connectionString);

                    options.UseCachedEfCore(cachedEfCoreOptions =>
                    {
                        cachedEfCoreOptions.UsePostgreSql();
                        cachedEfCoreOptions.UseInMemoryCacheStore();
                    });
                });
            });

            return provider;
        }

        public class TestDbContext : DbContext
        {
            public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
            {
                
            }

            public DbSet<TestEntity> TestEntities => Set<TestEntity>();
        }

        public class TestEntity
        { 
            public int Id { get; set; }
        }
    }
}
