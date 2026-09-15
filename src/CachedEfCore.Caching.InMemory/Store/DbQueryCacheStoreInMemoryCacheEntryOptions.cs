using Microsoft.Extensions.Caching.Memory;

namespace CachedEfCore.Caching.InMemory.Store
{
    internal class DbQueryCacheStoreInMemoryCacheEntryOptions
    {
        public required MemoryCacheEntryOptions EntryOptions { get; set; }
    }
}
