using CachedEfCore.Context;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;

namespace CachedEfCore.Cache.EventData
{
    public record class OnInvalidatingRootEntities : IOnInvalidatingRootEntities
    {
        public required HashSet<IEntityType> Entities { get; init; }
        public required ICachedDbContext CachedDbContext { get; init; }
    }
}
