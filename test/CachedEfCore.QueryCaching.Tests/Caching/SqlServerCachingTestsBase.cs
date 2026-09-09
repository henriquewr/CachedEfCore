using CachedEfCore.Caching.InMemory.Configuration;
using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlServer.Configuration;
using CachedEfCore.SqlServer.Tests.Testing;
using CachedEfCore.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Xunit;

namespace CachedEfCore.SqlServer.Tests.Caching
{
    public class SqlServerCachingTestsBase : IClassFixture<SqlServerTestContainer>, IClassFixture<ServiceProviderFixture>, IAsyncLifetime
    {
        protected readonly SqlServerTestContainer _sqlServerTestContainer;
        protected readonly ServiceProviderFixture _serviceProviderFixture;
        protected IServiceProvider ServiceProvider { get; private set; } = null!;

        public SqlServerCachingTestsBase(SqlServerTestContainer sqlServerTestContainer,
            ServiceProviderFixture serviceProviderFixture)
        {
            _sqlServerTestContainer = sqlServerTestContainer;
            _serviceProviderFixture = serviceProviderFixture;
        }

        public virtual async ValueTask DisposeAsync()
        {
            await _sqlServerTestContainer.DisposeAsync();
        }

        public virtual async ValueTask InitializeAsync()
        {
            await _sqlServerTestContainer.InitializeAsync();

            ServiceProvider = CreateProvider(_sqlServerTestContainer.ConnectionString);

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
                    options.UseSqlServer(connectionString);

                    options.UseCachedEfCore(cachedEfCoreOptions =>
                    {
                        cachedEfCoreOptions.UseSqlServer();
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
