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
    public class EntityDependencyManagerAllRelatedTests : EntityDependencyManagerTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;

        public EntityDependencyManagerAllRelatedTests(ServiceProviderFixture serviceProviderFixture)
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

        [Theory]
        [ClassData(typeof(GetAllRelatedEntitiesTestCases))]
        public void GetAllRelatedEntities_Should_Return_All_Related(AllRelatedData allRelatedData)
        {
            var expectedEntities = allRelatedData.Expected.Select(GetIEntityType).ToHashSet();

            var iEntityType = GetIEntityType(allRelatedData.Type);
            var entitiesByIEntityType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(iEntityType, true);
            Assert.True(expectedEntities.SetEquals(entitiesByIEntityType));


            var entities = _cachedDbContext.DependencyManager.GetAllRelatedEntities(allRelatedData.Type, true);
            Assert.True(expectedEntities.SetEquals(entities));


            var entitiesByAnonymousType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(allRelatedData.AnonymousType, true);
            Assert.True(expectedEntities.SetEquals(entitiesByAnonymousType));


            var entitiesByNestedAnonymousType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(allRelatedData.NestedAnonymousType, true);
            Assert.True(expectedEntities.SetEquals(entitiesByNestedAnonymousType));


            var entitiesByGenericAnonymousType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(allRelatedData.GenericAnonymousType, true);
            Assert.True(expectedEntities.SetEquals(entitiesByGenericAnonymousType));


            var entitiesByNestedGenericAnonymousType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(allRelatedData.NestedGenericAnonymousType, true);
            Assert.True(expectedEntities.SetEquals(entitiesByNestedGenericAnonymousType));


            var entitiesByTupleLiteralType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(allRelatedData.TupleLiteralType, true);
            Assert.True(expectedEntities.SetEquals(entitiesByTupleLiteralType));


            var entitiesByGenericTupleLiteralType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(allRelatedData.GenericTupleLiteralType, true);
            Assert.True(expectedEntities.SetEquals(entitiesByGenericTupleLiteralType));


            var proxyType = _cachedDbContext.CreateProxy(allRelatedData.Type).GetType();
            var entitiesByProxyType = _cachedDbContext.DependencyManager.GetAllRelatedEntities(proxyType, true);
            Assert.True(expectedEntities.SetEquals(entitiesByProxyType));
        }

        public class GetAllRelatedEntitiesTestCases : TheoryData<AllRelatedData>
        {
            public GetAllRelatedEntitiesTestCases()
            {
                Add(
                    AllRelatedData.Create<NonLazyLoadEntity>(new HashSet<Type>
                    {
                        typeof(AnotherLazyLoadEntity),
                        typeof(LazyLoadEntity),
                        typeof(NonLazyLoadEntity),
                        typeof(LazyLoadWithGenericEntity),
                        typeof(EntityWithDependentAttribute),
                    })
                );

                Add(
                    AllRelatedData.Create<LazyLoadEntity>(new HashSet<Type>
                    {
                        typeof(AnotherLazyLoadEntity),
                        typeof(LazyLoadEntity),
                        typeof(NonLazyLoadEntity),
                        typeof(LazyLoadWithGenericEntity),
                        typeof(EntityWithDependentAttribute),
                    })
                );
                Add(
                    AllRelatedData.Create<AnotherLazyLoadEntity>(new HashSet<Type>
                    {
                        typeof(AnotherLazyLoadEntity),
                        typeof(LazyLoadEntity),
                        typeof(NonLazyLoadEntity),
                        typeof(LazyLoadWithGenericEntity),
                        typeof(EntityWithDependentAttribute),
                    })
                );
                Add(
                    AllRelatedData.Create<LazyLoadWithGenericEntity>(new HashSet<Type>
                    {
                        typeof(AnotherLazyLoadEntity),
                        typeof(LazyLoadEntity),
                        typeof(NonLazyLoadEntity),
                        typeof(LazyLoadWithGenericEntity),
                        typeof(EntityWithDependentAttribute),
                    })
                );
                Add(
                    AllRelatedData.Create<SharedPkPrincipal>(new HashSet<Type>
                    {
                        typeof(SharedPkPrincipal),
                        typeof(SharedPkDependent),
                    })
                );
                Add(
                    AllRelatedData.Create<SharedPkDependent>(new HashSet<Type>
                    {
                        typeof(SharedPkDependent),
                        typeof(SharedPkPrincipal),
                    })
                );
            }
        }

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
            public Type NestedAnonymousType { get; private set; }
            public Type GenericAnonymousType { get; private set; }
            public Type NestedGenericAnonymousType { get; private set; }
            public Type TupleLiteralType { get; private set; }
            public Type GenericTupleLiteralType { get; private set; }

            public HashSet<Type> Expected { get; private set; }

            public static AllRelatedData Create<T>(HashSet<Type> expected)
            {
                return Create(typeof(T), expected);
            }

            public static AllRelatedData Create(Type type, HashSet<Type> expected)
            {
                return new AllRelatedData
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

            private static AllRelatedData MapToExisting(AllRelatedData instance, Type type, HashSet<Type> expected)
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
                var expected = Expected.Select(x => x.FullName).ToArray();
                var expectedJson = System.Text.Json.JsonSerializer.Serialize(expected);

                info.AddValue(nameof(Type), Type.FullName);

                info.AddValue(nameof(Expected), expectedJson);
            }

            public void Deserialize(IXunitSerializationInfo info)
            {
                var type = Type.GetType(info.GetValue<string>(nameof(Type)), throwOnError: true)!;
                var expectedJson = info.GetValue<string>(nameof(Expected));

                var expected = System.Text.Json.JsonSerializer.Deserialize<string[]>(expectedJson)!;

                var expectedSet = expected.Select(x => Type.GetType(x, throwOnError: true)!).ToHashSet();

                MapToExisting(this, type, expectedSet);
            }

            public override string ToString()
            {
                return $"Type={Type.Name}, Expected={Expected.Count}";
            }
        }
    }
}