using CachedEfCore.Caching.InMemory.Store;
using CachedEfCore.Configuration;
using CachedEfCore.DbContextOptionExtensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace CachedEfCore.Caching.InMemory.Configuration
{
    public class CachedEfCoreCachingInMemoryOptionsBuilder
    {
        private readonly CachedEfCoreDbContextOptionExtension _cachedEfCoreExtension;

        public CachedEfCoreCachingInMemoryOptionsBuilder(CachedEfCoreDbContextOptionExtension cachedEfCoreExtension)
        {
            _cachedEfCoreExtension = cachedEfCoreExtension;
        }

        public CachedEfCoreCachingInMemoryOptionsBuilder WithMemoryCacheEntryOptions(Func<MemoryCacheEntryOptions, MemoryCacheEntryOptions> action)
        {
            var memoryCacheOptions = action(CachedEfCoreCachingInMemoryOptionsDefaults.DefaultMemoryCacheEntryOptions);

            var service = new CachedEfCoreService
            {
                ServiceDescriptor = ServiceDescriptor.Singleton<IDbQueryCacheInternalStore, DbQueryCacheInternalStore>(),
                GetServiceProviderHashCode = thisService => ((MemoryCacheEntryOptions)thisService.Options!).GetHashCode(),
                ShouldUseSameServiceProvider = arg => ((MemoryCacheEntryOptions)arg.ThisService.Options!) == ((MemoryCacheEntryOptions)arg.OtherServices.Single().Options!),
                Options = memoryCacheOptions,
            };

            _cachedEfCoreExtension.ReplaceService(service);

            return this;
        }
    }
}
