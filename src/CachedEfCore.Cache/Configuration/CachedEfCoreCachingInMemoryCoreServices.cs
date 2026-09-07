using CachedEfCore.Cache.Metrics;
using CachedEfCore.Cache.Store;
using CachedEfCore.Caching.InMemory.Store;
using CachedEfCore.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace CachedEfCore.Caching.InMemory.Configuration
{
    public static class CachedEfCoreCachingInMemoryCoreServices
    {
        public static IEnumerable<CachedEfCoreService> GetCoreServices()
        {
            {
                var memoryCacheOptions = CachedEfCoreCachingInMemoryOptionsDefaults.DefaultMemoryCacheEntryOptions;
                yield return new CachedEfCoreService
                {
                    ServiceDescriptor = ServiceDescriptor.Singleton<IDbQueryCacheInMemoryInternalStore, DbQueryCacheInMemoryInternalStore>(sp =>
                    {
                        var memoryCache = sp.GetRequiredService<IMemoryCache>();
                        var metrics = sp.GetRequiredService<IDbQueryCacheMetrics>();

                        return new DbQueryCacheInMemoryInternalStore(memoryCache, metrics, memoryCacheOptions);
                    }),
                    GetServiceProviderHashCode = thisService => ((MemoryCacheEntryOptions)thisService.Options!).GetHashCode(),
                    ShouldUseSameServiceProvider = arg => ((MemoryCacheEntryOptions)arg.ThisService.Options!) == ((MemoryCacheEntryOptions)arg.OtherServices.Single().Options!),
                    Options = memoryCacheOptions,
                };
            }

            yield return new CachedEfCoreService
            {
                ServiceDescriptor = ServiceDescriptor.Scoped<IDbQueryCacheStore>(sp =>
                {
                    var dbContext = sp.GetRequiredService<ICurrentDbContext>().Context;
                    return new DbQueryCacheInMemoryStore(dbContext);
                }),
                GetServiceProviderHashCode = null,
                ShouldUseSameServiceProvider = null,
                Options = null,
            };
        }
    }
}
