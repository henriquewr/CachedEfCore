using CachedEfCore.Cache.Helper;
using CachedEfCore.Cache.Metrics;
using CachedEfCore.Caching.Specification.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CachedEfCore.Caching.Specification.Tests.DbQueryCacheHelperTests
{
    public abstract class DbQueryCacheHelperSpecificationTest
    {
        protected abstract IServiceProvider CreatePooledProvider(bool withLazyLoading);
        protected abstract IServiceProvider CreateProvider(bool withLazyLoading);

        public abstract Task GetOrAdd_Adds_And_Gets_From_Cache(object valueToCache, bool isDbContextDependent);

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public virtual async Task DbContextDependent_Entry_Should_Not_Be_Returned_To_Other_DbContext_Multiple_Threads(bool pooledDbContext)
        {
            var serviceProvider = pooledDbContext ? CreatePooledProvider(true) : CreateProvider(true);

            {
                using var scope = serviceProvider.CreateScope();

                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

                await dbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
                await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

                dbContext.LazyLoadEntity.Add(new LazyLoadEntity
                {
                    LazyLoadProp = new NonLazyLoadEntity()
                    {
                        StringData = "Test",
                    },
                });

                await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
                Assert.NotNull(await dbContext.LazyLoadEntity.SingleAsync(cancellationToken: TestContext.Current.CancellationToken));
            }

            await Parallel.ForAsync(0, 10000, async (i, ct) =>
            {
                const string cacheKey = "cacheKeyAddToCache";
                using var scope = serviceProvider.CreateScope();

                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var dbQueryCacheMetrics = scope.ServiceProvider.GetRequiredService<IDbQueryCacheMetrics>();
                var dbQueryCacheHelper = scope.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();

                bool cached = true;

                var result = await dbQueryCacheHelper.GetOrAddAsync(typeof(LazyLoadEntity), dbContext, () =>
                {
                    cached = false;
                    return dbContext.LazyLoadEntity.FirstAsync();
                }, cacheKey);

                Assert.False(cached, "freshly created scope has a dbcontext dependent cache");

                var lazyLoadProp = result.LazyLoadProp;
                Assert.NotNull(lazyLoadProp);
                Assert.NotNull(lazyLoadProp.StringData);
            });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public virtual async Task Should_Be_Transaction_Aware_On_Commit(bool async)
        {
            var serviceProvider = CreatePooledProvider(true);

            var entities = Enumerable.Range(0, 2000).Select(static x => new NonLazyLoadEntity
            {
                StringData = $"Test-{x}",
                Value = 0,
            }).ToArray();

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

                await dbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
                await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

                dbContext.NonLazyLoadEntity.AddRange(entities);

                await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await Parallel.ForEachAsync(entities, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4, CancellationToken = TestContext.Current.CancellationToken }, async (savedEntity, cancellationToken) =>
            {
                using var scope1 = serviceProvider.CreateScope();
                using var scope2 = serviceProvider.CreateScope();

                var dbContext1 = scope1.ServiceProvider.GetRequiredService<TestDbContext>();
                var dbContext2 = scope2.ServiceProvider.GetRequiredService<TestDbContext>();

                var dbQueryCacheHelper1 = scope1.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();
                var dbQueryCacheHelper2 = scope2.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();

                using (var transaction = async ? await dbContext1.Database.BeginTransactionAsync(cancellationToken) : dbContext1.Database.BeginTransaction())
                {
                    var entity = await dbContext1.NonLazyLoadEntity.FirstAsync(x => x.Id == savedEntity.Id, cancellationToken: cancellationToken);

                    Assert.Equal(0, entity.Value);

                    entity.Value++;

                    await AssertEntity();

                    if (async)
                    {
                        await dbContext1.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        dbContext1.SaveChanges();
                    }

                    await AssertEntity();
                    await AssertEntity();

                    if (async)
                    {
                        await transaction.CommitAsync(cancellationToken);
                    }
                    else
                    {
                        transaction.Commit();
                    }

                    await AssertEntity();
                    await AssertEntity();

                    async Task AssertEntity()
                    {
                        var entitiesFromContext1 = await GetEntityFrom(dbQueryCacheHelper1, dbContext1);
                        Assert.Equal(entitiesFromContext1.FromDb.Value, entitiesFromContext1.FromCache.Value);

                        var entitiesFromContext2 = await GetEntityFrom(dbQueryCacheHelper2, dbContext2);
                        Assert.Equal(entitiesFromContext2.FromDb.Value, entitiesFromContext2.FromCache.Value);
                    }

                    async Task<(NonLazyLoadEntity FromCache, NonLazyLoadEntity FromDb)> GetEntityFrom(IDbQueryCacheHelper dbQueryCacheHelper, TestDbContext dbContext)
                    {
                        var cacheKey = $"cacheKeyAddToCache-{savedEntity.Id}";

                        var cachedEntity = async
                            ? await dbQueryCacheHelper.GetOrAddAsync(typeof(NonLazyLoadEntity), dbContext, () =>
                            {
                                return dbContext.NonLazyLoadEntity.AsNoTracking().FirstAsync(x => x.Id == savedEntity.Id);
                            }, cacheKey)
                            : dbQueryCacheHelper.GetOrAdd(typeof(NonLazyLoadEntity), dbContext, () =>
                            {
                                return dbContext.NonLazyLoadEntity.AsNoTracking().First(x => x.Id == savedEntity.Id);
                            }, cacheKey);

                        var fromDb = await dbContext.NonLazyLoadEntity.AsNoTracking().FirstAsync(x => x.Id == savedEntity.Id);

                        return (cachedEntity, fromDb);
                    }
                }
            });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public virtual async Task Should_Be_Transaction_Aware_On_Rollback(bool async)
        {
            var serviceProvider = CreatePooledProvider(true);

            var entities = Enumerable.Range(0, 2000).Select(static x => new NonLazyLoadEntity
            {
                StringData = $"Test-{x}",
                Value = 0,
            }).ToArray();

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

                await dbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
                await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

                dbContext.NonLazyLoadEntity.AddRange(entities);

                await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await Parallel.ForEachAsync(entities, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4, CancellationToken = TestContext.Current.CancellationToken }, async (savedEntity, cancellationToken) =>
            {
                using var scope1 = serviceProvider.CreateScope();
                using var scope2 = serviceProvider.CreateScope();

                var dbContext1 = scope1.ServiceProvider.GetRequiredService<TestDbContext>();
                var dbContext2 = scope2.ServiceProvider.GetRequiredService<TestDbContext>();

                var dbQueryCacheHelper1 = scope1.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();
                var dbQueryCacheHelper2 = scope2.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();

                using (var transaction = async ? await dbContext1.Database.BeginTransactionAsync(cancellationToken) : dbContext1.Database.BeginTransaction())
                {
                    var entity = await dbContext1.NonLazyLoadEntity.FirstAsync(x => x.Id == savedEntity.Id, cancellationToken: cancellationToken);

                    Assert.Equal(0, entity.Value);

                    entity.Value++;

                    await AssertEntity();

                    if (async)
                    {
                        await dbContext1.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        dbContext1.SaveChanges();
                    }

                    await AssertEntity();
                    await AssertEntity();

                    if (async)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                    }
                    else
                    {
                        transaction.Rollback();
                    }

                    await AssertEntity();
                    await AssertEntity();

                    async Task AssertEntity()
                    {
                        var entitiesFromContext1 = await GetEntityFrom(dbQueryCacheHelper1, dbContext1);
                        Assert.Equal(entitiesFromContext1.FromDb.Value, entitiesFromContext1.FromCache.Value);

                        var entitiesFromContext2 = await GetEntityFrom(dbQueryCacheHelper2, dbContext2);
                        Assert.Equal(entitiesFromContext2.FromDb.Value, entitiesFromContext2.FromCache.Value);
                    }

                    async Task<(NonLazyLoadEntity FromCache, NonLazyLoadEntity FromDb)> GetEntityFrom(IDbQueryCacheHelper dbQueryCacheHelper, TestDbContext dbContext)
                    {
                        var cacheKey = $"cacheKeyAddToCache-{savedEntity.Id}";

                        var cachedEntity = async
                            ? await dbQueryCacheHelper.GetOrAddAsync(typeof(NonLazyLoadEntity), dbContext, () =>
                            {
                                return dbContext.NonLazyLoadEntity.AsNoTracking().FirstAsync(x => x.Id == savedEntity.Id);
                            }, cacheKey)
                            : dbQueryCacheHelper.GetOrAdd(typeof(NonLazyLoadEntity), dbContext, () =>
                            {
                                return dbContext.NonLazyLoadEntity.AsNoTracking().First(x => x.Id == savedEntity.Id);
                            }, cacheKey);

                        var fromDb = await dbContext.NonLazyLoadEntity.AsNoTracking().FirstAsync(x => x.Id == savedEntity.Id);

                        return (cachedEntity, fromDb);
                    }
                }
            });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public virtual async Task Should_Be_Transaction_Aware_On_RollbackToSavepoint(bool async)
        {
            var serviceProvider = CreatePooledProvider(true);

            var entities = Enumerable.Range(0, 2000).Select(static x => new NonLazyLoadEntity
            {
                StringData = $"Test-{x}",
                Value = 0,
            }).ToArray();

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

                await dbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
                await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

                dbContext.NonLazyLoadEntity.AddRange(entities);

                await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await Parallel.ForEachAsync(entities, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4, CancellationToken = TestContext.Current.CancellationToken }, async (savedEntity, cancellationToken) =>
            {
                using var scope1 = serviceProvider.CreateScope();
                using var scope2 = serviceProvider.CreateScope();

                var dbContext1 = scope1.ServiceProvider.GetRequiredService<TestDbContext>();
                var dbContext2 = scope2.ServiceProvider.GetRequiredService<TestDbContext>();

                var dbQueryCacheHelper1 = scope1.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();
                var dbQueryCacheHelper2 = scope2.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();

                using (var transaction = async ? await dbContext1.Database.BeginTransactionAsync(cancellationToken) : dbContext1.Database.BeginTransaction())
                {
                    var entity = await dbContext1.NonLazyLoadEntity.FirstAsync(x => x.Id == savedEntity.Id, cancellationToken: cancellationToken);

                    Assert.Equal(0, entity.Value);

                    entity.Value++;

                    await AssertEntity();

                    var savepointName = $"Savepoint-{savedEntity.Id}";

                    if (async)
                    {
                        await transaction.CreateSavepointAsync(savepointName, cancellationToken);

                        await dbContext1.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        transaction.CreateSavepoint(savepointName);

                        dbContext1.SaveChanges();
                    }

                    await AssertEntity();
                    await AssertEntity();

                    if (async)
                    {
                        await transaction.RollbackToSavepointAsync(savepointName, cancellationToken);
                    }
                    else
                    {
                        transaction.RollbackToSavepoint(savepointName);
                    }

                    await AssertEntity();
                    await AssertEntity();

                    async Task AssertEntity()
                    {
                        var entitiesFromContext1 = await GetEntityFrom(dbQueryCacheHelper1, dbContext1);
                        Assert.Equal(entitiesFromContext1.FromDb.Value, entitiesFromContext1.FromCache.Value);

                        var entitiesFromContext2 = await GetEntityFrom(dbQueryCacheHelper2, dbContext2);
                        Assert.Equal(entitiesFromContext2.FromDb.Value, entitiesFromContext2.FromCache.Value);
                    }

                    async Task<(NonLazyLoadEntity FromCache, NonLazyLoadEntity FromDb)> GetEntityFrom(IDbQueryCacheHelper dbQueryCacheHelper, TestDbContext dbContext)
                    {
                        var cacheKey = $"cacheKeyAddToCache-{savedEntity.Id}";

                        var cachedEntity = async
                            ? await dbQueryCacheHelper.GetOrAddAsync(typeof(NonLazyLoadEntity), dbContext, () =>
                            {
                                return dbContext.NonLazyLoadEntity.AsNoTracking().FirstAsync(x => x.Id == savedEntity.Id);
                            }, cacheKey)
                            : dbQueryCacheHelper.GetOrAdd(typeof(NonLazyLoadEntity), dbContext, () =>
                            {
                                return dbContext.NonLazyLoadEntity.AsNoTracking().First(x => x.Id == savedEntity.Id);
                            }, cacheKey);

                        var fromDb = await dbContext.NonLazyLoadEntity.AsNoTracking().FirstAsync(x => x.Id == savedEntity.Id);

                        return (cachedEntity, fromDb);
                    }
                }
            });
        }
    }
}
