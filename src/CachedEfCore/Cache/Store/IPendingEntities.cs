using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;

namespace CachedEfCore.Cache.Store
{
    public interface IPendingEntities : IDisposable, IAsyncDisposable
    {
        IReadOnlyList<IEntityType> EntityTypes { get; init; }
    }
}
