using System.Threading;

namespace CachedEfCore.Cache.Metrics
{
    public class DbQueryCacheMetrics : IDbQueryCacheMetrics
    {
        public static DbQueryCacheMetrics GlobalInstance { get; } = new();

        public void Reset()
        {
            Volatile.Write(ref _cacheHits, 0U);
            Volatile.Write(ref _pending, 0U);
            Volatile.Write(ref _cacheMisses, 0U);
        }

        private uint _cacheHits;
        private uint _pending;
        private uint _cacheMisses;

        public CacheMetrics GetCacheMetrics()
        {
            return new CacheMetrics
            {
                Hits = Volatile.Read(ref _cacheHits),
                Pending = Volatile.Read(ref _pending),
                Misses = Volatile.Read(ref _cacheMisses),
            };
        }

        public void ReportCacheHit()
        {
            Interlocked.Increment(ref _cacheHits);
        }

        public void ReportCacheMiss()
        {
            Interlocked.Increment(ref _cacheMisses);
        }

        public void ReportCachePending()
        {
            Interlocked.Increment(ref _pending);
            ReportCacheMiss();
        }
    }
}
