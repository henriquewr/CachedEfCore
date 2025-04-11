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

namespace CachedEfCore.DepencencyManager.Tests
{
    [TestFixture]
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

        private static IEnumerable<TestCaseData> GetUnderRelatedEntitiesTestCases()
        {
            yield return new TestCaseData(typeof(NonLazyLoadEntity),
                new { A = default(NonLazyLoadEntity) }.GetType(),
                new { A = default(IEnumerable<NonLazyLoadEntity>) }.GetType(),
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(NonLazyLoadEntity)),
                });

            yield return new TestCaseData(typeof(LazyLoadEntity),
                new { A = default(LazyLoadEntity) }.GetType(),
                new { A = default(IEnumerable<LazyLoadEntity>) }.GetType(),
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(LazyLoadEntity)),
                    GetIEntityType(typeof(NonLazyLoadEntity)),
                });

            yield return new TestCaseData(typeof(AnotherLazyLoadEntity),
                new { A = default(AnotherLazyLoadEntity) }.GetType(),
                new { A = default(IEnumerable<AnotherLazyLoadEntity>) }.GetType(),
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(AnotherLazyLoadEntity)),
                    GetIEntityType(typeof(LazyLoadEntity)),
                    GetIEntityType(typeof(NonLazyLoadEntity)),
                });

            yield return new TestCaseData(typeof(LazyLoadWithGenericEntity),
                new { A = default(LazyLoadWithGenericEntity) }.GetType(),
                new { A = default(IEnumerable<LazyLoadWithGenericEntity>) }.GetType(),
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                    GetIEntityType(typeof(NonLazyLoadEntity)),
                });

            yield return new TestCaseData(typeof(EntityWithDependentAttribute), 
                new { A = default(EntityWithDependentAttribute) }.GetType(),
                new { A = default(IEnumerable<EntityWithDependentAttribute>) }.GetType(),
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(EntityWithDependentAttribute)),
                    GetIEntityType(typeof(NonLazyLoadEntity)),
                    GetIEntityType(typeof(LazyLoadEntity)),
                });
        }

        [TestCaseSource(nameof(GetUnderRelatedEntitiesTestCases))]
        public void GetUnderRelatedEntities_Should_Return_Under_Related(Type type, Type anonymousType, Type genericAnonymousType, HashSet<IEntityType> expected)
        {
            var iEntityType = GetIEntityType(type);
            var entitiesByIEntityType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(iEntityType);
            Assert.That(entitiesByIEntityType, Is.EquivalentTo(expected));

            var entities = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(type);
            Assert.That(entities, Is.EquivalentTo(expected));


            var entitiesByAnonymousType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(anonymousType);
            Assert.That(entitiesByAnonymousType, Is.EquivalentTo(expected));


            var entitiesByGenericAnonymousType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(genericAnonymousType);
            Assert.That(entitiesByGenericAnonymousType, Is.EquivalentTo(expected));

        }




        private static IEnumerable<TestCaseData> GetAboveRelatedEntitiesTestCases()
        {
            yield return new TestCaseData(typeof(NonLazyLoadEntity),
                new { A = default(NonLazyLoadEntity) }.GetType(),
                new { A = default(IEnumerable<NonLazyLoadEntity>) }.GetType(),
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(AnotherLazyLoadEntity)),
                    GetIEntityType(typeof(LazyLoadEntity)),
                    GetIEntityType(typeof(NonLazyLoadEntity)),
                    GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                    GetIEntityType(typeof(EntityWithDependentAttribute)),
                });

            yield return new TestCaseData(typeof(LazyLoadEntity), 
                new { A = default(LazyLoadEntity) }.GetType(), 
                new { A = default(IEnumerable<LazyLoadEntity>) }.GetType(), 
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(AnotherLazyLoadEntity)),
                    GetIEntityType(typeof(LazyLoadEntity)),
                    GetIEntityType(typeof(EntityWithDependentAttribute)),
                });

            yield return new TestCaseData(typeof(AnotherLazyLoadEntity),
                new { A = default(AnotherLazyLoadEntity) }.GetType(),
                new { A = default(IEnumerable<AnotherLazyLoadEntity>) }.GetType(),
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(AnotherLazyLoadEntity)),
                });

            yield return new TestCaseData(typeof(LazyLoadWithGenericEntity),
                new { A = default(LazyLoadWithGenericEntity) }.GetType(),
                new { A = default(IEnumerable<LazyLoadWithGenericEntity>) }.GetType(),
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(AnotherLazyLoadEntity)),
                    GetIEntityType(typeof(LazyLoadEntity)),
                    GetIEntityType(typeof(NonLazyLoadEntity)),
                    GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                    GetIEntityType(typeof(EntityWithDependentAttribute)),
                });

            yield return new TestCaseData(typeof(EntityWithDependentAttribute),
                new { A = default(EntityWithDependentAttribute) }.GetType(),
                new { A = default(IEnumerable<EntityWithDependentAttribute>) }.GetType(),
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(EntityWithDependentAttribute)),
                });
        }

        [TestCaseSource(nameof(GetAboveRelatedEntitiesTestCases))]
        public void GetAboveRelatedEntities_Should_Return_Above_Related(Type type, Type anonymousType, Type genericAnonymousType, HashSet<IEntityType> expected)
        {
            var iEntityType = GetIEntityType(type);
            var entitiesByIEntityType = _cachedDbContext.DependencyManager.GetAboveRelatedEntities(iEntityType);
            Assert.That(entitiesByIEntityType, Is.EquivalentTo(expected));


            var entities = _cachedDbContext.DependencyManager.GetAboveRelatedEntities(type);
            Assert.That(entities, Is.EquivalentTo(expected));


            var entitiesByAnonymousType = _cachedDbContext.DependencyManager.GetAboveRelatedEntities(anonymousType);
            Assert.That(entitiesByAnonymousType, Is.EquivalentTo(expected));


            var entitiesByGenericAnonymousType = _cachedDbContext.DependencyManager.GetAboveRelatedEntities(genericAnonymousType);
            Assert.That(entitiesByGenericAnonymousType, Is.EquivalentTo(expected));
        }




        private static IEnumerable<TestCaseData> GetHasLazyLoadTestCases()
        {
            yield return new TestCaseData(typeof(NonLazyLoadEntity), 
                new { A = default(NonLazyLoadEntity) }.GetType(), 
                new { A = default(IEnumerable<NonLazyLoadEntity>) }.GetType(), 
                false);

            yield return new TestCaseData(typeof(LazyLoadEntity),
                new { A = default(LazyLoadEntity) }.GetType(), 
                new { A = default(IEnumerable<LazyLoadEntity>) }.GetType(), 
                true);

            yield return new TestCaseData(typeof(AnotherLazyLoadEntity), 
                new { A = default(AnotherLazyLoadEntity) }.GetType(), 
                new { A = default(IEnumerable<AnotherLazyLoadEntity>) }.GetType(), 
                true);

            yield return new TestCaseData(typeof(LazyLoadWithGenericEntity), 
                new { A = default(LazyLoadWithGenericEntity) }.GetType(), 
                new { A = default(IEnumerable<LazyLoadWithGenericEntity>) }.GetType(), 
                true);    
            
            yield return new TestCaseData(typeof(EntityWithDependentAttribute), 
                new { A = default(EntityWithDependentAttribute) }.GetType(), 
                new { A = default(IEnumerable<EntityWithDependentAttribute>) }.GetType(), 
                false);
        }

        [TestCaseSource(nameof(GetHasLazyLoadTestCases))]
        public void HasLazyLoad_Should_Return_HasLazyLoad(Type type, Type anonymousType, Type genericAnonymousType, bool expected)
        {
            var iEntityType = GetIEntityType(type);
            var lazyLoadByIEntityType = _cachedDbContext.DependencyManager.HasLazyLoad(iEntityType);
            Assert.That(lazyLoadByIEntityType, Is.EqualTo(expected));

            var lazyLoadByType = _cachedDbContext.DependencyManager.HasLazyLoad(type);
            Assert.That(lazyLoadByType, Is.EqualTo(expected));


            var lazyLoadByAnonymousType = _cachedDbContext.DependencyManager.HasLazyLoad(anonymousType);
            Assert.That(lazyLoadByAnonymousType, Is.EqualTo(expected));


            var lazyLoadByGenericAnonymousType = _cachedDbContext.DependencyManager.HasLazyLoad(genericAnonymousType);
            Assert.That(lazyLoadByGenericAnonymousType, Is.EqualTo(expected));
        }




        private static IEnumerable<TestCaseData> GetAllRelatedEntitiesTestCases()
        {
            yield return new TestCaseData(typeof(NonLazyLoadEntity),
                new { A = default(NonLazyLoadEntity) }.GetType(),
                new { A = default(IEnumerable<NonLazyLoadEntity>) }.GetType(),
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(AnotherLazyLoadEntity)),
                    GetIEntityType(typeof(LazyLoadEntity)),
                    GetIEntityType(typeof(NonLazyLoadEntity)),
                    GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                    GetIEntityType(typeof(EntityWithDependentAttribute)),
                });

            yield return new TestCaseData(typeof(LazyLoadEntity),
                new { A = default(LazyLoadEntity) }.GetType(),
                new { A = default(IEnumerable<LazyLoadEntity>) }.GetType(),
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(AnotherLazyLoadEntity)),
                    GetIEntityType(typeof(LazyLoadEntity)),
                    GetIEntityType(typeof(NonLazyLoadEntity)),
                    GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                    GetIEntityType(typeof(EntityWithDependentAttribute)),
                });

            yield return new TestCaseData(typeof(AnotherLazyLoadEntity),
                new { A = default(AnotherLazyLoadEntity) }.GetType(),
                new { A = default(IEnumerable<AnotherLazyLoadEntity>) }.GetType(),
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(AnotherLazyLoadEntity)),
                    GetIEntityType(typeof(LazyLoadEntity)),
                    GetIEntityType(typeof(NonLazyLoadEntity)),
                    GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                    GetIEntityType(typeof(EntityWithDependentAttribute)),
                });

            yield return new TestCaseData(typeof(LazyLoadWithGenericEntity),
                new { A = default(LazyLoadWithGenericEntity) }.GetType(),
                new { A = default(IEnumerable<LazyLoadWithGenericEntity>) }.GetType(),
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(AnotherLazyLoadEntity)),
                    GetIEntityType(typeof(LazyLoadEntity)),
                    GetIEntityType(typeof(NonLazyLoadEntity)),
                    GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                    GetIEntityType(typeof(EntityWithDependentAttribute)),
                });

            yield return new TestCaseData(typeof(EntityWithDependentAttribute),
                new { A = default(EntityWithDependentAttribute) }.GetType(),
                new { A = default(IEnumerable<EntityWithDependentAttribute>) }.GetType(),
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(AnotherLazyLoadEntity)),
                    GetIEntityType(typeof(LazyLoadEntity)),
                    GetIEntityType(typeof(NonLazyLoadEntity)),
                    GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                    GetIEntityType(typeof(EntityWithDependentAttribute)),
                });
        }

        [TestCaseSource(nameof(GetAllRelatedEntitiesTestCases))]
        public void GetAllRelatedEntities_Should_Return_Above_Related(Type type, Type anonymousType, Type genericAnonymousType, HashSet<IEntityType> expected)
        {
            var iEntityType = GetIEntityType(type);
            var entitiesByIEntityType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(iEntityType);
            Assert.That(entitiesByIEntityType, Is.EquivalentTo(expected));


            var entities = _cachedDbContext.DependencyManager.GetAllRelatedEntities(type);
            Assert.That(entities, Is.EquivalentTo(expected));


            var entitiesByAnonymousType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(anonymousType);
            Assert.That(entitiesByAnonymousType, Is.EquivalentTo(expected));


            var entitiesByGenericAnonymousType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(genericAnonymousType);
            Assert.That(entitiesByGenericAnonymousType, Is.EquivalentTo(expected));
        }


        [OneTimeTearDown]
        public void Dispose()
        {
            _cachedDbContext.Dispose();
        }

        private class TestDbContext : CachedDbContext
        {
            public TestDbContext(IDbQueryCacheStore dbQueryCacheStore) : base(dbQueryCacheStore)
            {
            }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseInMemoryDatabase("test").AddInterceptors(new DbStateInterceptor(new SqlQueryEntityExtractor()));
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