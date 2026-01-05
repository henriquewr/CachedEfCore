using CachedEfCore.Cache;
using CachedEfCore.Context;
using CachedEfCore.DependencyManager.Attributes;
using CachedEfCore.Interceptors;
using CachedEfCore.SqlAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;

namespace CachedEfCore.DependencyManager.Tests
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
                modelBuilder.ApplyConfigurationsFromAssembly(typeof(TestDbContext).Assembly);

                base.OnModelCreating(modelBuilder);
            }

            public DbSet<LazyLoadEntity> LazyLoadEntity => Set<LazyLoadEntity>();
            public DbSet<NonLazyLoadEntity> NonLazyLoadEntity => Set<NonLazyLoadEntity>();
            public DbSet<AnotherLazyLoadEntity> AnotherLazyLoadEntity => Set<AnotherLazyLoadEntity>();
            public DbSet<LazyLoadWithGenericEntity> LazyLoadWithGenericEntity => Set<LazyLoadWithGenericEntity>();
            public DbSet<EntityWithDependentAttribute> EntityWithDependentAttribute => Set<EntityWithDependentAttribute>();

            public DbSet<EntityManyToMany> EntityManyToMany => Set<EntityManyToMany>();
            public DbSet<EntityManyToManyOtherEntityManyToMany> EntityManyToManyOtherEntityManyToMany => Set<EntityManyToManyOtherEntityManyToMany>();
            public DbSet<OtherEntityManyToMany> OtherEntityManyToMany => Set<OtherEntityManyToMany>();


            public DbSet<EntityManyToManyWithoutNavigation> EntityManyToManyWithoutNavigation => Set<EntityManyToManyWithoutNavigation>();
            public DbSet<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation> EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation => Set<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation>();
            public DbSet<OtherEntityManyToManyWithoutNavigation> OtherEntityManyToManyWithoutNavigation => Set<OtherEntityManyToManyWithoutNavigation>();

            public DbSet<EntityManyToManySkipNavigation> EntityManyToManySkipNavigation => Set<EntityManyToManySkipNavigation>();
            public DbSet<OtherEntityManyToManySkipNavigation> OtherEntityManyToManySkipNavigation => Set<OtherEntityManyToManySkipNavigation>();
           

            public DbSet<SharedPkPrincipal> SharedPkPrincipal => Set<SharedPkPrincipal>();
            public DbSet<SharedPkDependent> SharedPkDependent => Set<SharedPkDependent>();
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

            public class Configuration : IEntityTypeConfiguration<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation>
            {
                public void Configure(EntityTypeBuilder<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation> builder)
                {
                    builder.HasOne<EntityManyToManyWithoutNavigation>()
                        .WithMany(e => e.EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation)
                        .HasForeignKey(e => e.EntityManyToManyWithoutNavigationId);

                    builder.HasOne<OtherEntityManyToManyWithoutNavigation>()
                        .WithMany(e => e.EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation)
                        .HasForeignKey(e => e.OtherEntityManyToManyWithoutNavigationId);
                }
            }
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

            public class Configuration : IEntityTypeConfiguration<EntityManyToManySkipNavigation>
            {
                public void Configure(EntityTypeBuilder<EntityManyToManySkipNavigation> builder)
                {
                    builder.HasMany(p => p.OtherEntityManyToManySkipNavigation)
                        .WithMany(t => t.EntityManyToManySkipNavigation)
                        .UsingEntity(EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName);
                }
            }
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

        public class SharedPkPrincipal
        {
            public int Id { get; set; }
            public virtual SharedPkDependent SharedPkDependent { get; set; } = null!;

            public class Configuration : IEntityTypeConfiguration<SharedPkPrincipal>
            {
                public void Configure(EntityTypeBuilder<SharedPkPrincipal> builder)
                {
                    builder.HasKey(x => x.Id);

                    builder.HasOne(x => x.SharedPkDependent)
                        .WithOne(x => x.SharedPkPrincipal)
                        .HasForeignKey<SharedPkDependent>(x => x.Id);
                }
            }
        }

        public class SharedPkDependent
        {
            public int Id { get; set; }
            public virtual SharedPkPrincipal SharedPkPrincipal { get; set; } = null!;

            public class Configuration : IEntityTypeConfiguration<SharedPkDependent>
            {
                public void Configure(EntityTypeBuilder<SharedPkDependent> builder)
                {
                    builder.HasKey(x => x.Id);

                    builder.Property(x => x.Id)
                        .ValueGeneratedNever();
                }
            }
        }
    }
}
