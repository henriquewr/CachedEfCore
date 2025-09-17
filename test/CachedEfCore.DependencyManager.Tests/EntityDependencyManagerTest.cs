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
    public class EntityDependencyDiscover
    {
        private static readonly TestDbContext _cachedDbContext = new TestDbContext(new DbQueryCacheStore(new MemoryCache(new MemoryCacheOptions())));
        public EntityDependencyDiscover()
        {
        }

        private static IEntityType GetIEntityType(Type entityType)
        {
            return _cachedDbContext.Model.FindEntityType(entityType) ?? throw new InvalidDataException();
        }

        private static IEntityType GetIEntityType(string name)
        {
            return _cachedDbContext.Model.FindEntityType(name) ?? throw new InvalidDataException();
        }

        public static TheoryData<Type, Type, Type, HashSet<IEntityType>> GetUnderRelatedEntitiesNotIncludingFksTestCases()
        {
            var theoryData = new TheoryData<Type, Type, Type, HashSet <IEntityType>>
            {
                {
                    typeof(EntityManyToMany),
                    new { A = default(EntityManyToMany) }.GetType(),
                    new { A = default(IEnumerable<EntityManyToMany>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToMany)),
                        GetIEntityType(typeof(OtherEntityManyToMany)),
                        GetIEntityType(typeof(EntityManyToManyOtherEntityManyToMany)),
                    }
                },
                {
                    typeof(EntityManyToManyWithoutNavigation),
                    new { A = default(EntityManyToManyWithoutNavigation) }.GetType(),
                    new { A = default(IEnumerable<EntityManyToManyWithoutNavigation>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigation)),
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation)),
                    }
                },
                {
                    typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation),
                    new { A = default(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation) }.GetType(),
                    new { A = default(IEnumerable<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation)),
                    }
                },
                {
                    typeof(EntityManyToManySkipNavigation),
                    new { A = default(EntityManyToManySkipNavigation) }.GetType(),
                    new { A = default(IEnumerable<EntityManyToManySkipNavigation>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToManySkipNavigation)),
                        GetIEntityType(typeof(OtherEntityManyToManySkipNavigation)),
                        GetIEntityType(EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName),
                    }
                },

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
        [MemberData(nameof(GetUnderRelatedEntitiesNotIncludingFksTestCases))]
        public void GetUnderRelatedEntities_Should_Return_Under_Related_Not_IncludingFks(Type type, Type anonymousType, Type genericAnonymousType, HashSet<IEntityType> expected)
        {
            var iEntityType = GetIEntityType(type);
            var entitiesByIEntityType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(iEntityType, false);
            Assert.True(expected.SetEquals(entitiesByIEntityType));

            var entities = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(type, false);
            Assert.True(expected.SetEquals(entities));

            var entitiesByAnonymousType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(anonymousType, false);
            Assert.True(expected.SetEquals(entitiesByAnonymousType));

            var entitiesByGenericAnonymousType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(genericAnonymousType, false);
            Assert.True(expected.SetEquals(entitiesByGenericAnonymousType));
        }



        public static TheoryData<Type, Type, Type, HashSet<IEntityType>> GetUnderRelatedEntitiesIncludingFksTestCases()
        {
            var theoryData = new TheoryData<Type, Type, Type, HashSet<IEntityType>>
            {
                {
                    typeof(EntityManyToMany),
                    new { A = default(EntityManyToMany) }.GetType(),
                    new { A = default(IEnumerable<EntityManyToMany>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToMany)),
                        GetIEntityType(typeof(OtherEntityManyToMany)),
                        GetIEntityType(typeof(EntityManyToManyOtherEntityManyToMany)),
                    }
                },
                {
                    typeof(EntityManyToManyOtherEntityManyToMany),
                    new { A = default(EntityManyToMany) }.GetType(),
                    new { A = default(IEnumerable<EntityManyToMany>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToMany)),
                        GetIEntityType(typeof(OtherEntityManyToMany)),
                        GetIEntityType(typeof(EntityManyToManyOtherEntityManyToMany)),
                    }
                },
                {
                    typeof(EntityManyToManyWithoutNavigation),
                    new { A = default(EntityManyToManyWithoutNavigation) }.GetType(),
                    new { A = default(IEnumerable<EntityManyToManyWithoutNavigation>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigation)),
                        GetIEntityType(typeof(OtherEntityManyToManyWithoutNavigation)),
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation)),
                    }
                },
                {
                    typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation),
                    new { A = default(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation) }.GetType(),
                    new { A = default(IEnumerable<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigation)),
                        GetIEntityType(typeof(OtherEntityManyToManyWithoutNavigation)),
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation)),
                    }
                },

                {
                    typeof(EntityManyToManySkipNavigation),
                    new { A = default(EntityManyToManySkipNavigation) }.GetType(),
                    new { A = default(IEnumerable<EntityManyToManySkipNavigation>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToManySkipNavigation)),
                        GetIEntityType(typeof(OtherEntityManyToManySkipNavigation)),
                        GetIEntityType(EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName),
                    }
                },
                {
                    typeof(NonLazyLoadEntity),
                    new { A = default(NonLazyLoadEntity) }.GetType(),
                    new { A = default(IEnumerable<NonLazyLoadEntity>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(NonLazyLoadEntity)),
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
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
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
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
                        GetIEntityType(typeof(LazyLoadWithGenericEntity)),
                    }
                }
            };

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetUnderRelatedEntitiesIncludingFksTestCases))]
        public void GetUnderRelatedEntities_Should_Return_Under_Related_When_IncludingFks(Type type, Type anonymousType, Type genericAnonymousType, HashSet<IEntityType> expected)
        {
            var iEntityType = GetIEntityType(type);
            var entitiesByIEntityTypeIncludingFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(iEntityType, true);
            Assert.True(expected.SetEquals(entitiesByIEntityTypeIncludingFks));

            var entitiesIncludingFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(type, true);
            Assert.True(expected.SetEquals(entitiesIncludingFks));

            var entitiesByAnonymousTypeIncludingFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(anonymousType, true);
            Assert.True(expected.SetEquals(entitiesByAnonymousTypeIncludingFks));

            var entitiesByGenericAnonymousTypeIncludingFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(genericAnonymousType, true);
            Assert.True(expected.SetEquals(entitiesByGenericAnonymousTypeIncludingFks));
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



        public static TheoryData<Type, Type, Type, HashSet<IEntityType>> GetUpperRelatedEntitiesTestCases()
        {
            var theoryData = new TheoryData<Type, Type, Type, HashSet<IEntityType>>
            {
                {
                    typeof(EntityManyToMany),
                    new { A = default(EntityManyToMany) }.GetType(),
                    new { A = default(IEnumerable<EntityManyToMany>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToMany)),
                        GetIEntityType(typeof(OtherEntityManyToMany)),
                        GetIEntityType(typeof(EntityManyToManyOtherEntityManyToMany)),
                    }
                },
                {
                    typeof(EntityManyToManyOtherEntityManyToMany),
                    new { A = default(EntityManyToMany) }.GetType(),
                    new { A = default(IEnumerable<EntityManyToMany>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToMany)),
                        GetIEntityType(typeof(OtherEntityManyToMany)),
                        GetIEntityType(typeof(EntityManyToManyOtherEntityManyToMany)),
                    }
                },
                {
                    typeof(EntityManyToManyWithoutNavigation),
                    new { A = default(EntityManyToManyWithoutNavigation) }.GetType(),
                    new { A = default(IEnumerable<EntityManyToManyWithoutNavigation>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigation)),
                        GetIEntityType(typeof(OtherEntityManyToManyWithoutNavigation)),
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation)),
                    }
                },
                {
                    typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation),
                    new { A = default(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation) }.GetType(),
                    new { A = default(IEnumerable<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigation)),
                        GetIEntityType(typeof(OtherEntityManyToManyWithoutNavigation)),
                        GetIEntityType(typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation)),
                    }
                },
                {
                    typeof(EntityManyToManySkipNavigation),
                    new { A = default(EntityManyToManySkipNavigation) }.GetType(),
                    new { A = default(IEnumerable<EntityManyToManySkipNavigation>) }.GetType(),
                    new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(EntityManyToManySkipNavigation)),
                        GetIEntityType(typeof(OtherEntityManyToManySkipNavigation)),
                        GetIEntityType(EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName),
                    }
                },
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
        [MemberData(nameof(GetUpperRelatedEntitiesTestCases))]
        public void GetUpperRelatedEntities_Should_Return_Upper_Related(Type type, Type anonymousType, Type genericAnonymousType, HashSet<IEntityType> expected)
        {
            var iEntityType = GetIEntityType(type);
            var entitiesByIEntityType = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(iEntityType);
            Assert.True(expected.SetEquals(entitiesByIEntityType));


            var entities = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(type);
            Assert.True(expected.SetEquals(entities));


            var entitiesByAnonymousType = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(anonymousType);
            Assert.True(expected.SetEquals(entitiesByAnonymousType));


            var entitiesByGenericAnonymousType = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(genericAnonymousType);
            Assert.True(expected.SetEquals(entitiesByGenericAnonymousType));
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
        public void GetAllRelatedEntities_Should_Return_All_Related(Type type, Type anonymousType, Type genericAnonymousType, HashSet<IEntityType> expected)
        {
            var iEntityType = GetIEntityType(type);
            var entitiesByIEntityType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(iEntityType, true);
            Assert.True(expected.SetEquals(entitiesByIEntityType));

            var entities = _cachedDbContext.DependencyManager.GetAllRelatedEntities(type, true);
            Assert.True(expected.SetEquals(entities));


            var entitiesByAnonymousType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(anonymousType, true);
            Assert.True(expected.SetEquals(entitiesByAnonymousType));


            var entitiesByGenericAnonymousType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(genericAnonymousType, true);
            Assert.True(expected.SetEquals(entitiesByGenericAnonymousType));
        }


        private class TestDbContext : CachedDbContext
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




        private class EntityManyToMany
        {
            [Key]
            public int Id { get; set; }

            public virtual ICollection<EntityManyToManyOtherEntityManyToMany>? EntityManyToManyOtherEntityManyToMany { get; set; }
        }

        private class EntityManyToManyOtherEntityManyToMany
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

        private class OtherEntityManyToMany
        {
            [Key]
            public int Id { get; set; }

            public virtual ICollection<EntityManyToManyOtherEntityManyToMany>? EntityManyToManyOtherEntityManyToMany { get; set; }
        }




        private class EntityManyToManyWithoutNavigation
        {
            [Key]
            public int Id { get; set; }

            public virtual ICollection<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation>? EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation { get; set; }
        }

        private class EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation
        {
            [Key]
            public int Id { get; set; }

            public int? EntityManyToManyWithoutNavigationId { get; set; }

            public int? OtherEntityManyToManyWithoutNavigationId { get; set; }
        }

        private class OtherEntityManyToManyWithoutNavigation
        {
            [Key]
            public int Id { get; set; }

            public virtual ICollection<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation>? EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation { get; set; }
        }




        private class EntityManyToManySkipNavigation
        {
            [Key]
            public int Id { get; set; }

            public virtual ICollection<OtherEntityManyToManySkipNavigation>? OtherEntityManyToManySkipNavigation { get; set; }
        }

        private static class EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation
        {
            public const string TableName = "EntityManyToManySkipNavigation_OtherEntityManyToManySkipNavigation";
        }

        private class OtherEntityManyToManySkipNavigation
        {
            [Key]
            public int Id { get; set; }

            public virtual ICollection<EntityManyToManySkipNavigation>? EntityManyToManySkipNavigation { get; set; }
        }
    }
}