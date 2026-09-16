namespace CachedEfCore.Cache.Metrics
{
    internal sealed class DbQueryCacheWithGlobalMetrics : IDbQueryCacheMetrics
    {
        private readonly IDbQueryCacheMetrics _globalMetrics;
        private readonly IDbQueryCacheMetrics _innerCacheMetrics;

        public DbQueryCacheWithGlobalMetrics(IDbQueryCacheMetrics globalMetrics, IDbQueryCacheMetrics innerCacheMetrics)
        {
            _globalMetrics = globalMetrics;
            _innerCacheMetrics = innerCacheMetrics;
        }

        public CacheMetrics GetCacheMetrics()
        {
            return _innerCacheMetrics.GetCacheMetrics();
        }

        public void ReportCacheHit()
        {
            _globalMetrics.ReportCacheHit();
            _innerCacheMetrics.ReportCacheHit();
        }

        public void ReportCacheMiss()
        {
            _globalMetrics.ReportCacheMiss();
            _innerCacheMetrics.ReportCacheMiss();
        }

        public void Reset()
        {
            _innerCacheMetrics.Reset();
        }
    }
}
