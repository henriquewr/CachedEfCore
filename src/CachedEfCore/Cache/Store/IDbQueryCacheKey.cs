using Microsoft.EntityFrameworkCore;

namespace CachedEfCore.Cache.Store
{
    public interface IDbQueryCacheKey
    {
        DbContextId? DependentDbContext { get; }
    }
}