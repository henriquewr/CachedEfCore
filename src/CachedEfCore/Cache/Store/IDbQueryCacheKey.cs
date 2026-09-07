using System;

namespace CachedEfCore.Cache.Store
{
    public interface IDbQueryCacheKey
    {
        Guid? DependentDbContext { get; }
    }
}