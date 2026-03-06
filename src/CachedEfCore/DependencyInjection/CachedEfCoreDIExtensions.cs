using CachedEfCore.Cache;
using CachedEfCore.Cache.Helper;
using CachedEfCore.Configuration;
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
            Action<IServiceProvider, CachedEfCoreOptionsBuilder> configure
        )
            where TSqlQueryEntityExtractor : class, ISqlQueryEntityExtractor
        {
            services.AddMemoryCache();

            services.TryAddSingleton<IPrintabilityChecker, PrintabilityChecker>();
            services.TryAddSingleton<IExpressionEvalTypeChecker, ExpressionEvalTypeCheckerVisitor>();

            services.TryAddSingleton<KeyGeneratorVisitor>();
            services.TryAddSingleton<IDbQueryCacheHelper, DbQueryCacheHelper>();
            services.TryAddSingleton<IDbQueryCacheStore, DbQueryCacheStore>();

            services.TryAddSingleton<ISqlQueryEntityExtractor, TSqlQueryEntityExtractor>();
            services.TryAddSingleton<DbStateInterceptor>();

            services.TryAddSingleton<ICachedEfCoreOptions>(sp =>
            {
                var options = new CachedEfCoreOptionsBuilder();
                configure.Invoke(sp, options);
                return options.Build();
            });

            services.TryAddSingleton<ITypeCompatibilityChecker>(sp =>
            {
                var options = sp.GetRequiredService<ICachedEfCoreOptions>();
                return new TypeCompatibilityChecker(options.NonEvaluableTypes);
            });
            services.TryAddSingleton<JsonSerializerOptions>(sp =>
            {
                var options = sp.GetRequiredService<ICachedEfCoreOptions>();
                return options.KeyGeneratorJsonSerializerOptions;
            });

            return services;
        }

        public static IServiceCollection AddCachedEfCore<TSqlQueryEntityExtractor>(
            this IServiceCollection services,
            Action<CachedEfCoreOptionsBuilder>? configure = null
        )
            where TSqlQueryEntityExtractor : class, ISqlQueryEntityExtractor
        {
            return services.AddCachedEfCore<TSqlQueryEntityExtractor>((serviceProvider, options) =>
            {
                configure?.Invoke(options);
            });
        }
    }
}