using CachedEfCore.Caching.FusionCache.DbContextOptionExtensions;
using CachedEfCore.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;

namespace CachedEfCore.Caching.FusionCache.Configuration
{
    public static class CachingFusionCacheCachedEfCoreOptionsBuilderExtensions
    {
        extension(CachedEfCoreOptionsBuilder builder)
        {
            public CachedEfCoreOptionsBuilder UseFusionCacheStore(Action<CachedEfCoreCachingFusionCacheOptionsBuilder>? configure = null)
            {
                var extension = new CachedEfCoreFusionCacheExtension(builder.DbContextOptionsBuilder);

                var optionsBuilder = new CachedEfCoreCachingFusionCacheOptionsBuilder(extension);
                configure?.Invoke(optionsBuilder);

                ((IDbContextOptionsBuilderInfrastructure)builder.DbContextOptionsBuilder).AddOrUpdateExtension(extension);

                return builder;
            }
        }
    }
}
