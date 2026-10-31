using CachedEfCore.Cache.Helper;
using CachedEfCore.Cache.Metrics;
using CachedEfCore.Caching.Specification.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
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
        [InlineData(true, true)]
        [InlineData(false, true)]

        [InlineData(true, false)]
        [InlineData(false, false)]
        public virtual async Task Should_Be_Transaction_Aware_On_Commit_When_Calling_UseTransaction(bool async, bool dirtyReads)
        {
            var configuration = ShouldBeTransactionAwareOnFuncConfiguration.Create(dirtyReads);

            await ShouldBeTransactionAwareOnFunc(configuration,
                async static (dbContext, isolationLevel, async, cancellationToken) =>
                {
                    DbConnection connection = dbContext.Database.GetDbConnection();

                    await connection.OpenAsync();

                    var dbTransaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken);

                    var transaction = async ? (await dbContext.Database.UseTransactionAsync(dbTransaction, cancellationToken))! : dbContext.Database.UseTransaction(dbTransaction)!;

                    return transaction;
                },
                async static (dbContext, entity, transaction, async, cancellationToken) =>
                {
                    if (async)
                    {
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        dbContext.SaveChanges();
                    }
                },
                async static (dbContext, transaction, async, cancellationToken) =>
                {
                    if (async)
                    {
                        await transaction.CommitAsync(cancellationToken);
                    }
                    else
                    {
                        transaction.Commit();
                    }
                }, async);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, true)]

        [InlineData(true, false)]
        [InlineData(false, false)]
        public virtual async Task Should_Be_Transaction_Aware_On_Rollback_When_Calling_UseTransaction(bool async, bool dirtyReads)
        {
            var configuration = ShouldBeTransactionAwareOnFuncConfiguration.Create(dirtyReads);

            await ShouldBeTransactionAwareOnFunc(configuration,
                async static (dbContext, isolationLevel, async, cancellationToken) =>
                {
                    DbConnection connection = dbContext.Database.GetDbConnection();

                    await connection.OpenAsync();

                    var dbTransaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken);

                    var transaction = async ? (await dbContext.Database.UseTransactionAsync(dbTransaction, cancellationToken))! : dbContext.Database.UseTransaction(dbTransaction)!;

                    return transaction;
                },
                async static (dbContext, entity, transaction, async, cancellationToken) =>
                {
                    if (async)
                    {
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        dbContext.SaveChanges();
                    }
                },
                async static (dbContext, transaction, async, cancellationToken) =>
                {
                    if (async)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                    }
                    else
                    {
                        transaction.Rollback();
                    }
                }, async);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, true)]

        [InlineData(true, false)]
        [InlineData(false, false)]
        public virtual async Task Should_Be_Transaction_Aware_On_RollbackToSavepoint_When_Calling_UseTransaction(bool async, bool dirtyReads)
        {
            var configuration = ShouldBeTransactionAwareOnFuncConfiguration.Create(dirtyReads);

            await ShouldBeTransactionAwareOnFunc(configuration,
                async static (dbContext, isolationLevel, async, cancellationToken) =>
                {
                    DbConnection connection = dbContext.Database.GetDbConnection();

                    await connection.OpenAsync();

                    var dbTransaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken);

                    var transaction = async ? (await dbContext.Database.UseTransactionAsync(dbTransaction, cancellationToken))! : dbContext.Database.UseTransaction(dbTransaction)!;

                    return transaction;
                },
                async static (dbContext, entity, transaction, async, cancellationToken) =>
                {
                    var savepointName = transaction.TransactionId.ToString("N");

                    if (async)
                    {
                        await transaction.CreateSavepointAsync(savepointName, cancellationToken);

                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        transaction.CreateSavepoint(savepointName);

                        dbContext.SaveChanges();
                    }
                },
                async static (dbContext, transaction, async, cancellationToken) =>
                {
                    var savepointName = transaction.TransactionId.ToString("N");

                    if (async)
                    {
                        await transaction.RollbackToSavepointAsync(savepointName, cancellationToken);
                    }
                    else
                    {
                        transaction.RollbackToSavepoint(savepointName);
                    }

                }, async);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, true)]

        [InlineData(true, false)]
        [InlineData(false, false)]
        public virtual async Task Should_Be_Transaction_Aware_On_Dispose_When_Calling_UseTransaction(bool async, bool dirtyReads)
        {
            var configuration = ShouldBeTransactionAwareOnFuncConfiguration.Create(dirtyReads);

            await ShouldBeTransactionAwareOnFunc(configuration,
                async static (dbContext, isolationLevel, async, cancellationToken) =>
                {
                    DbConnection connection = dbContext.Database.GetDbConnection();

                    await connection.OpenAsync();

                    var dbTransaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken);

                    var transaction = async ? (await dbContext.Database.UseTransactionAsync(dbTransaction, cancellationToken))! : dbContext.Database.UseTransaction(dbTransaction)!;

                    return transaction;
                },
                async static (dbContext, entity, transaction, async, cancellationToken) =>
                {
                    if (async)
                    {
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        dbContext.SaveChanges();
                    }
                },
                async static (dbContext, transaction, async, cancellationToken) =>
                {
                    var dbTransaction = transaction.GetDbTransaction();

                    if (async)
                    {
                        await transaction.DisposeAsync();
                        await dbTransaction.DisposeAsync();
                    }
                    else
                    {
                        transaction.Dispose();
                        dbTransaction.Dispose();
                    }
                }, async);
        }


        [Theory]
        [InlineData(true, true)]
        [InlineData(false, true)]

        [InlineData(true, false)]
        [InlineData(false, false)]
        public virtual async Task Should_Be_Transaction_Aware_On_Commit(bool async, bool dirtyReads)
        {
            var configuration = ShouldBeTransactionAwareOnFuncConfiguration.Create(dirtyReads);

            await ShouldBeTransactionAwareOnFunc(configuration,
                async static (dbContext, isolationLevel, async, cancellationToken) =>
                {
                    return async
                        ? await dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken)
                        : dbContext.Database.BeginTransaction(isolationLevel);
                },
                async static (dbContext, entity, transaction, async, cancellationToken) =>
                {
                    if (async)
                    {
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        dbContext.SaveChanges();
                    }
                },
                async static (dbContext, transaction, async, cancellationToken) =>
                {
                    if (async)
                    {
                        await transaction.CommitAsync(cancellationToken);
                    }
                    else
                    {
                        transaction.Commit();
                    }
                }, async);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, true)]

        [InlineData(true, false)]
        [InlineData(false, false)]
        public virtual async Task Should_Be_Transaction_Aware_On_Rollback(bool async, bool dirtyReads)
        {
            var configuration = ShouldBeTransactionAwareOnFuncConfiguration.Create(dirtyReads);

            await ShouldBeTransactionAwareOnFunc(configuration,
                async static (dbContext, isolationLevel, async, cancellationToken) =>
                {
                    return async
                        ? await dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken)
                        : dbContext.Database.BeginTransaction(isolationLevel);
                },
                async static (dbContext, entity, transaction, async, cancellationToken) =>
                {
                    if (async)
                    {
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        dbContext.SaveChanges();
                    }
                },
                async static (dbContext, transaction, async, cancellationToken) =>
                {
                    if (async)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                    }
                    else
                    {
                        transaction.Rollback();
                    }
                }, async);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, true)]

        [InlineData(true, false)]
        [InlineData(false, false)]
        public virtual async Task Should_Be_Transaction_Aware_On_RollbackToSavepoint(bool async, bool dirtyReads)
        {
            var configuration = ShouldBeTransactionAwareOnFuncConfiguration.Create(dirtyReads);

            await ShouldBeTransactionAwareOnFunc(configuration,
                async static (dbContext, isolationLevel, async, cancellationToken) =>
                {
                    return async
                        ? await dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken)
                        : dbContext.Database.BeginTransaction(isolationLevel);
                },
                async static (dbContext, entity, transaction, async, cancellationToken) =>
                {
                    var savepointName = transaction.TransactionId.ToString("N");

                    if (async)
                    {
                        await transaction.CreateSavepointAsync(savepointName, cancellationToken);

                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        transaction.CreateSavepoint(savepointName);

                        dbContext.SaveChanges();
                    }
                },
                async static (dbContext, transaction, async, cancellationToken) =>
                {
                    var savepointName = transaction.TransactionId.ToString("N");

                    if (async)
                    {
                        await transaction.RollbackToSavepointAsync(savepointName, cancellationToken);
                    }
                    else
                    {
                        transaction.RollbackToSavepoint(savepointName);
                    }

                }, async);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, true)]

        [InlineData(true, false)]
        [InlineData(false, false)]
        public virtual async Task Should_Be_Transaction_Aware_On_Dispose(bool async, bool dirtyReads)
        {
            var configuration = ShouldBeTransactionAwareOnFuncConfiguration.Create(dirtyReads);

            await ShouldBeTransactionAwareOnFunc(configuration,
                async static (dbContext, isolationLevel, async, cancellationToken) =>
                {
                    return async
                        ? await dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken)
                        : dbContext.Database.BeginTransaction(isolationLevel);
                },
                async static (dbContext, entity, transaction, async, cancellationToken) =>
                {
                    if (async)
                    {
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        dbContext.SaveChanges();
                    }
                },
                async static (dbContext, transaction, async, cancellationToken) =>
                {
                    if (async)
                    {
                        await transaction.DisposeAsync();
                    }
                    else
                    {
                        transaction.Dispose();
                    }
                }, async);
        }


        protected record struct ShouldBeTransactionAwareOnFuncConfiguration(int Iterations, int MaxDegreeOfParallelism, IsolationLevel IsolationLevel)
        {
            public static ShouldBeTransactionAwareOnFuncConfiguration Create(bool dirtyReads)
            {
                var configuration = dirtyReads
                   ? new ShouldBeTransactionAwareOnFuncConfiguration(50, 1, IsolationLevel.ReadUncommitted)
                   : new ShouldBeTransactionAwareOnFuncConfiguration(2000, Environment.ProcessorCount * 2, IsolationLevel.ReadCommitted);

                return configuration;
            }
        }

        protected virtual async Task ShouldBeTransactionAwareOnFunc(
            ShouldBeTransactionAwareOnFuncConfiguration configuration,
            Func<TestDbContext, IsolationLevel, bool, CancellationToken, Task<IDbContextTransaction>> createTransaction,
            Func<TestDbContext, NonLazyLoadEntity, IDbContextTransaction, bool, CancellationToken, Task> saveFunction,
            Func<TestDbContext, IDbContextTransaction, bool, CancellationToken, Task> function,
            bool async)
        {
            var serviceProvider = CreatePooledProvider(true);

            var entities = Enumerable.Range(0, configuration.Iterations).Select(static x => new NonLazyLoadEntity
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

            await Parallel.ForEachAsync(entities, new ParallelOptions { MaxDegreeOfParallelism = configuration.MaxDegreeOfParallelism, CancellationToken = TestContext.Current.CancellationToken }, async (savedEntity, cancellationToken) =>
            {
                using var scope1 = serviceProvider.CreateScope();
                using var scope2 = serviceProvider.CreateScope();

                var dbContext1 = scope1.ServiceProvider.GetRequiredService<TestDbContext>();
                var dbContext2 = scope2.ServiceProvider.GetRequiredService<TestDbContext>();

                var dbQueryCacheHelper1 = scope1.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();
                var dbQueryCacheHelper2 = scope2.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();

                using (var transaction = await createTransaction(dbContext1, configuration.IsolationLevel, async, cancellationToken))
                {
                    var entity = await dbContext1.NonLazyLoadEntity.FirstAsync(x => x.Id == savedEntity.Id, cancellationToken: cancellationToken);

                    Assert.Equal(0, entity.Value);

                    entity.Value++;

                    await AssertEntity();

                    await saveFunction(dbContext1, entity, transaction, async, cancellationToken);

                    await AssertEntity();
                    await AssertEntity();

                    await function(dbContext1, transaction, async, cancellationToken);

                    await AssertEntity();
                    await AssertEntity();
                }

                async Task AssertEntity()
                {
                    var entitiesFromContext1 = await GetEntityFrom(dbQueryCacheHelper1, dbContext1);
                    Assert.Equal(entitiesFromContext1.FromDb, entitiesFromContext1.FromCache);

                    var entitiesFromContext2 = await GetEntityFrom(dbQueryCacheHelper2, dbContext2);
                    Assert.Equal(entitiesFromContext2.FromDb, entitiesFromContext2.FromCache);

                    async Task<(int FromCache, int FromDb)> GetEntityFrom(IDbQueryCacheHelper dbQueryCacheHelper, TestDbContext dbContext)
                    {
                        var cacheKey = $"cacheKeyAddToCache-{savedEntity.Id}";

                        var cachedEntity = async
                            ? await dbQueryCacheHelper.GetOrAddAsync(typeof(NonLazyLoadEntity), dbContext, () =>
                            {
                                return dbContext.NonLazyLoadEntity.AsNoTracking().Where(x => x.Id == savedEntity.Id).Select(x => x.Value).FirstAsync();
                            }, cacheKey)
                            : dbQueryCacheHelper.GetOrAdd(typeof(NonLazyLoadEntity), dbContext, () =>
                            {
                                return dbContext.NonLazyLoadEntity.AsNoTracking().Where(x => x.Id == savedEntity.Id).Select(x => x.Value).First();
                            }, cacheKey);

                        var fromDb = await dbContext.NonLazyLoadEntity.AsNoTracking().Where(x => x.Id == savedEntity.Id).Select(x => x.Value).FirstAsync();

                        return (cachedEntity, fromDb);
                    }
                }
            });
        }
    }
}
