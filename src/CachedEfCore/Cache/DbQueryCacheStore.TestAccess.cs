#if TEST_BUILD
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace CachedEfCore.Cache
{
    public partial class DbQueryCacheStore
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