using CachedEfCore.Configuration;

namespace CachedEfCore.Caching.InMemory.Configuration
{
    public static class CachingInMemoryCachedEfCoreOptionsBuilderExtensions
    {
        extension(CachedEfCoreOptionsBuilder builder)
        {
            public CachedEfCoreOptionsBuilder UseInMemoryCacheStore(Action<CachedEfCoreCachingInMemoryOptionsBuilder>? configure = null)
            {
                foreach (var item in CachedEfCoreCachingInMemoryCoreServices.GetCoreServices())
                {
                    builder.CachedEfCoreExtension.AddService(item);
                }

                var optionsBuilder = new CachedEfCoreCachingInMemoryOptionsBuilder(builder.CachedEfCoreExtension);
                configure?.Invoke(optionsBuilder);

                return builder;
            }
        }
    }
}
