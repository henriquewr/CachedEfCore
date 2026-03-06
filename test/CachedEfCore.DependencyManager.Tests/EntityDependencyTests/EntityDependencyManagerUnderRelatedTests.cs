using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlAnalysis;
using CachedEfCore.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace CachedEfCore.DependencyManager.Tests.EntityDependencyTests
{
    public class EntityDependencyManagerUnderRelatedTests : EntityDependencyManagerTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;

        public EntityDependencyManagerUnderRelatedTests(ServiceProviderFixture serviceProviderFixture)
        {
            _serviceProviderFixture = serviceProviderFixture;
            _cachedDbContext = GetDbContext();
        }

        protected virtual IServiceProvider CreateProvider()
         => _serviceProviderFixture.CreateProvider(services =>
         {
             services.AddCachedEfCore<SqlServerQueryEntityExtractor>();

             services.AddDbContext<TestDbContext>();
         });

        protected virtual TestDbContext GetDbContext()
        {
            return CreateProvider().GetRequiredService<TestDbContext>();
        }

        public static TheoryData<UnderRelatedData> GetUnderRelatedEntitiesNotIncludingFksTestCases()
        {
            var theoryData = new TheoryData<UnderRelatedData>
            {
                {
                    UnderRelatedData.Create<EntityManyToMany>(new HashSet<string>
                    {
                        typeof(EntityManyToMany).FullName!,
                        typeof(OtherEntityManyToMany).FullName!,
                        typeof(EntityManyToManyOtherEntityManyToMany).FullName!
                    })
                },
                {
                    UnderRelatedData.Create<EntityManyToManyWithoutNavigation>(new HashSet<string>
                    {
                        typeof(EntityManyToManyWithoutNavigation).FullName!,
                        typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation).FullName!,
                    })
                },
                {
                    UnderRelatedData.Create<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation>(new HashSet<string>
                    {
                        typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation).FullName!,
                    })
                },
                {
                    UnderRelatedData.Create<EntityManyToManySkipNavigation>(new HashSet<string>
                    {
                        typeof(EntityManyToManySkipNavigation).FullName!,
                        typeof(OtherEntityManyToManySkipNavigation).FullName!,
                        EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName,
                    })
                },

                {
                    UnderRelatedData.Create<NonLazyLoadEntity>(new HashSet<string>
                    {
                        typeof(NonLazyLoadEntity).FullName!,
                    })
                },
                {
                    UnderRelatedData.Create<LazyLoadEntity>(new HashSet<string>
                    {
                        typeof(LazyLoadEntity).FullName!,
                        typeof(NonLazyLoadEntity).FullName!,
                    })
                },
                {
                    UnderRelatedData.Create<AnotherLazyLoadEntity>(new HashSet<string>
                    {
                        typeof(AnotherLazyLoadEntity).FullName!,
                        typeof(LazyLoadEntity).FullName!,
                        typeof(NonLazyLoadEntity).FullName!,
                    })
                },
                {
                    UnderRelatedData.Create<LazyLoadWithGenericEntity>(new HashSet<string>
                    {
                        typeof(LazyLoadWithGenericEntity).FullName!,
                        typeof(NonLazyLoadEntity).FullName!,
                    })
                },
                {
                    UnderRelatedData.Create<EntityWithDependentAttribute>(new HashSet<string>
                    {
                        typeof(EntityWithDependentAttribute).FullName!,
                        typeof(NonLazyLoadEntity).FullName!,
                        typeof(LazyLoadEntity).FullName!,
                    })
                },
                {
                    UnderRelatedData.Create<SharedPkPrincipal>(new HashSet<string>
                    {
                        typeof(SharedPkPrincipal).FullName!,
                        typeof(SharedPkDependent).FullName!,
                    })
                },
                {
                    UnderRelatedData.Create<SharedPkDependent>(new HashSet<string>
                    {
                        typeof(SharedPkDependent).FullName!,
                        typeof(SharedPkPrincipal).FullName!,
                    })
                },
            };

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetUnderRelatedEntitiesNotIncludingFksTestCases))]
        public void GetUnderRelatedEntities_Should_Return_Under_Related_Not_IncludingFks(UnderRelatedData underRelatedData)
        {
            var expectedEntities = underRelatedData.Expected.Select(GetIEntityType).ToHashSet();

            var iEntityType = GetIEntityType(underRelatedData.Type);
            var entitiesByIEntityType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(iEntityType, false);
            Assert.True(expectedEntities.SetEquals(entitiesByIEntityType));

            var entities = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.Type, false);
            Assert.True(expectedEntities.SetEquals(entities));

            var entitiesByAnonymousType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.AnonymousType, false);
            Assert.True(expectedEntities.SetEquals(entitiesByAnonymousType));

            var entitiesByNestedAnonymousType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.NestedAnonymousType, false);
            Assert.True(expectedEntities.SetEquals(entitiesByNestedAnonymousType));

            var entitiesByGenericAnonymousType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.GenericAnonymousType, false);
            Assert.True(expectedEntities.SetEquals(entitiesByGenericAnonymousType));

            var entitiesByNestedGenericAnonymousType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.NestedGenericAnonymousType, false);
            Assert.True(expectedEntities.SetEquals(entitiesByNestedGenericAnonymousType));

            var entitiesTupleLiteralType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.TupleLiteralType, false);
            Assert.True(expectedEntities.SetEquals(entitiesTupleLiteralType));

            var entitiesByGenericTupleLiteralType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.GenericTupleLiteralType, false);
            Assert.True(expectedEntities.SetEquals(entitiesByGenericTupleLiteralType));

            var proxyType = _cachedDbContext.CreateProxy(underRelatedData.Type).GetType();
            var entitiesByProxyType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(proxyType, false);
            Assert.True(expectedEntities.SetEquals(entitiesByProxyType));
        }



        public static TheoryData<UnderRelatedData> GetUnderRelatedEntitiesIncludingFksTestCases()
        {
            var theoryData = new TheoryData<UnderRelatedData>
            {
                {
                    UnderRelatedData.Create<EntityManyToMany>(new HashSet<string>
                    {
                        typeof(EntityManyToMany).FullName!,
                        typeof(OtherEntityManyToMany).FullName!,
                        typeof(EntityManyToManyOtherEntityManyToMany).FullName!,
                    })
                },
                {
                    UnderRelatedData.Create<EntityManyToManyOtherEntityManyToMany>(new HashSet<string>
                    {
                        typeof(EntityManyToMany).FullName!,
                        typeof(OtherEntityManyToMany).FullName!,
                        typeof(EntityManyToManyOtherEntityManyToMany).FullName!,
                    })
                },
                {
                    UnderRelatedData.Create<EntityManyToManyWithoutNavigation>(new HashSet<string>
                    {
                        typeof(EntityManyToManyWithoutNavigation).FullName!,
                        typeof(OtherEntityManyToManyWithoutNavigation).FullName!,
                        typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation).FullName!,
                    })
                },
                {
                    UnderRelatedData.Create<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation>(new HashSet<string>
                    {
                        typeof(EntityManyToManyWithoutNavigation).FullName!,
                        typeof(OtherEntityManyToManyWithoutNavigation).FullName!,
                        typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation).FullName!,
                    })
                },

                {
                    UnderRelatedData.Create<EntityManyToManySkipNavigation>(new HashSet<string>
                    {
                        typeof(EntityManyToManySkipNavigation).FullName!,
                        typeof(OtherEntityManyToManySkipNavigation).FullName!,
                        EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName!,
                    })
                },
                {
                    UnderRelatedData.Create<NonLazyLoadEntity>(new HashSet<string>
                    {
                        typeof(NonLazyLoadEntity).FullName!,
                        typeof(LazyLoadWithGenericEntity).FullName!,
                    })
                },
                {
                    UnderRelatedData.Create<LazyLoadEntity>(new HashSet<string>
                    {
                        typeof(LazyLoadEntity).FullName!,
                        typeof(NonLazyLoadEntity).FullName!,
                        typeof(LazyLoadWithGenericEntity).FullName!,
                    })
                },
                {
                    UnderRelatedData.Create<AnotherLazyLoadEntity>(new HashSet<string>
                    {
                        typeof(AnotherLazyLoadEntity).FullName!,
                        typeof(LazyLoadEntity).FullName!,
                        typeof(NonLazyLoadEntity).FullName!,
                        typeof(LazyLoadWithGenericEntity).FullName!,
                    })
                },
                {
                    UnderRelatedData.Create<LazyLoadWithGenericEntity>(new HashSet<string>
                    {
                        typeof(LazyLoadWithGenericEntity).FullName!,
                        typeof(NonLazyLoadEntity).FullName!,
                    })
                },
                {
                    UnderRelatedData.Create<EntityWithDependentAttribute>(new HashSet<string>
                    {
                        typeof(EntityWithDependentAttribute).FullName!,
                        typeof(NonLazyLoadEntity).FullName!,
                        typeof(LazyLoadEntity).FullName!,
                        typeof(LazyLoadWithGenericEntity).FullName!,
                    })
                }
            };

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetUnderRelatedEntitiesIncludingFksTestCases))]
        public void GetUnderRelatedEntities_Should_Return_Under_Related_When_IncludingFks(UnderRelatedData underRelatedData)
        {
            var expectedEntities = underRelatedData.Expected.Select(GetIEntityType).ToHashSet();

            var iEntityType = GetIEntityType(underRelatedData.Type);
            var entitiesByIEntityTypeIncludingFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(iEntityType, true);
            Assert.True(expectedEntities.SetEquals(entitiesByIEntityTypeIncludingFks));

            var entitiesIncludingFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.Type, true);
            Assert.True(expectedEntities.SetEquals(entitiesIncludingFks));

            var entitiesByAnonymousTypeIncludingFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.AnonymousType, true);
            Assert.True(expectedEntities.SetEquals(entitiesByAnonymousTypeIncludingFks));

            var entitiesByNestedAnonymousTypeIncludingFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.NestedAnonymousType, true);
            Assert.True(expectedEntities.SetEquals(entitiesByNestedAnonymousTypeIncludingFks));

            var entitiesByGenericAnonymousTypeIncludingFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.GenericAnonymousType, true);
            Assert.True(expectedEntities.SetEquals(entitiesByGenericAnonymousTypeIncludingFks));

            var entitiesByNestedGenericAnonymousTypeIncludingFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.NestedGenericAnonymousType, true);
            Assert.True(expectedEntities.SetEquals(entitiesByNestedGenericAnonymousTypeIncludingFks));

            var entitiesByTupleLiteralTypeFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.TupleLiteralType, true);
            Assert.True(expectedEntities.SetEquals(entitiesByTupleLiteralTypeFks));

            var entitiesByGenericTupleLiteralTypeIncludingFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(underRelatedData.GenericTupleLiteralType, true);
            Assert.True(expectedEntities.SetEquals(entitiesByGenericTupleLiteralTypeIncludingFks));

            var proxyType = _cachedDbContext.CreateProxy(underRelatedData.Type).GetType();
            var entitiesByProxyType = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(proxyType, true);
            Assert.True(expectedEntities.SetEquals(entitiesByProxyType));
        }



        public static TheoryData<string, bool, HashSet<string>> GetUnderRelatedEntitiesGeneratedByEfTestCases()
        {
            var theoryData = new TheoryData<string, bool, HashSet<string>>
            {
                {
                    EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName,
                    false,
                    new HashSet<string>
                    {
                        EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName,
                    }
                },
                {
                    EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName,
                    true,
                    new HashSet<string>
                    {
                        typeof(EntityManyToManySkipNavigation).FullName!,
                        typeof(OtherEntityManyToManySkipNavigation).FullName!,
                        EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName,
                    }
                },
            };

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetUnderRelatedEntitiesGeneratedByEfTestCases))]
        public void GetUnderRelatedEntities_Should_Return_Under_Related_Generated_By_EF(string iEntityType, bool includingRelatedInFks, HashSet<string> expected)
        {
            var entitiesByIEntityTypeIncludingFks = _cachedDbContext.DependencyManager.GetUnderRelatedEntities(GetIEntityType(iEntityType), includingRelatedInFks);
           
            var expectedEntities = expected.Select(GetIEntityType).ToHashSet();
            Assert.True(expectedEntities.SetEquals(entitiesByIEntityTypeIncludingFks));
        }


        [DebuggerDisplay("{Type.Name}")]
        public record UnderRelatedData : IXunitSerializable
        {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
            public UnderRelatedData()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
            {
            }

            public Type Type { get; private set; }
            public Type AnonymousType { get; private set; }
            public Type NestedAnonymousType { get; private set; }
            public Type GenericAnonymousType { get; private set; }
            public Type NestedGenericAnonymousType { get; private set; }
            public Type TupleLiteralType { get; private set; }
            public Type GenericTupleLiteralType { get; private set; }

            public HashSet<string> Expected { get; private set; }

            public static UnderRelatedData Create<T>(HashSet<string> expected)
            {
                return Create(typeof(T), expected);
            }

            public static UnderRelatedData Create(Type type, HashSet<string> expected)
            {
                return new UnderRelatedData
                {
                    Type = type,
                    AnonymousType = TypeHelper.AnonymousType.Create(type),
                    NestedAnonymousType = TypeHelper.AnonymousType.CreateNested(type),
                    GenericAnonymousType = TypeHelper.AnonymousType.CreateGeneric(type),
                    NestedGenericAnonymousType = TypeHelper.AnonymousType.CreateNestedGeneric(type),
                    TupleLiteralType = TypeHelper.Tuple.Create(type),
                    GenericTupleLiteralType = TypeHelper.Tuple.CreateGeneric(type),
                    Expected = expected,
                };
            }

            private static UnderRelatedData MapToExisting(UnderRelatedData instance, Type type, HashSet<string> expected)
            {
                instance.Type = type;
                instance.AnonymousType = TypeHelper.AnonymousType.Create(type);
                instance.NestedAnonymousType = TypeHelper.AnonymousType.CreateNested(type);
                instance.GenericAnonymousType = TypeHelper.AnonymousType.CreateGeneric(type);
                instance.NestedGenericAnonymousType = TypeHelper.AnonymousType.CreateNestedGeneric(type);
                instance.TupleLiteralType = TypeHelper.Tuple.Create(type);
                instance.GenericTupleLiteralType = TypeHelper.Tuple.CreateGeneric(type);
                instance.Expected = expected;

                return instance;
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                var expectedJson = System.Text.Json.JsonSerializer.Serialize(Expected);

                info.AddValue(nameof(Type), Type.FullName);

                info.AddValue(nameof(Expected), expectedJson);
            }

            public void Deserialize(IXunitSerializationInfo info)
            {
                var type = Type.GetType(info.GetValue<string>(nameof(Type)), throwOnError: true)!;
                var expectedJson = info.GetValue<string>(nameof(Expected));

                var expected = System.Text.Json.JsonSerializer.Deserialize<HashSet<string>>(expectedJson)!;

                MapToExisting(this, type, expected);
            }

            public override string ToString()
            {
                return $"Type={Type.Name}, Expected={Expected.Count}";
            }
        }
    }
}
