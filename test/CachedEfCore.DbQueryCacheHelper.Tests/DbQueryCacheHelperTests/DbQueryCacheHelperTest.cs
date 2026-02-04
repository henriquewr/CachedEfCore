using CachedEfCore.Cache.Helper;
using CachedEfCore.Cache.Tests.Common;
using CachedEfCore.DependencyInjection;
using CachedEfCore.KeyGeneration;
using CachedEfCore.KeyGeneration.EvalTypeChecker;
using CachedEfCore.KeyGeneration.ExpressionKeyGen;
using Microsoft.Extensions.Caching.Memory;
using System;
using Xunit;

namespace CachedEfCore.Cache.Tests.DbQueryCacheHelperTests
{
    public class DbQueryCacheHelperTest
    {
        private static readonly PrintabilityChecker _defaultPrintabilityChecker = new PrintabilityChecker();

        private static readonly KeyGeneratorVisitor _keyGeneratorVisitor = new KeyGeneratorVisitor
        (
            _defaultPrintabilityChecker,
            new ExpressionEvalTypeCheckerVisitor(new TypeCompatibilityChecker([])),
            CachedEfCoreOptions.DefaultKeyGeneratorJsonSerializerOptions
        );

        private static readonly DbQueryCacheHelper _dbQueryCacheHelper = new DbQueryCacheHelper(_keyGeneratorVisitor, _defaultPrintabilityChecker);

        private static IMemoryCache CreateMemoryCache()
        {
            return new MemoryCache(new MemoryCacheOptions()
            {
                TrackStatistics = true,
            });
        }

        private static TestDbContext CreateDbContext(IMemoryCache memoryCache)
        {
            var dbQueryCacheStore = new Cache.DbQueryCacheStore(memoryCache);
            var dbContext = new TestDbContext(dbQueryCacheStore);

            return dbContext;
        }

        public static TheoryData<TestDbContext, object, bool> GetGetOrAddToCacheData()
        {
            var dbContext = CreateDbContext(CreateMemoryCache());

            return new()
            {
                { dbContext, new LazyLoadEntity(), true },
                { dbContext, new NonLazyLoadEntity(), false },
            };
        }

        [Theory]
        [MemberData(nameof(GetGetOrAddToCacheData))]
        public void GetOrAdd_Adds_And_Gets_From_Cache(TestDbContext dbContext, object valueToCache, bool isDbContextDependent)
        {
            dbContext.TestDbQueryCacheStore.TestDbContextDependentKeys.Clear();
            dbContext.TestDbQueryCacheStore.TestTypeKeys.Clear();

            const string cacheKey = "cacheKeyAddToCache";

            bool created = false;
            object result;

            if (isDbContextDependent)
            {
                result = _dbQueryCacheHelper.GetOrAdd<LazyLoadEntity, object/*any type*/>(dbContext, DbContextDependentCreateFunc, cacheKey);
                Assert.Single(dbContext.TestDbQueryCacheStore.TestDbContextDependentKeys);
            }
            else
            {
                result = _dbQueryCacheHelper.GetOrAdd<NonLazyLoadEntity, object/*any type*/>(dbContext, NonDbContextDependentCreateFunc, cacheKey);
                Assert.Empty(dbContext.TestDbQueryCacheStore.TestDbContextDependentKeys);
            }
            Assert.Single(dbContext.TestDbQueryCacheStore.TestTypeKeys);

            Assert.True(created);
            Assert.Same(valueToCache, result);

            created = false;
            object cached;

            if (isDbContextDependent)
            {
                cached = _dbQueryCacheHelper.GetOrAdd<LazyLoadEntity, object/*any type*/>(dbContext, DbContextDependentCreateFunc, cacheKey);
            }
            else
            {
                cached = _dbQueryCacheHelper.GetOrAdd<NonLazyLoadEntity, object/*any type*/>(dbContext, NonDbContextDependentCreateFunc, cacheKey);
            }

            Assert.False(created);
            Assert.Same(valueToCache, cached);

            LazyLoadEntity DbContextDependentCreateFunc()
            {
                created = true;
                return (LazyLoadEntity)valueToCache!;
            }
            NonLazyLoadEntity NonDbContextDependentCreateFunc()
            {
                created = true;
                return (NonLazyLoadEntity)valueToCache!;
            }
        }
    }
}
