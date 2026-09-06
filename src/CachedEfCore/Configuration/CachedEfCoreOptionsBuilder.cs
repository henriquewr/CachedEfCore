using CachedEfCore.DbContextOptionExtensions;
using CachedEfCore.SqlAnalysis;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace CachedEfCore.Configuration
{
    public class CachedEfCoreOptionsBuilder
    {
        private readonly CachedEfCoreDbContextOptionExtension _cachedEfCoreExtension;

        public CachedEfCoreOptionsBuilder(CachedEfCoreDbContextOptionExtension cachedEfCoreExtension)
        {
            _cachedEfCoreExtension = cachedEfCoreExtension;
        }

        public virtual CachedEfCoreOptionsBuilder ConfigureKeyGeneration(Action<CachedEfCoreKeyGenerationOptionsBuilder> configure)
        {
            var builder = new CachedEfCoreKeyGenerationOptionsBuilder(_cachedEfCoreExtension);

            configure(builder);

            return this;
        }

        public CachedEfCoreOptionsBuilder UseGenericProvider()
            => WithSqlQueryEntityExtractor<GenericSqlQueryEntityExtractor>();

        public virtual CachedEfCoreOptionsBuilder WithSqlQueryEntityExtractor<TSqlQueryEntityExtractor>() 
            where TSqlQueryEntityExtractor : class, ISqlQueryEntityExtractor
            => WithSqlQueryEntityExtractor(typeof(TSqlQueryEntityExtractor));

        public virtual CachedEfCoreOptionsBuilder WithSqlQueryEntityExtractor(Type sqlQueryEntityExtractorType)
        {
            _cachedEfCoreExtension.AddOrReplaceService(new CachedEfCoreService
            {
                ServiceDescriptor = ServiceDescriptor.Singleton(typeof(ISqlQueryEntityExtractor), sqlQueryEntityExtractorType),
                GetServiceProviderHashCode = thisService => ((Type)thisService.Options!).GetHashCode(),
                ShouldUseSameServiceProvider = args => ((Type)args.ThisService.Options!) == ((Type)args.OtherServices.Single().Options!),
                Options = sqlQueryEntityExtractorType,
            });

            return this;
        }
    }
}