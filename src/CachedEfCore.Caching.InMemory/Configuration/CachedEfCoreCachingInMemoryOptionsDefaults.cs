using Microsoft.Extensions.Caching.Memory;

namespace CachedEfCore.Caching.InMemory.Configuration
{
    public static class CachedEfCoreCachingInMemoryOptionsDefaults
    {
        public static MemoryCacheEntryOptions DefaultMemoryCacheEntryOptions => new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
            Size = 0
        };
    }
}
