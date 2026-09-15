using CachedEfCore.Cache.Store;
using Microsoft.EntityFrameworkCore;

namespace CachedEfCore.Caching.InMemory.Store
{
    internal interface IStatefulDbQueryCacheInMemoryInternalStore
    {
        T GetOrAdd<TState, T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, TState state, Func<TState, T> create);
        ValueTask<T> GetOrAddAsync<TState, T>(DbContext dbContext, Type rootEntityType, IDbQueryCacheKey key, TState state, Func<TState, Task<T>> create);
    }
}
