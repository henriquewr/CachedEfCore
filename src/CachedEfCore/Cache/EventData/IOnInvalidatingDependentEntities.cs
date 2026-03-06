using CachedEfCore.Context;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;

namespace CachedEfCore.Cache.EventData
{
    public interface IOnInvalidatingDependentEntities
    {
        HashSet<IEntityType> Entities { get; init; }
        ICachedDbContext CachedDbContext { get; init; }
    }
}
