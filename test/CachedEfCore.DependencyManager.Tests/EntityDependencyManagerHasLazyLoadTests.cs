using System;
using System.Collections.Generic;
using Xunit;

namespace CachedEfCore.DepencencyManager.Tests
{
    public class EntityDependencyManagerHasLazyLoadTests : EntityDependencyManagerTestBase
    {
        public record HasLazyLoadData
        {
            private HasLazyLoadData()
            {
            }

            public required Type Type { get; init; }
            public required Type AnonymousType { get; init; }
            public required Type GenericAnonymousType { get; init; }
            public required Type TupleLiteralType { get; init; }
            public required Type GenericTupleLiteralType { get; init; }

            public required bool Expected { get; init; }

            public static HasLazyLoadData Create<T>(bool expected)
            {
                return new HasLazyLoadData
                {
                    Type = typeof(T),
                    AnonymousType = new { A = default(T) }.GetType(),
                    GenericAnonymousType = new { A = default(IEnumerable<T>) }.GetType(),
                    TupleLiteralType = (first: default(T), sec: default(T)).GetType(),
                    GenericTupleLiteralType = (first: default(IEnumerable<T>), sec: default(IEnumerable<T>)).GetType(),
                    Expected = expected
                };
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
        }
    }
}
