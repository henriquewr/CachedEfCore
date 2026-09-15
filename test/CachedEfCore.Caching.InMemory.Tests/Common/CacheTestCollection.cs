using Xunit;

namespace CachedEfCore.Caching.InMemory.Tests.Common
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class CacheTestCollection
    {
        public const string Name = "Cache tests";
    }
}
