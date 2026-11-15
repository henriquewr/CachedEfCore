using CachedEfCore.Cache.Store;
using CachedEfCore.Caching.InMemory.Store;
using CachedEfCore.Caching.Store;
using CachedEfCore.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

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
                    ServiceDescriptor = ServiceDescriptor.Scoped<DbQueryCacheStoreInMemoryCacheEntryOptions>(sp =>
                    {
                        return new DbQueryCacheStoreInMemoryCacheEntryOptions() { EntryOptions = memoryCacheOptions };
                    }),
                    GetServiceProviderHashCode = null,
                    ShouldUseSameServiceProvider = null,
                    Options = null,
                };
            }

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

            yield return new CachedEfCoreService
            {
                ServiceDescriptor = ServiceDescriptor.Singleton<DbQueryCacheInMemoryInternalStore, DbQueryCacheInMemoryInternalStore>(),
                GetServiceProviderHashCode = null,
                ShouldUseSameServiceProvider = null,
                Options = null,
            };

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
