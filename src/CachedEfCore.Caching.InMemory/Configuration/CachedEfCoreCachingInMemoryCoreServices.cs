using CachedEfCore.Cache.Metrics;
using CachedEfCore.Cache.Store;
using CachedEfCore.Caching.InMemory.DbContextOptionExtensions;
using CachedEfCore.Caching.InMemory.Store;
using CachedEfCore.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace CachedEfCore.Caching.InMemory.Configuration
{
    public static class CachedEfCoreCachingInMemoryCoreServices
    {
        public static IEnumerable<CachedEfCoreService> GetCoreServices()
        {
            yield return new CachedEfCoreService
            {
                ServiceDescriptor = ServiceDescriptor.Scoped<IDbQueryCacheStore>(sp =>
                {
                    var dbContext = sp.GetRequiredService<ICurrentDbContext>().Context;
                    var metrics = dbContext.GetService<IDbQueryCacheMetrics>();

                    var inMemoryCacheExtension = dbContext.GetService<IDbContextOptions>()
                        .FindExtension<CachedEfCoreInMemoryCacheExtension>()!;

                    return new DbQueryCacheInMemoryStore(dbContext, inMemoryCacheExtension.DbQueryCacheInternalStore, metrics);
                }),
                GetServiceProviderHashCode = null,
                ShouldUseSameServiceProvider = null,
                Options = null,
            };
        }
    }
}
