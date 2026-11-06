using CachedEfCore.Cache.Metrics;
using CachedEfCore.Cache.Store;
using CachedEfCore.Caching.FusionCache.DbContextOptionExtensions;
using CachedEfCore.Caching.FusionCache.Store;
using CachedEfCore.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CachedEfCore.Caching.FusionCache.Configuration
{
    internal static class CachedEfCoreCachingFusionCacheCoreServices
    {
        public static IEnumerable<CachedEfCoreService> GetCoreServices()
        {
            yield return new CachedEfCoreService
            {
                ServiceDescriptor = ServiceDescriptor.Scoped<IDbQueryCacheStore>(static sp =>
                {
                    var dbContext = sp.GetRequiredService<ICurrentDbContext>().Context;
                    var metrics = dbContext.GetService<IDbQueryCacheMetrics>();

                    var fusionCacheExtension = dbContext.GetService<IDbContextOptions>()
                        .FindExtension<CachedEfCoreFusionCacheExtension>()!;

                    return new DbQueryCacheFusionCacheStore(dbContext, fusionCacheExtension.FusionCache, metrics);
                }),
                GetServiceProviderHashCode = null,
                ShouldUseSameServiceProvider = null,
                Options = null,
            };
        }
    }
}
