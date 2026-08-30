using CachedEfCore.Configuration;
using CachedEfCore.SqlServer.SqlAnalysis;

namespace CachedEfCore.SqlServer.Configuration
{
    public static class CachedEfCoreOptionsBuilderExtensions
    {
        extension(CachedEfCoreOptionsBuilder builder)
        {
            public CachedEfCoreOptionsBuilder UseSqlServer()
                => builder.WithSqlQueryEntityExtractor<SqlServerQueryEntityExtractor>();
        }
    }
}
