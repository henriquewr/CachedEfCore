using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;

namespace CachedEfCore.Cache.EventData
{
    public interface IOnInvalidatingDependentEntities
    {
        HashSet<IEntityType> Entities { get; init; }
        DbContext DbContext { get; init; }
    }
}
