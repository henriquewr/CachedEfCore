using CachedEfCore.Cache.Helper;
using CachedEfCore.Cache.Metrics;
using CachedEfCore.Configuration;
using CachedEfCore.DbContextOptionExtensions;
using CachedEfCore.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace CachedEfCore.DependencyInjection
{
    public static class CachedEfCoreDIExtensions
    {
        extension(IServiceCollection services)
        {
            public IServiceCollection AddCachedEfCore()
            {
                services.TryAddSingleton<IDbQueryCacheHelper, DbQueryCacheHelper>();
                services.TryAddSingleton<IDbQueryCacheMetrics>(DbQueryCacheMetrics.GlobalInstance);

                return services;
            }
        }

        extension (DbContextOptionsBuilder builder)
        {
            public DbContextOptionsBuilder UseCachedEfCore(Action<CachedEfCoreOptionsBuilder>? configure = null)
            {
                var extension = new CachedEfCoreDbContextOptionExtension(builder);

                var options = new CachedEfCoreOptionsBuilder(extension);
                configure?.Invoke(options);

                ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(extension);

                return builder;
            }
        }
    }
}