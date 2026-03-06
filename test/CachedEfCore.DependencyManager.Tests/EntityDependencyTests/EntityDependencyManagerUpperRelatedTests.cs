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
    public class EntityDependencyManagerUpperRelatedTests : EntityDependencyManagerTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;

        public EntityDependencyManagerUpperRelatedTests(ServiceProviderFixture serviceProviderFixture)
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

        protected TestDbContext GetDbContext()
        {
            return CreateProvider().GetRequiredService<TestDbContext>();
        }
        public static TheoryData<UpperRelatedData> GetUpperRelatedEntitiesTestCases()
        {
            var theoryData = new TheoryData<UpperRelatedData>
            {
                {
                    UpperRelatedData.Create<EntityManyToMany>(new HashSet<string>
                    {
                        typeof(EntityManyToMany).FullName!,
                        typeof(OtherEntityManyToMany).FullName!,
                        typeof(EntityManyToManyOtherEntityManyToMany).FullName!,
                    })
                },
                {
                    UpperRelatedData.Create<EntityManyToManyOtherEntityManyToMany>(new HashSet<string>
                    {
                        typeof(EntityManyToMany).FullName!,
                        typeof(OtherEntityManyToMany).FullName!,
                        typeof(EntityManyToManyOtherEntityManyToMany).FullName!,
                    })
                },
                {
                    UpperRelatedData.Create<EntityManyToManyWithoutNavigation>(new HashSet<string>
                    {
                        typeof(EntityManyToManyWithoutNavigation).FullName!,
                        typeof(OtherEntityManyToManyWithoutNavigation).FullName!,
                        typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation).FullName!,
                    })
                },
                {
                    UpperRelatedData.Create<EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation>(new HashSet<string>
                    {
                        typeof(EntityManyToManyWithoutNavigation).FullName!,
                        typeof(OtherEntityManyToManyWithoutNavigation).FullName!,
                        typeof(EntityManyToManyWithoutNavigationOtherEntityManyToManyWithoutNavigation).FullName!,
                    })
                },
                {
                    UpperRelatedData.Create<EntityManyToManySkipNavigation>(new HashSet<string>
                    {
                        typeof(EntityManyToManySkipNavigation).FullName!,
                        typeof(OtherEntityManyToManySkipNavigation).FullName!,
                        EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName,
                    })
                },
                {
                    UpperRelatedData.Create<NonLazyLoadEntity>(new HashSet<string>
                    {
                        typeof(AnotherLazyLoadEntity).FullName!,
                        typeof(LazyLoadEntity).FullName!,
                        typeof(NonLazyLoadEntity).FullName!,
                        typeof(LazyLoadWithGenericEntity).FullName!,
                        typeof(EntityWithDependentAttribute).FullName!,
                    })
                },
                {
                    UpperRelatedData.Create<LazyLoadEntity>(new HashSet<string>
                    {
                        typeof(AnotherLazyLoadEntity).FullName!,
                        typeof(LazyLoadEntity).FullName!,
                        typeof(EntityWithDependentAttribute).FullName!,
                    })
                },
                {
                    UpperRelatedData.Create<AnotherLazyLoadEntity>(new HashSet<string>
                    {
                        typeof(AnotherLazyLoadEntity).FullName!,
                    })
                },
                {
                    UpperRelatedData.Create<LazyLoadWithGenericEntity>(new HashSet<string>
                    {
                        typeof(AnotherLazyLoadEntity).FullName!,
                        typeof(LazyLoadEntity).FullName!,
                        typeof(NonLazyLoadEntity).FullName!,
                        typeof(LazyLoadWithGenericEntity).FullName!,
                        typeof(EntityWithDependentAttribute).FullName!,
                    })
                },
                {
                    UpperRelatedData.Create<EntityWithDependentAttribute>(new HashSet<string>
                    {
                        typeof(EntityWithDependentAttribute).FullName!,
                    })
                },
                {
                    UpperRelatedData.Create<SharedPkPrincipal>(new HashSet<string>
                    {
                        typeof(SharedPkDependent).FullName!,
                        typeof(SharedPkPrincipal).FullName!,
                    })
                },
                {
                    UpperRelatedData.Create<SharedPkDependent>(new HashSet<string>
                    {
                        typeof(SharedPkDependent).FullName!,
                        typeof(SharedPkPrincipal).FullName!,
                    })
                },
            };

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetUpperRelatedEntitiesTestCases))]
        public void GetUpperRelatedEntities_Should_Return_Upper_Related(UpperRelatedData upperRelatedData)
        {
            var expectedEntities = upperRelatedData.Expected.Select(GetIEntityType).ToHashSet();

            var iEntityType = GetIEntityType(upperRelatedData.Type);
            var entitiesByIEntityType = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(iEntityType);
            Assert.True(expectedEntities.SetEquals(entitiesByIEntityType));


            var entities = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(upperRelatedData.Type);
            Assert.True(expectedEntities.SetEquals(entities));


            var entitiesByAnonymousType = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(upperRelatedData.AnonymousType);
            Assert.True(expectedEntities.SetEquals(entitiesByAnonymousType));


            var entitiesByNestedAnonymousType = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(upperRelatedData.NestedAnonymousType);
            Assert.True(expectedEntities.SetEquals(entitiesByNestedAnonymousType));


            var entitiesByGenericAnonymousType = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(upperRelatedData.GenericAnonymousType);
            Assert.True(expectedEntities.SetEquals(entitiesByGenericAnonymousType));


            var entitiesByNestedGenericAnonymousType = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(upperRelatedData.NestedGenericAnonymousType);
            Assert.True(expectedEntities.SetEquals(entitiesByNestedGenericAnonymousType));


            var entitiesByTupleLiteralType = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(upperRelatedData.TupleLiteralType);
            Assert.True(expectedEntities.SetEquals(entitiesByTupleLiteralType));


            var entitiesByGenericTupleLiteralType = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(upperRelatedData.GenericTupleLiteralType);
            Assert.True(expectedEntities.SetEquals(entitiesByGenericTupleLiteralType));

            var proxyType = _cachedDbContext.CreateProxy(upperRelatedData.Type).GetType();
            var entitiesByProxyType = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(proxyType);
            Assert.True(expectedEntities.SetEquals(entitiesByProxyType));
        }


        public static TheoryData<string, HashSet<string>> GetUpperRelatedEntitiesGenByEfTestCases()
        {
            var theoryData = new TheoryData<string, HashSet<string>>
            {
                {
                    EntityManyToManySkipNavigationOtherEntityManyToManySkipNavigation.TableName,
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
        [MemberData(nameof(GetUpperRelatedEntitiesGenByEfTestCases))]
        public void GetUpperRelatedEntities_Should_Return_Upper_Related_When_Is_Generated_By_EF(string iEntityType, HashSet<string> expected)
        {
            var entitiesByIEntityType = _cachedDbContext.DependencyManager.GetUpperRelatedEntities(GetIEntityType(iEntityType));

            var expectedEntities = expected.Select(GetIEntityType).ToHashSet();

            Assert.True(expectedEntities.SetEquals(entitiesByIEntityType));
        }

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
            public Type NestedAnonymousType { get; private set; }
            public Type GenericAnonymousType { get; private set; }
            public Type NestedGenericAnonymousType { get; private set; }
            public Type TupleLiteralType { get; private set; }
            public Type GenericTupleLiteralType { get; private set; }

            public HashSet<string> Expected { get; private set; }

            public static UpperRelatedData Create<T>(HashSet<string> expected)
            {
                return Create(typeof(T), expected);
            }

            public static UpperRelatedData Create(Type type, HashSet<string> expected)
            {
                return new UpperRelatedData
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

            private static UpperRelatedData MapToExisting(UpperRelatedData instance, Type type, HashSet<string> expected)
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
