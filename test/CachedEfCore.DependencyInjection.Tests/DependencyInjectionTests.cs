using CachedEfCore.Cache;
using CachedEfCore.Cache.Helper;
using CachedEfCore.Cache.Metrics;
using CachedEfCore.Context;
using CachedEfCore.Interceptors;
using CachedEfCore.KeyGeneration;
using CachedEfCore.KeyGeneration.ExpressionEvaluation;
using CachedEfCore.KeyGeneration.ExpressionEvaluation.EvalTypeChecker;
using CachedEfCore.KeyGeneration.ExpressionKeyGen;
using CachedEfCore.KeyGeneration.TypeCompatibility;
using CachedEfCore.SqlAnalysis;
using CachedEfCore.SqlAnalysis.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;

namespace CachedEfCore.DependencyInjection.Tests
{
    public class DependencyInjectionTests
    {
        [Fact]
        public void DependencyInjection_Should_Register_CachedEfCore()
        {
            var services = new ServiceCollection();

            services.AddCachedEfCore();

            services.AddDbContext<TestDbContext>(options => 
            {
                options.UseSqlServer();

                options.UseCachedEfCore(cachedEfCoreOptions =>
                {
                    cachedEfCoreOptions.WithSqlQueryEntityExtractor<SqlServerQueryEntityExtractor>();
                });
            });

            var builtServiceProvider = services.BuildServiceProvider();

            AssertCachedEfCoreIsRegistred(builtServiceProvider);
        }

        private static void AssertCachedEfCoreIsRegistred(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var appDbQueryCacheMetrics = scope.ServiceProvider.GetRequiredService<IDbQueryCacheMetrics>();
            var dbQueryCacheHelper = scope.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

            var printabilityChecker = dbContext.GetService<IPrintabilityChecker>();
            var expressionEvalTypeChecker = dbContext.GetService<IExpressionEvalTypeChecker>();
            var typeCompatibilityChecker = dbContext.GetService<ITypeCompatibilityChecker>();
            var keyGeneratorVisitor = dbContext.GetService<KeyGeneratorVisitor>();
            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();
            var dbQueryCacheMetrics = dbContext.GetService<IDbQueryCacheMetrics>();
            var cachedEfCoreEvalutableExpressionChecker = dbContext.GetService<ICachedEfCoreEvalutableExpressionChecker>();
            var sqlQueryEntityExtractor = dbContext.GetService<ISqlQueryEntityExtractor>();
            Assert.IsType<SqlServerQueryEntityExtractor>(sqlQueryEntityExtractor);

            var dbStateInterceptor = dbContext.GetService<DbStateInterceptor>();
        }

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