using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using Xunit;

namespace CachedEfCore.DepencencyManager.Tests
{
    public class EntityDependencyManagerUnderRelatedTests : EntityDependencyManagerTestBase
    {
        public record UnderRelatedData
        {
            private UnderRelatedData()
            {
            }

            public required Type Type { get; init; }
            public required Type AnonymousType { get; init; }
            public required Type GenericAnonymousType { get; init; }
            public required Type TupleLiteralType { get; init; }
            public required Type GenericTupleLiteralType { get; init; }
            public required Type ProxyType { get; init; }

            public required HashSet<IEntityType> Expected { get; init; }

            public static UnderRelatedData Create<T>(HashSet<IEntityType> expected)
            {
                var proxyType = _cachedDbContext.CreateProxy<T>()!.GetType();

                return new UnderRelatedData
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

        public static TheoryData<UnderRelatedData> GetUnderRelatedEntitiesNotIncludingFksTestCases()
        {
            var theoryData = new TheoryData<UnderRelatedData>
            {
                {
                    UnderRelatedData.Create<EntityManyToMany>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToMany)),
                        GetIEntityType(typeof(OtherEntityManyToMany)),
                        GetIEntityType(typeof(EntityManyToManyOtherEntityManyToMany)),
                    })
                },
                {
                    UnderRelatedData.Create<EntityManyToManyWithoutNavigation>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigation)),
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation)),
                    })
                },
                {
                    UnderRelatedData.Create<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation)),
                    })
                },
                {
                    UnderRelatedData.Create<EntityManyToManySkipNavigation>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToManySkipNavigation)),
                        GetIEntityType(typeof(OtherEntityManyToManySkipNavigation)),
                        GetIEntityType(EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName),
                    })
                },

                {
                    UnderRelatedData.Create<NonLazyLoadEntity>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                    })
                },
                {
                    UnderRelatedData.Create<LazyLoadEntity>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                    })
                },
                {
                    UnderRelatedData.Create<AnotherLazyLoadEntity>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                    })
                },
                {
                    UnderRelatedData.Create<LazyLoadWithGenericEntity>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                    })
                },
                {
                    UnderRelatedData.Create<EntityWithDependentAttribute>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                    })
                }
            };

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetUnderRelatedEntitiesNotIncludingFksTestCases))]
        public void GetUnderRelatedEntities_Should_Return_Under_Related_Not_IncludingFks(UnderRelatedData underRelatedData)
        {
            var iEntityType = GetIEntityType(underRelatedData.Type);
            var entitiesByIEntityType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(iEntityType, false);
            Assert.True(underRelatedData.Expected.SetEquals(entitiesByIEntityType));

            var entities = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.Type, false);
            Assert.True(underRelatedData.Expected.SetEquals(entities));

            var entitiesByAnonymousType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.AnonymousType, false);
            Assert.True(underRelatedData.Expected.SetEquals(entitiesByAnonymousType));

            var entitiesByGenericAnonymousType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.GenericAnonymousType, false);
            Assert.True(underRelatedData.Expected.SetEquals(entitiesByGenericAnonymousType));

            var entitiesTupleLiteralType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.TupleLiteralType, false);
            Assert.True(underRelatedData.Expected.SetEquals(entitiesTupleLiteralType));

            var entitiesByGenericTupleLiteralType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.GenericTupleLiteralType, false);
            Assert.True(underRelatedData.Expected.SetEquals(entitiesByGenericTupleLiteralType));

            var entitiesByProxyType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.ProxyType, false);
            Assert.True(underRelatedData.Expected.SetEquals(entitiesByProxyType));
        }



        public static TheoryData<UnderRelatedData> GetUnderRelatedEntitiesIncludingFksTestCases()
        {
            var theoryData = new TheoryData<UnderRelatedData>
            {
                {
                    UnderRelatedData.Create<EntityManyToMany>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToMany)),
                        GetIEntityType(typeof(OtherEntityManyToMany)),
                        GetIEntityType(typeof(EntityManyToManyOtherEntityManyToMany)),
                    })
                },
                {
                    UnderRelatedData.Create<EntityManyToManyOtherEntityManyToMany>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToMany)),
                        GetIEntityType(typeof(OtherEntityManyToMany)),
                        GetIEntityType(typeof(EntityManyToManyOtherEntityManyToMany)),
                    })
                },
                {
                    UnderRelatedData.Create<EntityManyToManyWithoutNavigation>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigation)),
                        GetIEntityType(typeof(OtherEntityManyToManyWithoutNavigation)),
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation)),
                    })
                },
                {
                    UnderRelatedData.Create<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigation)),
                        GetIEntityType(typeof(OtherEntityManyToManyWithoutNavigation)),
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation)),
                    })
                },

                {
                    UnderRelatedData.Create<EntityManyToManySkipNavigation>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToManySkipNavigation)),
                        GetIEntityType(typeof(OtherEntityManyToManySkipNavigation)),
                        GetIEntityType(EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName),
                    })
                },
                {
                    UnderRelatedData.Create<NonLazyLoadEntity>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                    })
                },
                {
                    UnderRelatedData.Create<LazyLoadEntity>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                    })
                },
                {
                    UnderRelatedData.Create<AnotherLazyLoadEntity>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(AnotherLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                    })
                },
                {
                    UnderRelatedData.Create<LazyLoadWithGenericEntity>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                    })
                },
                {
                    UnderRelatedData.Create<EntityWithDependentAttribute>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityWithDependentAttribute)),
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                    })
                }
            };

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetUnderRelatedEntitiesIncludingFksTestCases))]
        public void GetUnderRelatedEntities_Should_Return_Under_Related_When_IncludingFks(UnderRelatedData underRelatedData)
        {
            var iEntityType = GetIEntityType(underRelatedData.Type);
            var entitiesByIEntityTypeIncludingFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(iEntityType, true);
            Assert.True(underRelatedData.Expected.SetEquals(entitiesByIEntityTypeIncludingFks));

            var entitiesIncludingFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.Type, true);
            Assert.True(underRelatedData.Expected.SetEquals(entitiesIncludingFks));

            var entitiesByAnonymousTypeIncludingFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.AnonymousType, true);
            Assert.True(underRelatedData.Expected.SetEquals(entitiesByAnonymousTypeIncludingFks));

            var entitiesByGenericAnonymousTypeIncludingFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.GenericAnonymousType, true);
            Assert.True(underRelatedData.Expected.SetEquals(entitiesByGenericAnonymousTypeIncludingFks));

            var entitiesByTupleLiteralTypeFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.TupleLiteralType, true);
            Assert.True(underRelatedData.Expected.SetEquals(entitiesByTupleLiteralTypeFks));

            var entitiesByGenericTupleLiteralTypeIncludingFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.GenericTupleLiteralType, true);
            Assert.True(underRelatedData.Expected.SetEquals(entitiesByGenericTupleLiteralTypeIncludingFks));

            var entitiesByProxyType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.ProxyType, true);
            Assert.True(underRelatedData.Expected.SetEquals(entitiesByProxyType));
        }



        public static TheoryData<IEntityType, bool, HashSet<IEntityType>> GetUnderRelatedEntitiesGeneratedByEfTestCases()
        {
            var theoryData = new TheoryData<IEntityType, bool, HashSet<IEntityType>>
            {
                {
                    GetIEntityType(EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName),
                    false,
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName),
                    }
                },
                {
                    GetIEntityType(EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName),
                    true,
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
        [MemberData(nameof(GetUnderRelatedEntitiesGeneratedByEfTestCases))]
        public void GetUnderRelatedEntities_Should_Return_Under_Related_Generated_By_EF(IEntityType iEntityType, bool includingRelatedInFks, HashSet<IEntityType> expected)
        {
            var entitiesByIEntityTypeIncludingFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(iEntityType, includingRelatedInFks);
            Assert.True(expected.SetEquals(entitiesByIEntityTypeIncludingFks));
        }
    }
}
