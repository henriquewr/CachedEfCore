using CachedEfCore.Cache;
using CachedEfCore.Context;
using CachedEfCore.DependencyManager.Attributes;
using CachedEfCore.Interceptors;
using CachedEfCore.SqlAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;

namespace CachedEfCore.DepencencyManager.Tests
{
    public class EntityDependencyManagerTestBase
    {
        protected static readonly TestDbContext _cachedDbContext = new TestDbContext(new DbQueryCacheStore(new MemoryCache(new MemoryCacheOptions())));

        protected static IEntityType GetIEntityType(Type entityType)
        {
            return _cachedDbContext.Model.FindEntityType(entityType) ?? throw new InvalidDataException();
        }

        protected static IEntityType GetIEntityType(string name)
        {
            return _cachedDbContext.Model.FindEntityType(name) ?? throw new InvalidDataException();
        }

        public class TestDbContext : CachedDbContext
        {
            public TestDbContext(IDbQueryCacheStore dbQueryCacheStore) : base(dbQueryCacheStore)
            {
            }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseLazyLoadingProxies();

                optionsBuilder.UseInMemoryDatabase("test").AddInterceptors(new DbStateInterceptor(new SqlServerQueryEntityExtractor()));
                base.OnConfiguring(optionsBuilder);
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<EntityManyToManySkipNavigation>()
                    .HasMany(p => p.OtherEntityManyToManySkipNavigation)
                    .WithMany(t => t.EntityManyToManySkipNavigation)
                    .UsingEntity(EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName);


                modelBuilder.Entity<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation>();

                modelBuilder.Entity<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation>()
                    .HasOne<EntityManyToManyWithoutNavigation>()
                    .WithMany(e => e.EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation)
                    .HasForeignKey(e => e.EntityManyToManyWithoutNavigationId);

                modelBuilder.Entity<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation>()
                    .HasOne<OtherEntityManyToManyWithoutNavigation>()
                    .WithMany(e => e.EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation)
                    .HasForeignKey(e => e.OtherEntityManyToManyWithoutNavigationId);


                base.OnModelCreating(modelBuilder);
            }

            public DbSet<LazyLoadEntity> LazyLoadEntity { get; set; }
            public DbSet<NonLazyLoadEntity> NonLazyLoadEntity { get; set; }
            public DbSet<AnotherLazyLoadEntity> AnotherLazyLoadEntity { get; set; }
            public DbSet<LazyLoadWithGenericEntity> LazyLoadWithGenericEntity { get; set; }
            public DbSet<EntityWithDependentAttribute> EntityWithDependentAttribute { get; set; }

            public DbSet<EntityManyToMany> EntityManyToMany { get; set; }
            public DbSet<EntityManyToManyOtherEntityManyToMany> EntityManyToManyOtherEntityManyToMany { get; set; }
            public DbSet<OtherEntityManyToMany> OtherEntityManyToMany { get; set; }


            public DbSet<EntityManyToManyWithoutNavigation> EntityManyToManyWithoutNavigation { get; set; }
            public DbSet<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation> EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation { get; set; }
            public DbSet<OtherEntityManyToManyWithoutNavigation> OtherEntityManyToManyWithoutNavigation { get; set; }

            public DbSet<EntityManyToManySkipNavigation> EntityManyToManySkipNavigation { get; set; }
            public DbSet<OtherEntityManyToManySkipNavigation> OtherEntityManyToManySkipNavigation { get; set; }
        }

        public class AnotherLazyLoadEntity
        {
            [Key]
            public int Id { get; set; }
            public string? StringData { get; set; }

            [ForeignKey(nameof(LazyLoadProp))]
            public int? LazyLoadPropId { get; set; }

            [ForeignKey(nameof(LazyLoadPropId))]
            public virtual LazyLoadEntity? LazyLoadProp { get; set; }
        }

        public class LazyLoadWithGenericEntity
        {
            [Key]
            public int Id { get; set; }
            public string? StringData { get; set; }

            public virtual ICollection<NonLazyLoadEntity>? LazyLoadGenericProp { get; set; }
        }

        public class LazyLoadEntity
        {
            [Key]
            public int Id { get; set; }
            public string? StringData { get; set; }

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

        [DependentOnEntity(typeof(LazyLoadEntity))]
        public class EntityWithDependentAttribute
        {
            [Key]
            public int Id { get; set; }

            public string? StringData { get; set; }
        }




        public class EntityManyToMany
        {
            [Key]
            public int Id { get; set; }

            public virtual ICollection<EntityManyToManyOtherEntityManyToMany>? EntityManyToManyOtherEntityManyToMany { get; set; }
        }

        public class EntityManyToManyOtherEntityManyToMany
        {
            [Key]
            public int Id { get; set; }



            [ForeignKey(nameof(EntityManyToMany))]
            public int? EntityManyToManyId { get; set; }

            [ForeignKey(nameof(EntityManyToManyId))]
            public virtual EntityManyToMany? EntityManyToMany { get; set; }



            [ForeignKey(nameof(OtherEntityManyToMany))]
            public int? OtherEntityManyToManyId { get; set; }

            [ForeignKey(nameof(OtherEntityManyToManyId))]
            public virtual OtherEntityManyToMany? OtherEntityManyToMany { get; set; }
        }

        public class OtherEntityManyToMany
        {
            [Key]
            public int Id { get; set; }

            public virtual ICollection<EntityManyToManyOtherEntityManyToMany>? EntityManyToManyOtherEntityManyToMany { get; set; }
        }




        public class EntityManyToManyWithoutNavigation
        {
            [Key]
            public int Id { get; set; }

            public virtual ICollection<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation>? EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation { get; set; }
        }

        public class EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation
        {
            [Key]
            public int Id { get; set; }

            public int? EntityManyToManyWithoutNavigationId { get; set; }

            public int? OtherEntityManyToManyWithoutNavigationId { get; set; }
        }

        public class OtherEntityManyToManyWithoutNavigation
        {
            [Key]
            public int Id { get; set; }

            public virtual ICollection<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation>? EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation { get; set; }
        }




        public class EntityManyToManySkipNavigation
        {
            [Key]
            public int Id { get; set; }

            public virtual ICollection<OtherEntityManyToManySkipNavigation>? OtherEntityManyToManySkipNavigation { get; set; }
        }

        public static class EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation
        {
            public const string TableName = "EntityManyToManySkipNavigation_OtherEntityManyToManySkipNavigation";
        }

        public class OtherEntityManyToManySkipNavigation
        {
            [Key]
            public int Id { get; set; }

            public virtual ICollection<EntityManyToManySkipNavigation>? EntityManyToManySkipNavigation { get; set; }
        }
    }
}
