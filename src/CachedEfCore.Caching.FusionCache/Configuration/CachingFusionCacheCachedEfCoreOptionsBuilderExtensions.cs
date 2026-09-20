using CachedEfCore.Configuration;

namespace CachedEfCore.Caching.FusionCache.Configuration
{
    public static class CachingFusionCacheCachedEfCoreOptionsBuilderExtensions
    {
        extension(CachedEfCoreOptionsBuilder builder)
        {
            public CachedEfCoreOptionsBuilder UseFusionCacheStore(Action<CachedEfCoreCachingFusionCacheOptionsBuilder>? configure = null)
            {
                foreach (var item in CachedEfCoreCachingFusionCacheCoreServices.GetCoreServices())
                {
                    builder.CachedEfCoreExtension.AddService(item);
                }

                var optionsBuilder = new CachedEfCoreCachingFusionCacheOptionsBuilder(builder.CachedEfCoreExtension);
                configure?.Invoke(optionsBuilder);

                optionsBuilder.Validate();

                return builder;
            }
        }
    }
}
