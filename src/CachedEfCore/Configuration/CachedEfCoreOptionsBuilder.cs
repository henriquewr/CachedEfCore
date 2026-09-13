using CachedEfCore.DbContextOptionExtensions;
using CachedEfCore.SqlAnalysis;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace CachedEfCore.Configuration
{
    public class CachedEfCoreOptionsBuilder
    {
        public CachedEfCoreDbContextOptionExtension CachedEfCoreExtension { get; }

        public CachedEfCoreOptionsBuilder(CachedEfCoreDbContextOptionExtension cachedEfCoreExtension)
        {
            CachedEfCoreExtension = cachedEfCoreExtension;
        }

        public virtual CachedEfCoreOptionsBuilder ConfigureKeyGeneration(Action<CachedEfCoreKeyGenerationOptionsBuilder> configure)
        {
            var builder = new CachedEfCoreKeyGenerationOptionsBuilder(CachedEfCoreExtension);

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
            CachedEfCoreExtension.AddOrReplaceService(new CachedEfCoreService
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