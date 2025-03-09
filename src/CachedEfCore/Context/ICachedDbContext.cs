using CachedEfCore.Cache;
using CachedEfCore.DependencyManager;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CachedEfCore.Context
{
    public interface ICachedDbContext
    {
        public Guid Id { get; }
        public EntityDependency DependencyManager { get; }
        public IDbQueryCacheStore DbQueryCacheStore { get; }
        public IDictionary<string, ImmutableArray<IEntityType>> TableEntity { get; }
    }
}