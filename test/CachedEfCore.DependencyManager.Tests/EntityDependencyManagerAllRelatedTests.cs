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
    public class EntityDependencyManagerAllRelatedTests : EntityDependencyManagerTestBase
    {
        [DebuggerDisplay("{Type.Name}")]
        public record AllRelatedData : IXunitSerializable
        {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
            public AllRelatedData()
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

            public static AllRelatedData Create<T>(HashSet<IEntityType> expected)
            {
                return Create(typeof(T), expected);
            }

            public static AllRelatedData Create(Type type, HashSet<IEntityType> expected)
            {
                var proxyType = _cachedDbContext.CreateProxy(type).GetType();

                return new AllRelatedData
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

            private static AllRelatedData MapToExisting(AllRelatedData instance, Type type, HashSet<IEntityType> expected)
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
                    AllRelatedData.Create<SharedPkPrincipal>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(SharedPkPrincipal)),
                        GetIEntityType(typeof(SharedPkDependent)),
                    })
                },
                {
                    AllRelatedData.Create<SharedPkDependent>(new HashSet<IEntityType>
                    {
                        GetIEntityType(typeof(SharedPkDependent)),
                        GetIEntityType(typeof(SharedPkPrincipal)),
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


            var entitiesByProxyType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(allRelatedData.ProxyType, true);
            Assert.True(allRelatedData.Expected.SetEquals(entitiesByProxyType));
        }
    }
}