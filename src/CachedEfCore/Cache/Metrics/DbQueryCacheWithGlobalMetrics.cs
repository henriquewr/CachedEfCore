namespace CachedEfCore.Cache.Metrics
{
    internal class DbQueryCacheWithGlobalMetrics : IDbQueryCacheMetrics
    {
        private readonly IDbQueryCacheMetrics _innerCacheMetrics;

        public DbQueryCacheWithGlobalMetrics(IDbQueryCacheMetrics innerCacheMetrics)
        {
            _innerCacheMetrics = innerCacheMetrics;
        }

        public CacheMetrics GetCacheMetrics()
        {
            return _innerCacheMetrics.GetCacheMetrics();
        }

        public void ReportCacheHit()
        {
            DbQueryCacheMetrics.GlobalInstance.ReportCacheHit();
            _innerCacheMetrics.ReportCacheHit();
        }

        public void ReportCacheMiss()
        {
            DbQueryCacheMetrics.GlobalInstance.ReportCacheMiss();
            _innerCacheMetrics.ReportCacheMiss();
        }

        public void Reset()
        {
            _innerCacheMetrics.Reset();
        }
    }
}
