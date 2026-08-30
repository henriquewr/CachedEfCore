using CachedEfCore.SqlAnalysis;
using System;

namespace CachedEfCore.Configuration
{
    public class CachedEfCoreOptions
    {
        public required Type SqlQueryEntityExtractorType { get; set; }
        public required CachedEfCoreKeyGenerationOptions KeyGenerationOptions { get; set; }
    }

    public static class CachedEfCoreOptionsExtensions
    {
        extension(CachedEfCoreOptions options)
        {
            public static CachedEfCoreOptions CreateDefault()
            {
                return new CachedEfCoreOptions
                {
                    SqlQueryEntityExtractorType = typeof(GenericSqlQueryEntityExtractor),
                    KeyGenerationOptions = CachedEfCoreKeyGenerationOptions.CreateDefault(),
                };
            }
        }
    }
}
