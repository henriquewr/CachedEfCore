using System;
using System.Threading.Tasks;

namespace CachedEfCore.Cache.Store
{
    public interface IStatefulDbQueryCacheStore
    {
        T GetOrAdd<TState, T>(Type rootEntityType, IDbQueryCacheKey key, TState state, Func<TState, T> create);
        ValueTask<T> GetOrAddAsync<TState, T>(Type rootEntityType, IDbQueryCacheKey key, TState state, Func<TState, Task<T>> create);
    }
}
