using CachedEfCore.Cache;
using CachedEfCore.DependencyManager;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Linq;
using System;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace CachedEfCore.Context
{
    public class CachedDbContext : DbContext, ICachedDbContext
    {
        public IDbQueryCacheStore DbQueryCacheStore { get; }
        public EntityDependency DependencyManager => EntityDependency.GetOrAdd(this);
        public IDictionary<string, ImmutableArray<IEntityType>> TableEntity { get; }
        public Guid Id => this.ContextId.InstanceId;

        public CachedDbContext(IDbQueryCacheStore dbQueryCacheStore) : base()
        {
            DbQueryCacheStore = dbQueryCacheStore;
            TableEntity = GetTableEntity(this.Model);
        }

        public CachedDbContext(DbContextOptions options, IDbQueryCacheStore dbQueryCacheStore) : base(options)
        {
            DbQueryCacheStore = dbQueryCacheStore;
            TableEntity = GetTableEntity(this.Model);
        }

        private static IDictionary<string, ImmutableArray<IEntityType>> GetTableEntity(IModel model)
        {
            var tableEntity = model.GetEntityTypes().GroupBy(x => x.GetTableName() ?? x.GetViewName()!).ToFrozenDictionary(k => k.Key, v => v.ToImmutableArray());
            return tableEntity;
        }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            //optionsBuilder.AddInterceptors(new DbStateInterceptor());

            base.OnConfiguring(optionsBuilder);
        }

        public override void Dispose()
        {
            DbQueryCacheStore.RemoveAllLazyLoadByContextId(ContextId.InstanceId, DependencyManager);

            base.Dispose();
        }
        public override ValueTask DisposeAsync()
        {
            DbQueryCacheStore.RemoveAllLazyLoadByContextId(ContextId.InstanceId, DependencyManager);

            return base.DisposeAsync();
        }
    }
}
