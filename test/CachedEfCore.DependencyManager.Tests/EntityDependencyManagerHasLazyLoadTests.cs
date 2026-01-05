using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace CachedEfCore.DependencyManager.Tests
{
    public class EntityDependencyManagerHasLazyLoadTests : EntityDependencyManagerTestBase
    {
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
            public Type GenericAnonymousType { get; private set; }
            public Type TupleLiteralType { get; private set; }
            public Type GenericTupleLiteralType { get; private set; }
            public Type ProxyType { get; private set; }

            public bool Expected { get; private set; }

            public static HasLazyLoadData Create<T>(bool expected)
            {
                return Create(typeof(T), expected);
            }

            public static HasLazyLoadData Create(Type type, bool expected)
            {
                var proxyType = _cachedDbContext.CreateProxy(type).GetType();

                return new HasLazyLoadData
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

            private static HasLazyLoadData MapToExisting(HasLazyLoadData instance, Type type, bool expected)
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


            var lazyLoadByGenericAnonymousType = _cachedDbContext.DependencyManager.HasLazyLoad(hasLazyLoadData.GenericAnonymousType);
            Assert.Equal(hasLazyLoadData.Expected, lazyLoadByGenericAnonymousType);


            var lazyLoadByTupleLiteralType = _cachedDbContext.DependencyManager.HasLazyLoad(hasLazyLoadData.TupleLiteralType);
            Assert.Equal(hasLazyLoadData.Expected, lazyLoadByTupleLiteralType);


            var lazyLoadByGenericTupleLiteralType = _cachedDbContext.DependencyManager.HasLazyLoad(hasLazyLoadData.GenericTupleLiteralType);
            Assert.Equal(hasLazyLoadData.Expected, lazyLoadByGenericTupleLiteralType);


            var lazyLoadByProxyType = _cachedDbContext.DependencyManager.HasLazyLoad(hasLazyLoadData.ProxyType);
            Assert.Equal(hasLazyLoadData.Expected, lazyLoadByProxyType);
        }
    }
}
