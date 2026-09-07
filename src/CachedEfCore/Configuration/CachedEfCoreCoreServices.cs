using CachedEfCore.Cache.Helper;
using CachedEfCore.Cache.KeyGeneration;
using CachedEfCore.Cache.KeyGeneration.ExpressionEvaluation;
using CachedEfCore.Cache.KeyGeneration.ExpressionEvaluation.EvalTypeChecker;
using CachedEfCore.Cache.KeyGeneration.ExpressionKeyGen;
using CachedEfCore.Cache.KeyGeneration.TypeCompatibility;
using CachedEfCore.Cache.Metrics;
using CachedEfCore.DependencyManager;
using CachedEfCore.EntityMapping;
using CachedEfCore.Interceptors;
using CachedEfCore.SqlAnalysis;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CachedEfCore.Configuration
{
    public static class CachedEfCoreCoreServices
    {
        public static IEnumerable<CachedEfCoreService> GetCoreServices()
        {
            yield return new CachedEfCoreService
            {
                ServiceDescriptor = ServiceDescriptor.Scoped<EntityDependency>(sp =>
                {
                    var dbContext = sp.GetRequiredService<ICurrentDbContext>().Context;

                    var entityDependency = EntityDependency.GetOrAdd(dbContext.Model);

                    return entityDependency;
                }),
                GetServiceProviderHashCode = null,
                ShouldUseSameServiceProvider = null,
                Options = null,
            };

            yield return new CachedEfCoreService
            {
                ServiceDescriptor = ServiceDescriptor.Scoped<TableEntityMapping>(sp =>
                {
                    var dbContext = sp.GetRequiredService<ICurrentDbContext>().Context;

                    var tableEntityMapping = TableEntityMapping.GetOrAdd(dbContext.Model);

                    return tableEntityMapping;
                }),
                GetServiceProviderHashCode = null,
                ShouldUseSameServiceProvider = null,
                Options = null,
            };

            yield return new CachedEfCoreService
            {
                ServiceDescriptor = ServiceDescriptor.Singleton<IPrintabilityChecker, PrintabilityChecker>(),
                GetServiceProviderHashCode = null,
                ShouldUseSameServiceProvider = null,
                Options = null,
            };

            yield return new CachedEfCoreService
            {
                ServiceDescriptor = ServiceDescriptor.Singleton<IExpressionEvalTypeChecker, ExpressionEvalTypeCheckerVisitor>(),
                GetServiceProviderHashCode = null,
                ShouldUseSameServiceProvider = null,
                Options = null,
            };

            yield return new CachedEfCoreService
            {
                ServiceDescriptor = ServiceDescriptor.Singleton<ICachedEfCoreEvalutableExpressionChecker, CachedEfCoreEvalutableExpressionChecker>(),
                GetServiceProviderHashCode = null,
                ShouldUseSameServiceProvider = null,
                Options = null,
            };

            {
                var defaultJsonSerializerOptions = CachedEfCoreKeyGenerationOptionsDefaults.DefaultJsonSerializerOptions;
                yield return new CachedEfCoreService
                {
                    ServiceDescriptor = ServiceDescriptor.Scoped<KeyGeneratorVisitor>(sp =>
                    {
                        var printabilityChecker = sp.GetRequiredService<IPrintabilityChecker>();
                        var model = sp.GetRequiredService<IModel>();
                        var cachedEfCoreEvalutableExpressionChecker = sp.GetRequiredService<ICachedEfCoreEvalutableExpressionChecker>();

                        return new KeyGeneratorVisitor(
                            printabilityChecker,
                            model,
                            cachedEfCoreEvalutableExpressionChecker,
                            defaultJsonSerializerOptions
                        );
                    }),
                    GetServiceProviderHashCode = thisService => ((JsonSerializerOptions)thisService.Options!).GetHashCode(),
                    ShouldUseSameServiceProvider = args => ((JsonSerializerOptions)args.ThisService.Options!).Equals(((JsonSerializerOptions)args.OtherServices.Single().Options!)),
                    Options = defaultJsonSerializerOptions,
                };
            }

            {
                var defaultNonEvaluableTypes = CachedEfCoreKeyGenerationOptionsDefaults.DefaultNonEvaluableTypes;
                yield return new CachedEfCoreService
                {
                    ServiceDescriptor = ServiceDescriptor.Singleton<ITypeCompatibilityChecker>(sp =>
                    {
                        return new TypeCompatibilityChecker(defaultNonEvaluableTypes);
                    }),
                    GetServiceProviderHashCode = thisService => ((List<Type>)thisService.Options!).GetHashCode(),
                    ShouldUseSameServiceProvider = args => ((List<Type>)args.ThisService.Options!).SequenceEqual(((List<Type>)args.OtherServices.Single().Options!)),
                    Options = defaultNonEvaluableTypes,
                };
            }

            yield return new CachedEfCoreService
            {
                ServiceDescriptor = ServiceDescriptor.Singleton<IDbQueryCacheHelper, DbQueryCacheHelper>(),
                GetServiceProviderHashCode = null,
                ShouldUseSameServiceProvider = null,
                Options = null,
            };

            yield return new CachedEfCoreService
            {
                ServiceDescriptor = ServiceDescriptor.Singleton<IDbQueryCacheMetrics>(sp =>
                {
                    return new DbQueryCacheWithGlobalMetrics(new DbQueryCacheMetrics());
                }),
                GetServiceProviderHashCode = null,
                ShouldUseSameServiceProvider = null,
                Options = null,
            };

            {
                var sqlQueryEntityExtractorType = typeof(GenericSqlQueryEntityExtractor);
                yield return new CachedEfCoreService
                {
                    ServiceDescriptor = ServiceDescriptor.Singleton(typeof(ISqlQueryEntityExtractor), sqlQueryEntityExtractorType),
                    GetServiceProviderHashCode = thisService => ((Type)thisService.Options!).GetHashCode(),
                    ShouldUseSameServiceProvider = args => ((Type)args.ThisService.Options!) == ((Type)args.OtherServices.Single().Options!),
                    Options = sqlQueryEntityExtractorType,
                };
            }

            yield return new CachedEfCoreService
            {
                ServiceDescriptor = ServiceDescriptor.Scoped<IInterceptor, DbStateInterceptor>(),
                GetServiceProviderHashCode = null,
                ShouldUseSameServiceProvider = null,
                Options = null,
            };
        }
    }
}
