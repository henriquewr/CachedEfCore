using CachedEfCore.Cache;
using CachedEfCore.DependencyManager;
using CachedEfCore.EntityMapping;
using System;

namespace CachedEfCore.Context
{
    public interface ICachedDbContext
    {
        public Guid Id { get; }
        public EntityDependency DependencyManager { get; }
        public IDbQueryCacheStore DbQueryCacheStore { get; }
        public TableEntityMapping TableEntity { get; }
    }
}