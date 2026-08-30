using CachedEfCore.SqlAnalysis;
using System;

namespace CachedEfCore.Configuration
{
    public class CachedEfCoreOptionsBuilder
    {
        protected CachedEfCoreOptions CachedEfCoreOptions { get; }

        public CachedEfCoreOptionsBuilder(CachedEfCoreOptions cachedEfCoreOptions)
        {
            CachedEfCoreOptions = cachedEfCoreOptions;
        }

        public CachedEfCoreOptionsBuilder() : this(CachedEfCoreOptions.CreateDefault())
        {
        }

        public virtual CachedEfCoreOptionsBuilder ConfigureKeyGeneration(Action<CachedEfCoreKeyGenerationOptionsBuilder> configure)
        {
            var builder = new CachedEfCoreKeyGenerationOptionsBuilder(CachedEfCoreOptions.KeyGenerationOptions);

            configure(builder);

            return this;
        }

        public CachedEfCoreOptionsBuilder UseGenericProvider()
            => WithSqlQueryEntityExtractor<GenericSqlQueryEntityExtractor>();

        public virtual CachedEfCoreOptionsBuilder WithSqlQueryEntityExtractor<TSqlQueryEntityExtractor>() 
            where TSqlQueryEntityExtractor : ISqlQueryEntityExtractor
        {
            CachedEfCoreOptions.SqlQueryEntityExtractorType = typeof(TSqlQueryEntityExtractor);

            return this;
        }

        public virtual CachedEfCoreOptionsBuilder WithSqlQueryEntityExtractor(Type sqlQueryEntityExtractorType) 
        {
            CachedEfCoreOptions.SqlQueryEntityExtractorType = sqlQueryEntityExtractorType;

            return this;
        }

        public CachedEfCoreOptions Build()
        {
            return CachedEfCoreOptions;
        }
    }
}