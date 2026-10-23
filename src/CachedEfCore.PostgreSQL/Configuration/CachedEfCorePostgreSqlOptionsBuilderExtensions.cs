using CachedEfCore.Configuration;
using CachedEfCore.PostgreSQL.SqlAnalysis;
using CachedEfCore.SqlAnalysis;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace CachedEfCore.PostgreSQL.Configuration
{
    public static class CachedEfCorePostgreSqlOptionsBuilderExtensions
    {
        extension(CachedEfCoreOptionsBuilder builder)
        {
            public CachedEfCoreOptionsBuilder UsePostgreSql()
            {
                var sqlQueryEntityExtractorType = typeof(PostgreSqlQueryEntityExtractor);

                builder.CachedEfCoreExtension.AddOrReplaceService(new CachedEfCoreService
                {
                    ServiceDescriptor = ServiceDescriptor.Singleton(typeof(ISqlQueryEntityExtractor), sqlQueryEntityExtractorType),
                    GetServiceProviderHashCode = thisService => ((Type)thisService.Options!).GetHashCode(),
                    ShouldUseSameServiceProvider = args => ((Type)args.ThisService.Options!) == ((Type)args.OtherServices.Single().Options!),
                    Options = sqlQueryEntityExtractorType,
                });

                return builder;
            }
        }
    }
}
