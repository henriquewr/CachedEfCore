using CachedEfCore.Caching.InMemory.DbContextOptionExtensions;
using CachedEfCore.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;

namespace CachedEfCore.Caching.InMemory.Configuration
{
    public static class CachingInMemoryCachedEfCoreOptionsBuilderExtensions
    {
        extension(CachedEfCoreOptionsBuilder builder)
        {
            public CachedEfCoreOptionsBuilder UseInMemoryCacheStore(Action<CachedEfCoreCachingInMemoryOptionsBuilder>? configure = null)
            {
                var extension = new CachedEfCoreInMemoryCacheExtension(builder.DbContextOptionsBuilder);

                var optionsBuilder = new CachedEfCoreCachingInMemoryOptionsBuilder(extension);
                configure?.Invoke(optionsBuilder);

                ((IDbContextOptionsBuilderInfrastructure)builder.DbContextOptionsBuilder).AddOrUpdateExtension(extension);

                return builder;
            }
        }
    }
}
