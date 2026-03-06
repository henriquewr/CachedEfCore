using CachedEfCore.Context;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;

namespace CachedEfCore.Cache.EventData
{
    public interface IOnInvalidatingRootEntities
    {
        HashSet<IEntityType> Entities { get; }
        ICachedDbContext CachedDbContext { get; }
    }
}
