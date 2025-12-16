using CachedEfCore.Cache;
using CachedEfCore.DependencyManager;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System;
using CachedEfCore.EntityMapping;

namespace CachedEfCore.Context
{
    public class CachedDbContext : DbContext, ICachedDbContext
    {
        public IDbQueryCacheStore DbQueryCacheStore { get; }
        public EntityDependency DependencyManager { get; }
        public TableEntityMapping TableEntity { get; }
        public Guid Id => this.ContextId.InstanceId;

        public CachedDbContext(IDbQueryCacheStore dbQueryCacheStore) : base()
        {
            DbQueryCacheStore = dbQueryCacheStore;
            DependencyManager = EntityDependency.GetOrAdd(this);
            TableEntity = TableEntityMapping.GetOrAdd(this);
        }

        public CachedDbContext(DbContextOptions options, IDbQueryCacheStore dbQueryCacheStore) : base(options)
        {
            DbQueryCacheStore = dbQueryCacheStore;
            DependencyManager = EntityDependency.GetOrAdd(this);
            TableEntity = TableEntityMapping.GetOrAdd(this);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            //optionsBuilder.AddInterceptors(new DbStateInterceptor());

            base.OnConfiguring(optionsBuilder);
        }

        public override void Dispose()
        {
            DbQueryCacheStore.RemoveAllDbContextDependent(Id);

            base.Dispose();
        }
        public override ValueTask DisposeAsync()
        {
            DbQueryCacheStore.RemoveAllDbContextDependent(Id);

            return base.DisposeAsync();
        }
    }
}
