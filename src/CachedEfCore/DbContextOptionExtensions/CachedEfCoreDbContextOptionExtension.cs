using CachedEfCore.Cache;
using CachedEfCore.Cache.Helper;
using CachedEfCore.Cache.Metrics;
using CachedEfCore.Configuration;
using CachedEfCore.DependencyManager;
using CachedEfCore.EntityMapping;
using CachedEfCore.Interceptors;
using CachedEfCore.KeyGeneration;
using CachedEfCore.KeyGeneration.ExpressionEvaluation;
using CachedEfCore.KeyGeneration.ExpressionEvaluation.EvalTypeChecker;
using CachedEfCore.KeyGeneration.ExpressionKeyGen;
using CachedEfCore.KeyGeneration.TypeCompatibility;
using CachedEfCore.SqlAnalysis;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CachedEfCore.DbContextOptionExtensions
{
    public class CachedEfCoreDbContextOptionExtension : IDbContextOptionsExtension
    {
        private readonly Type _contextType;
        private readonly ICachedEfCoreOptions _options;

        public DbContextOptionsExtensionInfo Info { get; }

        public CachedEfCoreDbContextOptionExtension(Type contextType, ICachedEfCoreOptions options)
        {
            _contextType = contextType;
            _options = options;
            Info = new ExtensionInfo(this);
        }

        public void ApplyServices(IServiceCollection services)
        {
            services.AddMemoryCache();

            services.TryAddScoped<EntityDependency>(sp =>
            {
                var dbContext = sp.GetRequiredService<ICurrentDbContext>().Context;

                var entityDependency = EntityDependency.GetOrAdd(dbContext.Model);

                return entityDependency;
            });

            services.TryAddScoped<TableEntityMapping>(sp =>
            {
                var dbContext = sp.GetRequiredService<ICurrentDbContext>().Context;

                var tableEntityMapping = TableEntityMapping.GetOrAdd(dbContext.Model);

                return tableEntityMapping;
            });

            services.TryAddSingleton<IPrintabilityChecker, PrintabilityChecker>();
            services.TryAddSingleton<IExpressionEvalTypeChecker, ExpressionEvalTypeCheckerVisitor>();
            services.TryAddSingleton<ICachedEfCoreEvalutableExpressionChecker, CachedEfCoreEvalutableExpressionChecker>();

            services.TryAddScoped<KeyGeneratorVisitor>(sp =>
            {
                var printabilityChecker = sp.GetRequiredService<IPrintabilityChecker>();
                var model = sp.GetRequiredService<IModel>();
                var cachedEfCoreEvalutableExpressionChecker = sp.GetRequiredService<ICachedEfCoreEvalutableExpressionChecker>();

                return new KeyGeneratorVisitor(
                    printabilityChecker,
                    model,
                    cachedEfCoreEvalutableExpressionChecker,
                    _options.KeyGeneratorJsonSerializerOptions
                );
            });
            services.TryAddSingleton<IDbQueryCacheHelper, DbQueryCacheHelper>();
            services.TryAddSingleton<IDbQueryCacheMetrics, DbQueryCacheMetrics>();
            services.TryAddSingleton<IDbQueryCacheStore, DbQueryCacheStore>();

            services.TryAddSingleton(typeof(ISqlQueryEntityExtractor), _options.SqlQueryEntityExtractorType);
            services.TryAddSingleton<DbStateInterceptor>();

            services.TryAddSingleton<ITypeCompatibilityChecker>(sp =>
            {
                return new TypeCompatibilityChecker(_options.NonEvaluableTypes);
            });
        }

        public IDbContextOptionsExtension ApplyDefaults(IDbContextOptions options)
        {
            return this;
        }

        public void Validate(IDbContextOptions options)
        {
        }

        private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
        {
            public ExtensionInfo(CachedEfCoreDbContextOptionExtension extension) : base(extension)
            {
            }

            public override bool IsDatabaseProvider => false;

            public override string LogFragment => " CachedEfCore ";

            public override int GetServiceProviderHashCode()
            {
                var extension = (CachedEfCoreDbContextOptionExtension)this.Extension;

                return HashCode.Combine(extension._contextType, extension._options.NonEvaluableTypes, extension._options.SqlQueryEntityExtractorType);
            }

            public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            {
                debugInfo["CachedEfCore:Context"] = ((CachedEfCoreDbContextOptionExtension)Extension)._contextType.FullName!;
            }

            public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            {
                if (other.Extension is not CachedEfCoreDbContextOptionExtension cachedEfCoreDbContextOptionExtension)
                {
                    return false;
                }

                var extension = (CachedEfCoreDbContextOptionExtension)this.Extension;

                return cachedEfCoreDbContextOptionExtension._contextType == extension._contextType
                    && cachedEfCoreDbContextOptionExtension._options.NonEvaluableTypes.SequenceEqual(extension._options.NonEvaluableTypes)
                    && cachedEfCoreDbContextOptionExtension._options.SqlQueryEntityExtractorType == extension._options.SqlQueryEntityExtractorType;
            }
        }
    }
}
