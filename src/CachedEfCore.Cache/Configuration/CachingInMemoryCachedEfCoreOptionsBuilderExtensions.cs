using CachedEfCore.Configuration;

namespace CachedEfCore.Caching.InMemory.Configuration
{
    public static class CachingInMemoryCachedEfCoreOptionsBuilderExtensions
    {
        extension(CachedEfCoreOptionsBuilder builder)
        {
            public CachedEfCoreOptionsBuilder UseInMemoryCacheStore()
            {
                foreach (var item in CachedEfCoreCachingInMemoryCoreServices.GetCoreServices())
                {
                    builder.CachedEfCoreExtension.AddService(item);
                }

                return builder;
            }
        }
    }
}
