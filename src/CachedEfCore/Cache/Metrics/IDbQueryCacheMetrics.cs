namespace CachedEfCore.Cache.Metrics
{
    public interface IDbQueryCacheMetrics
    {
        void Reset();

        void ReportCacheHit();
        void ReportCacheMiss();

        CacheMetrics GetCacheMetrics();
    }

    public readonly struct CacheMetrics
    {
        public required readonly uint Hits { get; init; }
        public required readonly uint Misses { get; init; }

        public readonly ulong All => Hits + Misses;

        public readonly double Ratio => All == 0 ? 0 : (double)Hits / All;
    }
}
