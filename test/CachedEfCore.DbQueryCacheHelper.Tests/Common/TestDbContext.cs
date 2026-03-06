using CachedEfCore.Context;
using CachedEfCore.Interceptors;
using CachedEfCore.SqlAnalysis;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CachedEfCore.Cache.Tests.Common
{
    public class TestDbContext : CachedDbContext
    {
        public TestDbContext(IDbQueryCacheStore dbQueryCacheStore) : base(dbQueryCacheStore)
        {
        }

        public TestDbContext(IDbQueryCacheStore dbQueryCacheStore, DbContextOptions<TestDbContext> options) : base(options, dbQueryCacheStore)
        {
        }

        public Cache.DbQueryCacheStore TestDbQueryCacheStore => (Cache.DbQueryCacheStore)this.DbQueryCacheStore;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseLazyLoadingProxies();

            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(new DbStateInterceptor(new SqlServerQueryEntityExtractor()));
            base.OnConfiguring(optionsBuilder);
        }

        public DbSet<LazyLoadEntity> LazyLoadEntity { get; set; }
        public DbSet<NonLazyLoadEntity> NonLazyLoadEntity { get; set; }
    }

    public class LazyLoadEntity
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(LazyLoadProp))]
        public int? LazyLoadPropId { get; set; }

        [ForeignKey(nameof(LazyLoadPropId))]
        public virtual NonLazyLoadEntity? LazyLoadProp { get; set; }
    }

    public class NonLazyLoadEntity
    {
        [Key]
        public int Id { get; set; }

        public string? StringData { get; set; }
    }
}
