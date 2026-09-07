using CachedEfCore.Cache.Helper;
using CachedEfCore.Cache.KeyGeneration;
using CachedEfCore.Cache.KeyGeneration.ExpressionEvaluation;
using CachedEfCore.Cache.KeyGeneration.ExpressionEvaluation.EvalTypeChecker;
using CachedEfCore.Cache.KeyGeneration.ExpressionKeyGen;
using CachedEfCore.Cache.KeyGeneration.TypeCompatibility;
using CachedEfCore.Cache.Metrics;
using CachedEfCore.Cache.Store;
using CachedEfCore.Caching.InMemory.Configuration;
using CachedEfCore.Caching.InMemory.Store;
using CachedEfCore.Interceptors;
using CachedEfCore.SqlAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
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
                    cachedEfCoreOptions.UseInMemoryCacheStore();

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

                    cachedEfCoreOptions.UseGenericProvider();
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
            var dbQueryCacheInternalStore = dbContext.GetService<IDbQueryCacheInMemoryInternalStore>();
            var dbQueryCacheMetrics = dbContext.GetService<IDbQueryCacheMetrics>();
            var cachedEfCoreEvalutableExpressionChecker = dbContext.GetService<ICachedEfCoreEvalutableExpressionChecker>();
            var sqlQueryEntityExtractor = dbContext.GetService<ISqlQueryEntityExtractor>();
        }

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