#if TEST_BUILD
using System;
using System.Collections.Concurrent;

namespace CachedEfCore.Cache
{
    public partial class DbQueryCacheStore
    {
        /// <summary>
        /// Access for testing purposes only
        /// </summary>
        public ConcurrentDictionary<Guid, ConcurrentBag<object>> TestCacheKeysByContextId => _cacheKeysByContextId;

        /// <summary>
        /// Access for testing purposes only
        /// </summary>
        public ConcurrentDictionary<Type, ConcurrentBag<object>> TestCacheKeysByType => _cacheKeysByType;
    }
}
#endif