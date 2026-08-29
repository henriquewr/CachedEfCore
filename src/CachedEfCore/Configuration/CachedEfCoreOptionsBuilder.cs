using CachedEfCore.SqlAnalysis;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CachedEfCore.Configuration
{
    public partial class CachedEfCoreOptionsBuilder
    {
        protected CachedEfCoreOptions CachedEfCoreOptions { get; set; } = CachedEfCoreOptions.CreateDefault();

        public virtual CachedEfCoreOptionsBuilder ConfigureNonEvaluableTypes(Action<List<Type>> configure)
        {
            configure(CachedEfCoreOptions.NonEvaluableTypes);

            return this;
        }

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

        public virtual CachedEfCoreOptionsBuilder ConfigureKeyGeneratorJsonSerializer(Func<JsonSerializerOptions, JsonSerializerOptions> configure)
        {
            CachedEfCoreOptions.KeyGeneratorJsonSerializerOptions = configure(CachedEfCoreOptions.KeyGeneratorJsonSerializerOptions);

            return this;
        }

        public ICachedEfCoreOptions Build()
        {
            return CachedEfCoreOptions;
        }
    }
}