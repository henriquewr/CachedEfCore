using CachedEfCore.Cache;
using CachedEfCore.Cache.Helper;
using CachedEfCore.Interceptors;
using CachedEfCore.KeyGeneration;
using CachedEfCore.KeyGeneration.ExpressionKeyGen;
using CachedEfCore.SqlAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CachedEfCore.DependencyInjection
{
    public static class CachedEfCoreDIExtensions
    {
        public static IServiceCollection AddCachedEfCore<TSqlQueryEntityExtractor>(this IServiceCollection serviceCollection) where TSqlQueryEntityExtractor : class, ISqlQueryEntityExtractor
        {
            serviceCollection.AddMemoryCache();
            serviceCollection.TryAddSingleton<IPrintabilityChecker, PrintabilityChecker>();
            serviceCollection.TryAddSingleton<KeyGeneratorVisitor>();
            serviceCollection.TryAddSingleton<IDbQueryCacheHelper, DbQueryCacheHelper>();
            serviceCollection.TryAddSingleton<IDbQueryCacheStore, DbQueryCacheStore>();
            serviceCollection.TryAddSingleton<ISqlQueryEntityExtractor, TSqlQueryEntityExtractor>();
            serviceCollection.TryAddSingleton<DbStateInterceptor>();

            return serviceCollection;
        }
    }
}