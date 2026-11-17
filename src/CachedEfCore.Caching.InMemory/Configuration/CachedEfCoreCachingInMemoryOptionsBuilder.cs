using CachedEfCore.Caching.InMemory.DbContextOptionExtensions;
using Microsoft.Extensions.Caching.Memory;
using System;

namespace CachedEfCore.Caching.InMemory.Configuration
{
    public class CachedEfCoreCachingInMemoryOptionsBuilder
    {
        private readonly CachedEfCoreInMemoryCacheExtension _cachedEfCoreInMemoryCacheExtension;

        public CachedEfCoreCachingInMemoryOptionsBuilder(CachedEfCoreInMemoryCacheExtension cachedEfCoreInMemoryCacheExtension)
        {
            _cachedEfCoreInMemoryCacheExtension = cachedEfCoreInMemoryCacheExtension;
        }

        public CachedEfCoreCachingInMemoryOptionsBuilder WithMemoryCacheEntryOptions(Func<MemoryCacheEntryOptions, MemoryCacheEntryOptions> action)
        {
            var memoryCacheOptions = action(CachedEfCoreCachingInMemoryOptionsDefaults.DefaultMemoryCacheEntryOptions);

            _cachedEfCoreInMemoryCacheExtension.WithMemoryCacheEntryOptions(memoryCacheOptions);
            return this;
        }

        public CachedEfCoreCachingInMemoryOptionsBuilder UseInstance(IMemoryCache memoryCacheInstance)
        {
            _cachedEfCoreInMemoryCacheExtension.WithMemoryCacheInstance(memoryCacheInstance);

            return this;
        }
    }
}
