using CachedEfCore.Configuration;
using CachedEfCore.SqlAnalysis;
using CachedEfCore.SqlServer.SqlAnalysis;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace CachedEfCore.SqlServer.Configuration
{
    public static class CachedEfCoreOptionsBuilderExtensions
    {
        extension(CachedEfCoreOptionsBuilder builder)
        {
            public CachedEfCoreOptionsBuilder UseSqlServer()
            {
                var sqlQueryEntityExtractorType = typeof(SqlServerQueryEntityExtractor);

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
