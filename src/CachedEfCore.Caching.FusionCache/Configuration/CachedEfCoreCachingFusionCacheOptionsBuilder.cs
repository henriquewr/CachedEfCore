using CachedEfCore.Caching.FusionCache.DbContextOptionExtensions;
using ZiggyCreatures.Caching.Fusion;

namespace CachedEfCore.Caching.FusionCache.Configuration
{
    public class CachedEfCoreCachingFusionCacheOptionsBuilder
    {
        private readonly CachedEfCoreFusionCacheExtension _cachedEfCoreFusionCacheExtension;

        public CachedEfCoreCachingFusionCacheOptionsBuilder(CachedEfCoreFusionCacheExtension cachedEfCoreExtension)
        {
            _cachedEfCoreFusionCacheExtension = cachedEfCoreExtension;
        }

        public CachedEfCoreCachingFusionCacheOptionsBuilder UseInstance(IFusionCache fusionCacheInstance)
        {
            _cachedEfCoreFusionCacheExtension.WithFusionCacheInstance(fusionCacheInstance);

            return this;
        }

    }
}
