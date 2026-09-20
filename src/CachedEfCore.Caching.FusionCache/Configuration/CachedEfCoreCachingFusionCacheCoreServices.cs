using CachedEfCore.Cache.Metrics;
using CachedEfCore.Cache.Store;
using CachedEfCore.Caching.FusionCache.Store;
using CachedEfCore.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace CachedEfCore.Caching.FusionCache.Configuration
{
    internal static class CachedEfCoreCachingFusionCacheCoreServices
    {
        public static IEnumerable<CachedEfCoreService> GetCoreServices()
        {
            yield return new CachedEfCoreService
            {
                ServiceDescriptor = ServiceDescriptor.Scoped<IDbQueryCacheStore>(sp =>
                {
                    var dbContext = sp.GetRequiredService<ICurrentDbContext>().Context;
                    var fusionCache = dbContext.GetService<IFusionCache>();
                    var metrics = dbContext.GetService<IDbQueryCacheMetrics>();

                    return new DbQueryCacheFusionCacheStore(dbContext, fusionCache, metrics);
                }),
                GetServiceProviderHashCode = null,
                ShouldUseSameServiceProvider = null,
                Options = null,
            };
        }
    }
}
