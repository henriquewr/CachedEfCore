using CachedEfCore.Cache;
using CachedEfCore.Cache.Helper;
using CachedEfCore.Interceptors;
using CachedEfCore.KeyGeneration;
using CachedEfCore.KeyGeneration.EvalTypeChecker;
using CachedEfCore.KeyGeneration.ExpressionKeyGen;
using CachedEfCore.SqlAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Text.Json;

namespace CachedEfCore.DependencyInjection
{
    public static class CachedEfCoreDIExtensions
    {
        public static IServiceCollection AddCachedEfCore<TSqlQueryEntityExtractor>(
            this IServiceCollection services,
            Action<CachedEfCoreOptions>? configure = null
        )
            where TSqlQueryEntityExtractor : class, ISqlQueryEntityExtractor
        {
            services.AddMemoryCache();

            services.TryAddSingleton<IPrintabilityChecker, PrintabilityChecker>();
            services.TryAddSingleton<IExpressionEvalTypeChecker, ExpressionEvalTypeCheckerVisitor>();

            var cachedEfCoreOptions = CachedEfCoreOptions.CreateDefault();
            configure?.Invoke(cachedEfCoreOptions);

            services.TryAddSingleton<ITypeCompatibilityChecker>(serviceProvider => new TypeCompatibilityChecker(cachedEfCoreOptions.NonEvaluableTypes));

            services.TryAddSingleton<JsonSerializerOptions>(serviceProvider => cachedEfCoreOptions.KeyGeneratorJsonSerializerOptions);

            services.TryAddSingleton<KeyGeneratorVisitor>();
            services.TryAddSingleton<IDbQueryCacheHelper, DbQueryCacheHelper>();
            services.TryAddSingleton<IDbQueryCacheStore, DbQueryCacheStore>();

            services.TryAddSingleton<ISqlQueryEntityExtractor, TSqlQueryEntityExtractor>();
            services.TryAddSingleton<DbStateInterceptor>();

            return services;
        }

        public static IServiceCollection AddCachedEfCore<TSqlQueryEntityExtractor>(
            this IServiceCollection services,
            CachedEfCoreOptions cachedEfCoreOptions
        )
            where TSqlQueryEntityExtractor : class, ISqlQueryEntityExtractor
        {
            services.AddMemoryCache();

            services.TryAddSingleton<IPrintabilityChecker, PrintabilityChecker>();
            services.TryAddSingleton<IExpressionEvalTypeChecker, ExpressionEvalTypeCheckerVisitor>();

            services.TryAddSingleton<ITypeCompatibilityChecker>(serviceProvider => new TypeCompatibilityChecker(cachedEfCoreOptions.NonEvaluableTypes));

            services.TryAddSingleton<JsonSerializerOptions>(serviceProvider => cachedEfCoreOptions.KeyGeneratorJsonSerializerOptions);

            services.TryAddSingleton<KeyGeneratorVisitor>();
            services.TryAddSingleton<IDbQueryCacheHelper, DbQueryCacheHelper>();
            services.TryAddSingleton<IDbQueryCacheStore, DbQueryCacheStore>();

            services.TryAddSingleton<ISqlQueryEntityExtractor, TSqlQueryEntityExtractor>();
            services.TryAddSingleton<DbStateInterceptor>();

            return services;
        }

        public static IServiceCollection AddCachedEfCore<TSqlQueryEntityExtractor>(
            this IServiceCollection services
        )
            where TSqlQueryEntityExtractor : class, ISqlQueryEntityExtractor
        {
            services.AddMemoryCache();

            services.TryAddSingleton<IPrintabilityChecker, PrintabilityChecker>();
            services.TryAddSingleton<IExpressionEvalTypeChecker, ExpressionEvalTypeCheckerVisitor>();

            var cachedEfCoreOptions = CachedEfCoreOptions.CreateDefault();

            services.TryAddSingleton<ITypeCompatibilityChecker>(serviceProvider => new TypeCompatibilityChecker(cachedEfCoreOptions.NonEvaluableTypes));

            services.TryAddSingleton<JsonSerializerOptions>(serviceProvider => cachedEfCoreOptions.KeyGeneratorJsonSerializerOptions);

            services.TryAddSingleton<KeyGeneratorVisitor>();
            services.TryAddSingleton<IDbQueryCacheHelper, DbQueryCacheHelper>();
            services.TryAddSingleton<IDbQueryCacheStore, DbQueryCacheStore>();

            services.TryAddSingleton<ISqlQueryEntityExtractor, TSqlQueryEntityExtractor>();
            services.TryAddSingleton<DbStateInterceptor>();

            return services;
        }
    }
}