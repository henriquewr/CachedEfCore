using CachedEfCore.Cache;
using CachedEfCore.DependencyManager;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System;
using CachedEfCore.EntityMapping;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CachedEfCore.Context
{
    public class CachedDbContext : DbContext, ICachedDbContext
    {
        public IDbQueryCacheStore DbQueryCacheStore { get; }
        public EntityDependency DependencyManager { get; }
        public TableEntityMapping TableEntity { get; }
        public DbContext DbContext => this;
        public Guid Id => this.ContextId.InstanceId;

        public CachedDbContext() : base()
        {
            DbQueryCacheStore = this.GetService<IDbQueryCacheStore>();
            DependencyManager = this.GetService<EntityDependency>();
            TableEntity = this.GetService<TableEntityMapping>();
        }

        public CachedDbContext(DbContextOptions options) : base(options)
        {
            DbQueryCacheStore = this.GetService<IDbQueryCacheStore>();
            DependencyManager = this.GetService<EntityDependency>();
            TableEntity = this.GetService<TableEntityMapping>();
        }

        public override void Dispose()
        {
            DbQueryCacheStore.RemoveAllDbContextDependent(Id);

            GC.SuppressFinalize(this);

            base.Dispose();
        }
        public override ValueTask DisposeAsync()
        {
            DbQueryCacheStore.RemoveAllDbContextDependent(Id);

            GC.SuppressFinalize(this);

            return base.DisposeAsync();
        }
    }
}
