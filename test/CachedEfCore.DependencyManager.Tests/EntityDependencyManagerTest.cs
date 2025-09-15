using CachedEfCore.Context;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using CachedEfCore.Cache;
using CachedEfCore.DependencyManager.Attributes;
using Microsoft.Extensions.Caching.Memory;
using CachedEfCore.Interceptors;
using CachedEfCore.SqlAnalysis;
using Xunit;

namespace CachedEfCore.DepencencyManager.Tests
{
    public class EntityDependencyDiscover
    {
        private static readonly CachedDbContext _cachedDbContext = new TestDbContext(new DbQueryCacheStore(new MemoryCache(new MemoryCacheOptions())));

        public EntityDependencyDiscover()
        {
        }

        private static IEntityType GetIEntityType(Type entityType)
        {
            return _cachedDbContext.Model.GetEntityTypes().First(x => x.ClrType == entityType);
        }

        public static TheoryData<Type, Type, Type, HashSet<IEntityType>> GetUnderRelatedEntitiesTestCases()
        {
            var theoryData = new TheoryData<Type, Type, Type, HashSet<IEntityType>>
            {
                {
                    typeof(NonLazyLoadEntity),
                    new { A = default(NonLazyLoadEntity) }.GetType(),
                    new { A = default(IEnumerable<NonLazyLoadEntity>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                    }
                },
                {
                    typeof(LazyLoadEntity),
                    new { A = default(LazyLoadEntity) }.GetType(),
                    new { A = default(IEnumerable<LazyLoadEntity>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                    }
                },
                {
                    typeof(AnotherLazyLoadEntity),
                    new { A = default(AnotherLazyLoadEntity) }.GetType(),
                    new { A = default(IEnumerable<AnotherLazyLoadEntity>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                    }
                },
                {
                    typeof(LazyLoadWithGenericEntity),
                    new { A = default(LazyLoadWithGenericEntity) }.GetType(),
                    new { A = default(IEnumerable<LazyLoadWithGenericEntity>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                    }
                },
                {
                    typeof(EntityWithDependentAttribute),
                    new { A = default(EntityWithDependentAttribute) }.GetType(),
                    new { A = default(IEnumerable<EntityWithDependentAttribute>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                    }
                }
            };

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetUnderRelatedEntitiesTestCases))]
        public void GetUnderRelatedEntities_Should_Return_Under_Related(Type type, Type anonymousType, Type genericAnonymousType, HashSet<IEntityType> expected)
        {
            var iEntityType = GetIEntityType(type);
            var entitiesByIEntityType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(iEntityType);
            Assert.True(expected.SetEquals(entitiesByIEntityType));

            var entities = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(type);
            Assert.True(expected.SetEquals(entities));


            var entitiesByAnonymousType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(anonymousType);
            Assert.True(expected.SetEquals(entitiesByAnonymousType));


            var entitiesByGenericAnonymousType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(genericAnonymousType);
            Assert.True(expected.SetEquals(entitiesByGenericAnonymousType));
        }




        public static TheoryData<Type, Type, Type, HashSet<IEntityType>> GetAboveRelatedEntitiesTestCases()
        {
            var theoryData = new TheoryData<Type, Type, Type, HashSet<IEntityType>>
            {
                {
                    typeof(NonLazyLoadEntity),
                    new { A = default(NonLazyLoadEntity) }.GetType(),
                    new { A = default(IEnumerable<NonLazyLoadEntity>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                    }
                },
                {
                    typeof(LazyLoadEntity),
                    new { A = default(LazyLoadEntity) }.GetType(),
                    new { A = default(IEnumerable<LazyLoadEntity>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                    }
                },
                {
                    typeof(AnotherLazyLoadEntity),
                    new { A = default(AnotherLazyLoadEntity) }.GetType(),
                    new { A = default(IEnumerable<AnotherLazyLoadEntity>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                    }
                },
                {
                    typeof(LazyLoadWithGenericEntity),
                    new { A = default(LazyLoadWithGenericEntity) }.GetType(),
                    new { A = default(IEnumerable<LazyLoadWithGenericEntity>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                    }
                },
                {
                    typeof(EntityWithDependentAttribute),
                    new { A = default(EntityWithDependentAttribute) }.GetType(),
                    new { A = default(IEnumerable<EntityWithDependentAttribute>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                    }
                }
            };

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetAboveRelatedEntitiesTestCases))]
        public void GetAboveRelatedEntities_Should_Return_Above_Related(Type type, Type anonymousType, Type genericAnonymousType, HashSet<IEntityType> expected)
        {
            var iEntityType = GetIEntityType(type);
            var entitiesByIEntityType = _cachedDbContext.DependencyManager.GetAboveRelatedEntities(iEntityType);
            Assert.True(expected.SetEquals(entitiesByIEntityType));


            var entities = _cachedDbContext.DependencyManager.GetAboveRelatedEntities(type);
            Assert.True(expected.SetEquals(entities));


            var entitiesByAnonymousType = _cachedDbContext.DependencyManager.GetAboveRelatedEntities(anonymousType);
            Assert.True(expected.SetEquals(entitiesByAnonymousType));


            var entitiesByGenericAnonymousType = _cachedDbContext.DependencyManager.GetAboveRelatedEntities(genericAnonymousType);
            Assert.True(expected.SetEquals(entitiesByGenericAnonymousType));
        }




        public static TheoryData<Type, Type, Type, bool> GetHasLazyLoadTestCases()
        {
            var theoryData = new TheoryData<Type, Type, Type, bool>
            {
                {
                    typeof(NonLazyLoadEntity),
                    new { A = default(NonLazyLoadEntity) }.GetType(),
                    new { A = default(IEnumerable<NonLazyLoadEntity>) }.GetType(),
                    false
                },
                {
                    typeof(LazyLoadEntity),
                    new { A = default(LazyLoadEntity) }.GetType(),
                    new { A = default(IEnumerable<LazyLoadEntity>) }.GetType(),
                    true
                },
                {
                    typeof(AnotherLazyLoadEntity),
                    new { A = default(AnotherLazyLoadEntity) }.GetType(),
                    new { A = default(IEnumerable<AnotherLazyLoadEntity>) }.GetType(),
                    true
                },
                {
                    typeof(LazyLoadWithGenericEntity),
                    new { A = default(LazyLoadWithGenericEntity) }.GetType(),
                    new { A = default(IEnumerable<LazyLoadWithGenericEntity>) }.GetType(),
                    true
                },
                {
                    typeof(EntityWithDependentAttribute),
                    new { A = default(EntityWithDependentAttribute) }.GetType(),
                    new { A = default(IEnumerable<EntityWithDependentAttribute>) }.GetType(),
                    false
                },
            };

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetHasLazyLoadTestCases))]
        public void HasLazyLoad_Should_Return_HasLazyLoad(Type type, Type anonymousType, Type genericAnonymousType, bool expected)
        {
            var iEntityType = GetIEntityType(type);
            var lazyLoadByIEntityType = _cachedDbContext.DependencyManager.HasLazyLoad(iEntityType);
            Assert.Equal(expected, lazyLoadByIEntityType);


            var lazyLoadByType = _cachedDbContext.DependencyManager.HasLazyLoad(type);
            Assert.Equal(expected, lazyLoadByType);


            var lazyLoadByAnonymousType = _cachedDbContext.DependencyManager.HasLazyLoad(anonymousType);
            Assert.Equal(expected, lazyLoadByAnonymousType);


            var lazyLoadByGenericAnonymousType = _cachedDbContext.DependencyManager.HasLazyLoad(genericAnonymousType);
            Assert.Equal(expected, lazyLoadByGenericAnonymousType);
        }




        public static TheoryData<Type, Type, Type, HashSet<IEntityType>> GetAllRelatedEntitiesTestCases()
        {
            var theoryData = new TheoryData<Type, Type, Type, HashSet<IEntityType>>
            {
                {
                    typeof(NonLazyLoadEntity),
                    new { A = default(NonLazyLoadEntity) }.GetType(),
                    new { A = default(IEnumerable<NonLazyLoadEntity>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                    }
                },
                {
                    typeof(LazyLoadEntity),
                    new { A = default(LazyLoadEntity) }.GetType(),
                    new { A = default(IEnumerable<LazyLoadEntity>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                    }
                },
                {
                    typeof(AnotherLazyLoadEntity),
                    new { A = default(AnotherLazyLoadEntity) }.GetType(),
                    new { A = default(IEnumerable<AnotherLazyLoadEntity>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                    }
                },
                {
                    typeof(LazyLoadWithGenericEntity),
                    new { A = default(LazyLoadWithGenericEntity) }.GetType(),
                    new { A = default(IEnumerable<LazyLoadWithGenericEntity>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                    }
                },
                {
                    typeof(EntityWithDependentAttribute),
                    new { A = default(EntityWithDependentAttribute) }.GetType(),
                    new { A = default(IEnumerable<EntityWithDependentAttribute>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                    }
                },
            };

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetAllRelatedEntitiesTestCases))]
        public void GetAllRelatedEntities_Should_Return_Above_Related(Type type, Type anonymousType, Type genericAnonymousType, HashSet<IEntityType> expected)
        {
            var iEntityType = GetIEntityType(type);
            var entitiesByIEntityType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(iEntityType);
            Assert.True(expected.SetEquals(entitiesByIEntityType));

            var entities = _cachedDbContext.DependencyManager.GetAllRelatedEntities(type);
            Assert.True(expected.SetEquals(entities));


            var entitiesByAnonymousType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(anonymousType);
            Assert.True(expected.SetEquals(entitiesByAnonymousType));


            var entitiesByGenericAnonymousType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(genericAnonymousType);
            Assert.True(expected.SetEquals(entitiesByGenericAnonymousType));
        }

        private class TestDbContext : CachedDbContext
        {
            public TestDbContext(IDbQueryCacheStore dbQueryCacheStore) : base(dbQueryCacheStore)
            {
            }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseInMemoryDatabase("test").AddInterceptors(new DbStateInterceptor(new SqlServerQueryEntityExtractor()));
                base.OnConfiguring(optionsBuilder);
            }

            public DbSet<LazyLoadEntity> LazyLoadEntity { get; set; }
            public DbSet<NonLazyLoadEntity> NonLazyLoadEntity { get; set; }
            public DbSet<AnotherLazyLoadEntity> AnotherLazyLoadEntity { get; set; }
            public DbSet<LazyLoadWithGenericEntity> LazyLoadWithGenericEntity { get; set; }
            public DbSet<EntityWithDependentAttribute> EntityWithDependentAttribute { get; set; }
        }

        private class AnotherLazyLoadEntity
        {
            [Key]
            public int Id { get; set; }
            public string? StringData { get; set; }

            [ForeignKey(nameof(LazyLoadProp))]
            public int? LazyLoadPropId { get; set; }

            [ForeignKey(nameof(LazyLoadPropId))]
            public virtual LazyLoadEntity? LazyLoadProp { get; set; }
        }

        private class LazyLoadWithGenericEntity
        {
            [Key]
            public int Id { get; set; }
            public string? StringData { get; set; }

            public virtual ICollection<NonLazyLoadEntity>? LazyLoadGenericProp { get; set; }
        }

        private class LazyLoadEntity
        {
            [Key]
            public int Id { get; set; }
            public string? StringData { get; set; }

            [ForeignKey(nameof(LazyLoadProp))]
            public int? LazyLoadPropId { get; set; }

            [ForeignKey(nameof(LazyLoadPropId))]
            public virtual NonLazyLoadEntity? LazyLoadProp { get; set; }
        }

        private class NonLazyLoadEntity
        {
            [Key]
            public int Id { get; set; }

            public string? StringData { get; set; }
        }

        [DependentOnEntity(typeof(LazyLoadEntity))]
        private class EntityWithDependentAttribute
        {
            [Key]
            public int Id { get; set; }

            public string? StringData { get; set; }
        }
    }
}