using CachedEfCore.Context;
using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlAnalysis.SqlServer;
using CachedEfCore.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CachedEfCore.SqlAnalisys.Tests.Parsing
{
    public class SqlServerParsingTestBase
    {
        protected readonly ServiceProviderFixture _serviceProviderFixture;

        public SqlServerParsingTestBase(ServiceProviderFixture serviceProviderFixture)
        {
            _serviceProviderFixture = serviceProviderFixture;
        }

        protected virtual IServiceProvider CreateProvider()
           => _serviceProviderFixture.CreateProvider(services =>
           {
                services.AddDbContext<TestDbContext>((serviceProvider, options) =>
                {
                    options.UseSqlServer();

                    options.UseCachedEfCore(cachedEfCoreOptions =>
                    {
                        cachedEfCoreOptions.WithSqlQueryEntityExtractor<SqlServerQueryEntityExtractor>();
                    });
                });
           });

        public class TestDbContext : CachedDbContext
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
