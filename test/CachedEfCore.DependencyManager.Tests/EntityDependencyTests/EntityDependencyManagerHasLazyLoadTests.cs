using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlAnalysis;
using CachedEfCore.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace CachedEfCore.DependencyManager.Tests.EntityDependencyTests
{
    public class EntityDependencyManagerHasLazyLoadTests : EntityDependencyManagerTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;

        public EntityDependencyManagerHasLazyLoadTests(ServiceProviderFixture serviceProviderFixture)
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

        public static TheoryData<HasLazyLoadData> GetHasLazyLoadTestCases()
        {
            var theoryData = new TheoryData<HasLazyLoadData>
            {
                {
                    HasLazyLoadData.Create<NonLazyLoadEntity>(false)
                },
                {
                    HasLazyLoadData.Create<LazyLoadEntity>(true)
                },
                {
                    HasLazyLoadData.Create<AnotherLazyLoadEntity>(true)
                },
                {
                    HasLazyLoadData.Create<LazyLoadWithGenericEntity>(true)
                },
                {
                    HasLazyLoadData.Create<EntityWithDependentAttribute>(false)
                },
                {
                    HasLazyLoadData.Create<SharedPkPrincipal>(true)
                },
                {
                    HasLazyLoadData.Create<SharedPkDependent>(true)
                },
            };

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetHasLazyLoadTestCases))]
        public void HasLazyLoad_Should_Return_HasLazyLoad(HasLazyLoadData hasLazyLoadData)
        {
            var iEntityType = GetIEntityType(hasLazyLoadData.Type);
            var lazyLoadByIEntityType = _cachedDbContext.DependencyManager.HasLazyLoad(iEntityType);
            Assert.Equal(hasLazyLoadData.Expected, lazyLoadByIEntityType);


            var lazyLoadByType = _cachedDbContext.DependencyManager.HasLazyLoad(hasLazyLoadData.Type);
            Assert.Equal(hasLazyLoadData.Expected, lazyLoadByType);


            var lazyLoadByAnonymousType = _cachedDbContext.DependencyManager.HasLazyLoad(hasLazyLoadData.AnonymousType);
            Assert.Equal(hasLazyLoadData.Expected, lazyLoadByAnonymousType);


            var lazyLoadByNestedAnonymousType = _cachedDbContext.DependencyManager.HasLazyLoad(hasLazyLoadData.NestedAnonymousType);
            Assert.Equal(hasLazyLoadData.Expected, lazyLoadByNestedAnonymousType);


            var lazyLoadByGenericAnonymousType = _cachedDbContext.DependencyManager.HasLazyLoad(hasLazyLoadData.GenericAnonymousType);
            Assert.Equal(hasLazyLoadData.Expected, lazyLoadByGenericAnonymousType);


            var lazyLoadByNestedGenericAnonymousType = _cachedDbContext.DependencyManager.HasLazyLoad(hasLazyLoadData.NestedGenericAnonymousType);
            Assert.Equal(hasLazyLoadData.Expected, lazyLoadByNestedGenericAnonymousType);


            var lazyLoadByTupleLiteralType = _cachedDbContext.DependencyManager.HasLazyLoad(hasLazyLoadData.TupleLiteralType);
            Assert.Equal(hasLazyLoadData.Expected, lazyLoadByTupleLiteralType);


            var lazyLoadByGenericTupleLiteralType = _cachedDbContext.DependencyManager.HasLazyLoad(hasLazyLoadData.GenericTupleLiteralType);
            Assert.Equal(hasLazyLoadData.Expected, lazyLoadByGenericTupleLiteralType);

            var proxyType = _cachedDbContext.CreateProxy(hasLazyLoadData.Type).GetType();
            var lazyLoadByProxyType = _cachedDbContext.DependencyManager.HasLazyLoad(proxyType);
            Assert.Equal(hasLazyLoadData.Expected, lazyLoadByProxyType);
        }

        [DebuggerDisplay("{Type.Name}")]
        public record HasLazyLoadData : IXunitSerializable
        {

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
            public HasLazyLoadData()
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

            public bool Expected { get; private set; }

            public static HasLazyLoadData Create<T>(bool expected)
            {
                return Create(typeof(T), expected);
            }

            public static HasLazyLoadData Create(Type type, bool expected)
            {
                return new HasLazyLoadData
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

            private static HasLazyLoadData MapToExisting(HasLazyLoadData instance, Type type, bool expected)
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
                info.AddValue(nameof(Type), Type.FullName);

                info.AddValue(nameof(Expected), Expected);
            }

            public void Deserialize(IXunitSerializationInfo info)
            {
                var type = Type.GetType(info.GetValue<string>(nameof(Type)), throwOnError: true)!;
                var expected = info.GetValue<bool>(nameof(Expected));

                MapToExisting(this, type, expected);
            }

            public override string ToString()
            {
                return $"Type={Type.Name}, Expected={Expected}";
            }
        }
    }
}
