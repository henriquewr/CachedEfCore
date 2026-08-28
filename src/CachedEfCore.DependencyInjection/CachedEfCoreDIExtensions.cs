using CachedEfCore.Cache.Helper;
using CachedEfCore.Cache.Metrics;
using CachedEfCore.Configuration;
using CachedEfCore.DbContextOptionExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
                var options = new CachedEfCoreOptionsBuilder();
                configure?.Invoke(options);
                var builtOptions = options.Build();

                var extension = new CachedEfCoreDbContextOptionExtension(builder.Options.ContextType, builtOptions);

                ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(extension);

                return builder;
            }
        }
    }
}