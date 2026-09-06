using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;

namespace CachedEfCore.Cache.EventData
{
    public record class OnInvalidatingDependentEntities : IOnInvalidatingDependentEntities
    {
        public required HashSet<IEntityType> Entities { get; init; }
        public required DbContext DbContext { get; init; }
    }
}
