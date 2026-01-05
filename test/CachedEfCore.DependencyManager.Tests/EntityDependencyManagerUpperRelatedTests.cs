using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace CachedEfCore.DependencyManager.Tests
{
    public class EntityDependencyManagerUpperRelatedTests : EntityDependencyManagerTestBase
    {

        [DebuggerDisplay("{Type.Name}")]
        public record UpperRelatedData : IXunitSerializable
        {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
            public UpperRelatedData()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
            {
            }

            public Type Type { get; private set; }
            public Type AnonymousType { get; private set; }
            public Type GenericAnonymousType { get; private set; }
            public Type TupleLiteralType { get; private set; }
            public Type GenericTupleLiteralType { get; private set; }
            public Type ProxyType { get; private set; }

            public HashSet<IEntityType> Expected { get; private set; }

            public static UpperRelatedData Create<T>(HashSet<IEntityType> expected)
            {
                return Create(typeof(T), expected);
            }

            public static UpperRelatedData Create(Type type, HashSet<IEntityType> expected)
            {
                var proxyType = _cachedDbContext.CreateProxy(type).GetType();

                return new UpperRelatedData
                {
                    Type = type,
                    AnonymousType = TypeHelper.AnonymousType.Create(type),
                    GenericAnonymousType = TypeHelper.AnonymousType.CreateGeneric(type),
                    TupleLiteralType = TypeHelper.Tuple.Create(type),
                    GenericTupleLiteralType = TypeHelper.Tuple.CreateGeneric(type),
                    ProxyType = proxyType,
                    Expected = expected,
                };
            }

            private static UpperRelatedData MapToExisting(UpperRelatedData instance, Type type, HashSet<IEntityType> expected)
            {
                var proxyType = _cachedDbContext.CreateProxy(type).GetType();

                instance.Type = type;
                instance.AnonymousType = TypeHelper.AnonymousType.Create(type);
                instance.GenericAnonymousType = TypeHelper.AnonymousType.CreateGeneric(type);
                instance.TupleLiteralType = TypeHelper.Tuple.Create(type);
                instance.GenericTupleLiteralType = TypeHelper.Tuple.CreateGeneric(type);
                instance.ProxyType = proxyType;
                instance.Expected = expected;

                return instance;
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                var expected = Expected.Select(x => x.Name).ToArray();
                var expectedJson = System.Text.Json.JsonSerializer.Serialize(expected);

                info.AddValue(nameof(Type), Type.FullName);
                
                info.AddValue(nameof(Expected), expectedJson);
            }

            public void Deserialize(IXunitSerializationInfo info)
            {
                var type = Type.GetType(info.GetValue<string>(nameof(Type)), throwOnError: true)!;
                var expectedJson = info.GetValue<string>(nameof(Expected));

                var expected = System.Text.Json.JsonSerializer.Deserialize<string[]>(expectedJson)!;

                var expectedSet = expected.Select(GetIEntityType).ToHashSet();

                MapToExisting(this, type, expectedSet);
            }

            public override string ToString()
            {
                return $"Type={Type.Name}, Expected={Expected.Count}";
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
                },
                {
                    UpperRelatedData.Create<SharedPkPrincipal>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(SharedPkDependent)),
                        GetIEntityType(typeof(SharedPkPrincipal)),
                    })
                },
                {
                    UpperRelatedData.Create<SharedPkDependent>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(SharedPkDependent)),
                        GetIEntityType(typeof(SharedPkPrincipal)),
                    })
                },
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
