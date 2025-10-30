using CachedEfCore.Context;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
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
using System.IO;

namespace CachedEfCore.DepencencyManager.Tests
{
    public class EntityDependencyManagerAllRelatedTests : EntityDependencyManagerTestBase
    {
        public record AllRelatedData
        {
            private AllRelatedData()
            {
            }

            public required Type Type { get; init; }
            public required Type AnonymousType { get; init; }
            public required Type GenericAnonymousType { get; init; }
            public required Type TupleLiteralType { get; init; }
            public required Type GenericTupleLiteralType { get; init; }

            public required HashSet<IEntityType> Expected { get; init; }

            public static AllRelatedData Create<T>(HashSet<IEntityType> expected)
            {
                return new AllRelatedData
                {
                    Type = typeof(T),
                    AnonymousType = new { A = default(T) }.GetType(),
                    GenericAnonymousType = new { A = default(IEnumerable<T>) }.GetType(),
                    TupleLiteralType = (first: default(T), sec: default(T)).GetType(),
                    GenericTupleLiteralType = (first: default(IEnumerable<T>), sec: default(IEnumerable<T>)).GetType(),
                    Expected = expected
                };
            }
        }


        public static TheoryData<AllRelatedData> GetAllRelatedEntitiesTestCases()
        {
            var theoryData = new TheoryData<AllRelatedData>
            {
                {
                    AllRelatedData.Create<NonLazyLoadEntity>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                    })
                },
                {
                    AllRelatedData.Create<LazyLoadEntity>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                    })
                },
                {
                    AllRelatedData.Create<AnotherLazyLoadEntity>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                    })
                },
                {
                    AllRelatedData.Create<LazyLoadWithGenericEntity>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                    })
                },
                {
                    AllRelatedData.Create<EntityWithDependentAttribute>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                    })
                },
            };

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetAllRelatedEntitiesTestCases))]
        public void GetAllRelatedEntities_Should_Return_All_Related(AllRelatedData allRelatedData)
        {
            var iEntityType = GetIEntityType(allRelatedData.Type);
            var entitiesByIEntityType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(iEntityType, true);
            Assert.True(allRelatedData.Expected.SetEquals(entitiesByIEntityType));


            var entities = _cachedDbContext.DependencyManager.GetAllRelatedEntities(allRelatedData.Type, true);
            Assert.True(allRelatedData.Expected.SetEquals(entities));


            var entitiesByAnonymousType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(allRelatedData.AnonymousType, true);
            Assert.True(allRelatedData.Expected.SetEquals(entitiesByAnonymousType));


            var entitiesByGenericAnonymousType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(allRelatedData.GenericAnonymousType, true);
            Assert.True(allRelatedData.Expected.SetEquals(entitiesByGenericAnonymousType));


            var entitiesByTupleLiteralType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(allRelatedData.TupleLiteralType, true);
            Assert.True(allRelatedData.Expected.SetEquals(entitiesByTupleLiteralType));


            var entitiesByGenericTupleLiteralType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(allRelatedData.GenericTupleLiteralType, true);
            Assert.True(allRelatedData.Expected.SetEquals(entitiesByGenericTupleLiteralType));
        }
    }
}