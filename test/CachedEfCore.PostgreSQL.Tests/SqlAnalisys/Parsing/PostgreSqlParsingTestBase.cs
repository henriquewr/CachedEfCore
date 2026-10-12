using CachedEfCore.DependencyInjection;
using CachedEfCore.PostgreSQL.Configuration;
using CachedEfCore.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CachedEfCore.PostgreSQL.Tests.SqlAnalisys.Parsing
{
    public class PostgreSqlParsingTestBase
    {
        protected readonly ServiceProviderFixture _serviceProviderFixture;

        public PostgreSqlParsingTestBase(ServiceProviderFixture serviceProviderFixture)
        {
            _serviceProviderFixture = serviceProviderFixture;
        }

        protected virtual IServiceProvider CreateProvider()
           => _serviceProviderFixture.CreateProvider(services =>
           {
                services.AddDbContext<TestDbContext>((serviceProvider, options) =>
                {
                    options.UseNpgsql();

                    options.UseCachedEfCore(cachedEfCoreOptions =>
                    {
                        cachedEfCoreOptions.UsePostgreSql();
                    });
                });
           });

        public class TestDbContext : DbContext
        {
            public TestDbContext() : base()
            {
            }

            public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
            {
            }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
            }
        }
    }
}
