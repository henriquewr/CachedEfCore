using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using Xunit;

namespace CachedEfCore.DepencencyManager.Tests
{
    public class EntityDependencyManagerUpperRelatedTests : EntityDependencyManagerTestBase
    {
        public record UpperRelatedData
        {
            private UpperRelatedData()
            {
            }

            public required Type Type { get; init; }
            public required Type AnonymousType { get; init; }
            public required Type GenericAnonymousType { get; init; }
            public required Type TupleLiteralType { get; init; }
            public required Type GenericTupleLiteralType { get; init; }
            public required Type ProxyType { get; init; }

            public required HashSet<IEntityType> Expected { get; init; }

            public static UpperRelatedData Create<T>(HashSet<IEntityType> expected)
            {
                var proxyType = _cachedDbContext.CreateProxy<T>()!.GetType();

                return new UpperRelatedData
                {
                    Type = typeof(T),
                    AnonymousType = new { A = default(T) }.GetType(),
                    GenericAnonymousType = new { A = default(IEnumerable<T>) }.GetType(),
                    TupleLiteralType = (first: default(T), sec: default(T)).GetType(),
                    GenericTupleLiteralType = (first: default(IEnumerable<T>), sec: default(IEnumerable<T>)).GetType(),
                    ProxyType = proxyType,
                    Expected = expected,
                };
            }
        }

        public static TheoryData<UpperRelatedData> GetUpperRelatedEntitiesTestCases()
        {
            var theoryData = new TheoryData<UpperRelatedData>
            {
                {
                    UpperRelatedData.Create<EntityManyToMany>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToMany)),
                        GetIEntityType(typeof(OtherEntityManyToMany)),
                        GetIEntityType(typeof(EntityManyToManyOtherEntityManyToMany)),
                    })
                },
                {
                    UpperRelatedData.Create<EntityManyToManyOtherEntityManyToMany>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToMany)),
                        GetIEntityType(typeof(OtherEntityManyToMany)),
                        GetIEntityType(typeof(EntityManyToManyOtherEntityManyToMany)),
                    })
                },
                {
                    UpperRelatedData.Create<EntityManyToManyWithoutNavigation>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigation)),
                        GetIEntityType(typeof(OtherEntityManyToManyWithoutNavigation)),
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation)),
                    })
                },
                {
                    UpperRelatedData.Create<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigation)),
                        GetIEntityType(typeof(OtherEntityManyToManyWithoutNavigation)),
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation)),
                    })
                },
                {
                    UpperRelatedData.Create<EntityManyToManySkipNavigation>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToManySkipNavigation)),
                        GetIEntityType(typeof(OtherEntityManyToManySkipNavigation)),
                        GetIEntityType(EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName),
                    })
                },
                {
                    UpperRelatedData.Create<NonLazyLoadEntity>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                    })
                },
                {
                    UpperRelatedData.Create<LazyLoadEntity>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                    })
                },
                {
                    UpperRelatedData.Create<AnotherLazyLoadEntity>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                    })
                },
                {
                    UpperRelatedData.Create<LazyLoadWithGenericEntity>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                    })
                },
                {
                    UpperRelatedData.Create<EntityWithDependentAttribute>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                    })
                }
            };

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetUpperRelatedEntitiesTestCases))]
        public void GetUpperRelatedEntities_Should_Return_Upper_Related(UpperRelatedData upperRelatedData)
        {
            var iEntityType = GetIEntityType(upperRelatedData.Type);
            var entitiesByIEntityType = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(iEntityType);
            Assert.True(upperRelatedData.Expected.SetEquals(entitiesByIEntityType));


            var entities = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(upperRelatedData.Type);
            Assert.True(upperRelatedData.Expected.SetEquals(entities));


            var entitiesByAnonymousType = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(upperRelatedData.AnonymousType);
            Assert.True(upperRelatedData.Expected.SetEquals(entitiesByAnonymousType));


            var entitiesByGenericAnonymousType = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(upperRelatedData.GenericAnonymousType);
            Assert.True(upperRelatedData.Expected.SetEquals(entitiesByGenericAnonymousType));


            var entitiesByTupleLiteralType = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(upperRelatedData.TupleLiteralType);
            Assert.True(upperRelatedData.Expected.SetEquals(entitiesByTupleLiteralType));


            var entitiesByGenericTupleLiteralType = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(upperRelatedData.GenericTupleLiteralType);
            Assert.True(upperRelatedData.Expected.SetEquals(entitiesByGenericTupleLiteralType));


            var entitiesByProxyType = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(upperRelatedData.ProxyType);
            Assert.True(upperRelatedData.Expected.SetEquals(entitiesByProxyType));
        }


        public static TheoryData<IEntityType, HashSet<IEntityType>> GetUpperRelatedEntitiesGenByEfTestCases()
        {
            var theoryData = new TheoryData<IEntityType, HashSet<IEntityType>>
            {
                {
                    GetIEntityType(EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToManySkipNavigation)),
                        GetIEntityType(typeof(OtherEntityManyToManySkipNavigation)),
                        GetIEntityType(EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName),
                    }
                },
            };

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetUpperRelatedEntitiesGenByEfTestCases))]
        public void GetUpperRelatedEntities_Should_Return_Upper_Related_When_Is_Generated_By_EF(IEntityType iEntityType, HashSet<IEntityType> expected)
        {
            var entitiesByIEntityType = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(iEntityType);
            Assert.True(expected.SetEquals(entitiesByIEntityType));
        }
    }
}
