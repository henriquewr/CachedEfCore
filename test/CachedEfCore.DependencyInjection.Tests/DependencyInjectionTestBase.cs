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
using CachedEfCore.SqlServer.SqlAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace CachedEfCore.DependencyInjection.Tests
{
    public class DependencyInjectionTestBase
    {
        [Fact]
        public void Generic_DependencyInjection_Should_Register_CachedEfCore()
        {
            var services = new ServiceCollection();

            services.AddCachedEfCore();

            services.AddDbContext<TestDbContext>(options => 
            {
                options.UseSqlServer();

                options.UseCachedEfCore(cachedEfCoreOptions =>
                {
                    cachedEfCoreOptions.ConfigureKeyGeneration(keyGen =>
                    {
                        keyGen.ConfigureNonEvaluableTypes(originals =>
                        {
                            var cloned = originals.ToList();
                            cloned.Add(typeof(object));

                            return cloned;
                        });

                        keyGen.ConfigureJsonSerializer(original =>
                        {
                            var newOptions = new JsonSerializerOptions();
                            return newOptions;
                        });
                    });

                    cachedEfCoreOptions.WithSqlQueryEntityExtractor<GenericSqlQueryEntityExtractor>();
                });
            });

            var builtServiceProvider = services.BuildServiceProvider();

            using var scope = builtServiceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var sqlQueryEntityExtractor = dbContext.GetService<ISqlQueryEntityExtractor>();

            Assert.IsType<GenericSqlQueryEntityExtractor>(sqlQueryEntityExtractor);

            AssertCachedEfCoreIsRegistred(scope);
        }

        protected virtual void AssertCachedEfCoreIsRegistred(IServiceScope scope)
        {
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