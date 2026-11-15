using CachedEfCore.Cache.Metrics;
using CachedEfCore.Cache.Store;
using CachedEfCore.Caching.FusionCache.DbContextOptionExtensions;
using CachedEfCore.Caching.FusionCache.Store;
using CachedEfCore.Caching.Store;
using CachedEfCore.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

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
                    var inMemoryPendingEntitiesImplementation = dbContext.GetService<InMemoryPendingEntitiesImplementation>();

                    var fusionCacheExtension = dbContext.GetService<IDbContextOptions>()
                        .FindExtension<CachedEfCoreFusionCacheExtension>()!;

                    return new DbQueryCacheFusionCacheStore(dbContext, fusionCacheExtension.FusionCache, metrics, inMemoryPendingEntitiesImplementation);
                }),
                GetServiceProviderHashCode = null,
                ShouldUseSameServiceProvider = null,
                Options = null,
            };
            yield return new CachedEfCoreService
            {
                ServiceDescriptor = ServiceDescriptor.Singleton<InMemoryPendingEntitiesImplementation, InMemoryPendingEntitiesImplementation>(static sp =>
                {
                    var memoryCache = sp.GetRequiredService<IMemoryCache>();
                    return new InMemoryPendingEntitiesImplementation(memoryCache, new object());
                }),
                GetServiceProviderHashCode = null,
                ShouldUseSameServiceProvider = null,
                Options = null,
            };
        }
    }
}
