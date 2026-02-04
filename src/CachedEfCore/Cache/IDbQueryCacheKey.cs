using System;

namespace CachedEfCore.Cache
{
    public interface IDbQueryCacheKey
    {
        Guid? DependentDbContext { get; }
    }
}