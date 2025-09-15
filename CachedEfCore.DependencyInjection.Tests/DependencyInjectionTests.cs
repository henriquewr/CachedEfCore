using CachedEfCore.Cache;
using CachedEfCore.Cache.Helper;
using CachedEfCore.Interceptors;
using CachedEfCore.KeyGeneration;
using CachedEfCore.KeyGeneration.EvalTypeChecker;
using CachedEfCore.KeyGeneration.ExpressionKeyGen;
using CachedEfCore.SqlAnalysis;
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

            services.AddCachedEfCore<SqlServerQueryEntityExtractor>();

            var builtServiceProvider = services.BuildServiceProvider();

            AssertCachedEfCoreIsRegistred(builtServiceProvider);
        }

        private static void AssertCachedEfCoreIsRegistred(IServiceProvider serviceProvider)
        {
            var printabilityChecker = serviceProvider.GetRequiredService<IPrintabilityChecker>();
            var expressionEvalTypeChecker = serviceProvider.GetRequiredService<IExpressionEvalTypeChecker>();
            var typeCompatibilityChecker = serviceProvider.GetRequiredService<ITypeCompatibilityChecker>();
            var keyGeneratorVisitor = serviceProvider.GetRequiredService<KeyGeneratorVisitor>();
            var dbQueryCacheHelper = serviceProvider.GetRequiredService<IDbQueryCacheHelper>();
            var dbQueryCacheStore = serviceProvider.GetRequiredService<IDbQueryCacheStore>();
            var sqlQueryEntityExtractor = serviceProvider.GetRequiredService<ISqlQueryEntityExtractor>();
            Assert.IsType<SqlServerQueryEntityExtractor>(sqlQueryEntityExtractor);

            var dbStateInterceptor = serviceProvider.GetRequiredService<DbStateInterceptor>();
        }
    }
}