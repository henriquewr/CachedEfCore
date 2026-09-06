using Microsoft.EntityFrameworkCore.Infrastructure;
using System;

namespace CachedEfCore.Cache
{
    public interface IDbQueryCacheStore : IDbQueryCacheInternalStore, IResettableService, IDisposable, IAsyncDisposable
    {
    }
}