#if TEST_BUILD
using System.Collections.Concurrent;

namespace CachedEfCore.Caching.InMemory.Store
{
    public partial class DbQueryCacheInMemoryInternalStore
    {
        /// <summary>
        /// Access for testing purposes only
        /// </summary>
        public ConcurrentDictionary<Guid, CancellationTokenSource> TestDbContextDependentKeys => _dbContextDependentKeys;

        /// <summary>
        /// Access for testing purposes only
        /// </summary>
        public ConcurrentDictionary<Type, CancellationTokenSource> TestTypeKeys => _typeKeys;
    }
}
#endif