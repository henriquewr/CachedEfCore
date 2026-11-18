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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CachedEfCore.Caching.Specification.Tests.DbQueryCacheHelperTests
{
    public abstract class DbQueryCacheHelperSpecificationTest
    {
        protected abstract IServiceProvider CreatePooledProvider(bool withLazyLoading, [CallerMemberName] string caller = "");
        protected abstract IServiceProvider CreateProvider(bool withLazyLoading, [CallerMemberName] string caller = "");

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
                },
                async static (dbContext, transaction, async, cancellationToken) =>
                {
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
                },
                async static (dbContext, transaction, async, cancellationToken) =>
                {
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
            Func<TestDbContext, IDbContextTransaction, bool, CancellationToken, Task> dispose,
            bool async,
            [CallerMemberName] string caller = "")
        {
            var serviceProvider = CreatePooledProvider(false, caller);

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

                await AssertEntity();
                await AssertEntity();

                {
                    var transaction = await createTransaction(dbContext1, configuration.IsolationLevel, async, cancellationToken);
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

                    await dispose(dbContext1, transaction, async, cancellationToken);
                }

                await AssertEntity();
                await AssertEntity();

                async Task AssertEntity()
                {
                    var entitiesFromContext1 = await GetEntityFrom(dbQueryCacheHelper1, dbContext1);
                    Assert.Equal(entitiesFromContext1.FromDb, entitiesFromContext1.FromCache);

                    var entitiesFromContext2 = await GetEntityFrom(dbQueryCacheHelper2, dbContext2);
                    Assert.Equal(entitiesFromContext2.FromDb, entitiesFromContext2.FromCache);

                    async Task<(int FromCache, int FromDb)> GetEntityFrom(IDbQueryCacheHelper dbQueryCacheHelper, TestDbContext dbContext)
                    {
                        var cacheKey = $"cacheKeyAddToCache-{caller}-{savedEntity.Id}";

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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public virtual async Task Should_Get_Cache_Inside_Transaction_When_Saving_Doesnt_Impact_Query(bool async)
        {
            var serviceProvider = CreatePooledProvider(false);

            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

            await dbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
            await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

            var createdEntity = new LazyLoadEntity
            {
                LazyLoadProp = new NonLazyLoadEntity
                {
                    StringData = "Test",
                    Value = 0,
                }
            };

            var anotherNonLazyLoadEntity = new AnotherNonLazyLoadEntity
            {
                StringData = "Test",
                Value = 0,
            };

            dbContext.AnotherNonLazyLoadEntity.Add(anotherNonLazyLoadEntity);
            dbContext.LazyLoadEntity.Add(createdEntity);

            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            var dbQueryCacheHelper = scope.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();

            await AssertLazyLoadEntity(null);
            await AssertLazyLoadEntity(true);

            await AssertNonLazyLoadEntity(null);
            await AssertNonLazyLoadEntity(true);

            await AssertAnotherNonLazyLoadEntity(null);
            await AssertAnotherNonLazyLoadEntity(true);

            using (var transaction = async ? await dbContext.Database.BeginTransactionAsync(TestContext.Current.CancellationToken) : dbContext.Database.BeginTransaction())
            {
                await AssertLazyLoadEntity(true);
                await AssertNonLazyLoadEntity(true);
                await AssertAnotherNonLazyLoadEntity(true);

                var entity = await dbContext.LazyLoadEntity.FirstAsync(x => x.Id == createdEntity.Id, cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal(0, entity.Value);

                entity.Value++;

                if (async)
                {
                    await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
                }
                else
                {
                    dbContext.SaveChanges();
                }

                await AssertLazyLoadEntity(null);
                await AssertLazyLoadEntity(true);

                await AssertNonLazyLoadEntity(null);
                await AssertNonLazyLoadEntity(true);

                await AssertAnotherNonLazyLoadEntity(true);
            }

            async Task AssertLazyLoadEntity(bool? cached)
            {
                var entitiesFromContext = await GetEntityFrom(dbQueryCacheHelper, dbContext);
                Assert.Equal(entitiesFromContext.FromDb, entitiesFromContext.FromCache);

                async Task<(int FromCache, int FromDb)> GetEntityFrom(IDbQueryCacheHelper dbQueryCacheHelper, TestDbContext dbContext)
                {
                    var cacheKey = $"entity-{createdEntity.Id}";
                    bool created = false;

                    var cachedEntity = async
                        ? await dbQueryCacheHelper.GetOrAddAsync(typeof(LazyLoadEntity), dbContext, () =>
                        {
                            created = true;
                            return dbContext.LazyLoadEntity.AsNoTracking().Where(x => x.Id == createdEntity.Id).Select(x => x.Value).FirstAsync();
                        }, cacheKey)
                        : dbQueryCacheHelper.GetOrAdd(typeof(LazyLoadEntity), dbContext, () =>
                        {
                            created = true;
                            return dbContext.LazyLoadEntity.AsNoTracking().Where(x => x.Id == createdEntity.Id).Select(x => x.Value).First();
                        }, cacheKey);

                    if (cached.HasValue)
                    {
                        Assert.True(cached.Value != created, $"Expected value to {(cached.Value ? "" : "not ")}be cached");
                    }

                    var fromDb = await dbContext.LazyLoadEntity.AsNoTracking().Where(x => x.Id == createdEntity.Id).Select(x => x.Value).FirstAsync();

                    return (cachedEntity, fromDb);
                }
            }

            async Task AssertNonLazyLoadEntity(bool? cached)
            {
                var entitiesFromContext = await GetEntityFrom(dbQueryCacheHelper, dbContext);
                Assert.Equal(entitiesFromContext.FromDb, entitiesFromContext.FromCache);

                async Task<(int FromCache, int FromDb)> GetEntityFrom(IDbQueryCacheHelper dbQueryCacheHelper, TestDbContext dbContext)
                {
                    var cacheKey = $"entity-{createdEntity.Id}";
                    bool created = false;

                    var cachedEntity = async
                        ? await dbQueryCacheHelper.GetOrAddAsync(typeof(NonLazyLoadEntity), dbContext, () =>
                        {
                            created = true;
                            return dbContext.NonLazyLoadEntity.AsNoTracking().Where(x => x.Id == createdEntity.Id).Select(x => x.Value).FirstAsync();
                        }, cacheKey)
                        : dbQueryCacheHelper.GetOrAdd(typeof(LazyLoadEntity), dbContext, () =>
                        {
                            created = true;
                            return dbContext.NonLazyLoadEntity.AsNoTracking().Where(x => x.Id == createdEntity.Id).Select(x => x.Value).First();
                        }, cacheKey);

                    if (cached.HasValue)
                    {
                        Assert.True(cached.Value != created, $"Expected value to {(cached.Value ? "" : "not ")}be cached");
                    }

                    var fromDb = await dbContext.NonLazyLoadEntity.AsNoTracking().Where(x => x.Id == createdEntity.Id).Select(x => x.Value).FirstAsync();

                    return (cachedEntity, fromDb);
                }
            }

            async Task AssertAnotherNonLazyLoadEntity(bool? cached)
            {
                var entitiesFromContext = await GetEntityFrom(dbQueryCacheHelper, dbContext);
                Assert.Equal(entitiesFromContext.FromDb, entitiesFromContext.FromCache);

                async Task<(int FromCache, int FromDb)> GetEntityFrom(IDbQueryCacheHelper dbQueryCacheHelper, TestDbContext dbContext)
                {
                    var cacheKey = $"entity-{createdEntity.Id}";
                    bool created = false;

                    var cachedEntity = async
                        ? await dbQueryCacheHelper.GetOrAddAsync(typeof(AnotherNonLazyLoadEntity), dbContext, () =>
                        {
                            created = true;
                            return dbContext.AnotherNonLazyLoadEntity.AsNoTracking().Where(x => x.Id == createdEntity.Id).Select(x => x.Value).FirstAsync();
                        }, cacheKey)
                        : dbQueryCacheHelper.GetOrAdd(typeof(AnotherNonLazyLoadEntity), dbContext, () =>
                        {
                            created = true;
                            return dbContext.AnotherNonLazyLoadEntity.AsNoTracking().Where(x => x.Id == createdEntity.Id).Select(x => x.Value).First();
                        }, cacheKey);

                    if (cached.HasValue)
                    {
                        Assert.True(cached.Value != created, $"Expected value to {(cached.Value ? "" : "not ")}be cached");
                    }

                    var fromDb = await dbContext.AnotherNonLazyLoadEntity.AsNoTracking().Where(x => x.Id == createdEntity.Id).Select(x => x.Value).FirstAsync();

                    return (cachedEntity, fromDb);
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public virtual async Task Getting_From_Cache_Should_Not_Return_Old_Values_When_Multithreaded_Between_Multiple_Scopes(bool async)
        {
            var serviceProvider = CreatePooledProvider(false);

            var savedEntity = new NonLazyLoadEntity
            {
                StringData = $"Test",
                Value = 0,
            };

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

                await dbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
                await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

                dbContext.NonLazyLoadEntity.AddRange(savedEntity);

                await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await Parallel.ForAsync(0, 5000, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2, CancellationToken = TestContext.Current.CancellationToken }, async (i, cancellationToken) =>
            {
                using var scope = serviceProvider.CreateScope();

                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

                var dbQueryCacheHelper = scope.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();

                await AssertEntity(null, dbQueryCacheHelper, dbContext);

                await dbContext.NonLazyLoadEntity.Where(x => x.Id == savedEntity.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.Value, x => x.Value + 1), cancellationToken: cancellationToken);

                await AssertEntity(null, dbQueryCacheHelper, dbContext);
                await AssertEntity(null, dbQueryCacheHelper, dbContext);
            });

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

                var dbQueryCacheHelper = scope.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();

                await AssertEntity(true, dbQueryCacheHelper, dbContext);
                await AssertEntity(true, dbQueryCacheHelper, dbContext);
            }

            async Task AssertEntity(bool? cached, IDbQueryCacheHelper dbQueryCacheHelper, TestDbContext dbContext)
            {
                var entitiesFromContext = await GetEntityFrom(dbQueryCacheHelper, dbContext);
                Assert.True(entitiesFromContext.FromDb <= entitiesFromContext.FromCache, $"Expected FromDb ({entitiesFromContext.FromDb}) to be less than or equal to FromCache ({entitiesFromContext.FromCache})");

                async Task<(int FromCache, int FromDb)> GetEntityFrom(IDbQueryCacheHelper dbQueryCacheHelper, TestDbContext dbContext)
                {
                    var fromDb = await dbContext.NonLazyLoadEntity.AsNoTracking().Where(x => x.Id == savedEntity.Id).Select(x => x.Value).FirstAsync();

                    var cacheKey = $"{nameof(Getting_From_Cache_Should_Not_Return_Old_Values_When_Multithreaded_Between_Multiple_Scopes)}-cacheKeyAddToCache-{savedEntity.Id}";

                    bool created = false;

                    var cachedEntity = async
                        ? await dbQueryCacheHelper.GetOrAddAsync(typeof(NonLazyLoadEntity), dbContext, () =>
                        {
                            created = true;
                            return dbContext.NonLazyLoadEntity.AsNoTracking().Where(x => x.Id == savedEntity.Id).Select(x => x.Value).FirstAsync();
                        }, cacheKey)
                        : dbQueryCacheHelper.GetOrAdd(typeof(NonLazyLoadEntity), dbContext, () =>
                        {
                            created = true;
                            return dbContext.NonLazyLoadEntity.AsNoTracking().Where(x => x.Id == savedEntity.Id).Select(x => x.Value).First();
                        }, cacheKey);

                    if (cached.HasValue)
                    {
                        Assert.True(cached.Value != created, $"Expected value to {(cached.Value ? "" : "not ")}be cached");
                    }

                    return (cachedEntity, fromDb);
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public virtual async Task Getting_From_Cache_Should_Not_Return_Old_Values_When_Multithreaded_Between_Multiple_Scopes_And_Waits_To_Return(bool async)
        {
            var serviceProvider = CreatePooledProvider(false);

            var savedEntity = new NonLazyLoadEntity
            {
                StringData = $"Test",
                Value = 0,
            };

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

                await dbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
                await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

                dbContext.NonLazyLoadEntity.AddRange(savedEntity);

                await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await Parallel.ForAsync(0, 1000, new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = TestContext.Current.CancellationToken }, async (i, cancellationToken) =>
            {
                using var scope = serviceProvider.CreateScope();

                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

                var dbQueryCacheHelper = scope.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();

                await AssertEntity(null, dbQueryCacheHelper, dbContext);

                await dbContext.NonLazyLoadEntity.Where(x => x.Id == savedEntity.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.Value, x => x.Value + 1), cancellationToken: cancellationToken);

                await AssertEntity(null, dbQueryCacheHelper, dbContext);
                await AssertEntity(null, dbQueryCacheHelper, dbContext);
            });

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

                var dbQueryCacheHelper = scope.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();

                await AssertEntity(true, dbQueryCacheHelper, dbContext);
                await AssertEntity(true, dbQueryCacheHelper, dbContext);
            }

            async Task AssertEntity(bool? cached, IDbQueryCacheHelper dbQueryCacheHelper, TestDbContext dbContext)
            {
                var entitiesFromContext = await GetEntityFrom(dbQueryCacheHelper, dbContext);
                Assert.True(entitiesFromContext.FromDb <= entitiesFromContext.FromCache, $"Expected FromDb ({entitiesFromContext.FromDb}) to be less than or equal to FromCache ({entitiesFromContext.FromCache})");

                async Task<(int FromCache, int FromDb)> GetEntityFrom(IDbQueryCacheHelper dbQueryCacheHelper, TestDbContext dbContext)
                {
                    var fromDb = await dbContext.NonLazyLoadEntity.AsNoTracking().Where(x => x.Id == savedEntity.Id).Select(x => x.Value).FirstAsync();

                    var cacheKey = $"{nameof(Getting_From_Cache_Should_Not_Return_Old_Values_When_Multithreaded_Between_Multiple_Scopes_And_Waits_To_Return)}-cacheKeyAddToCache-{savedEntity.Id}";

                    bool created = false;

                    var cachedEntity = async
                        ? await dbQueryCacheHelper.GetOrAddAsync(typeof(NonLazyLoadEntity), dbContext, async () =>
                        {
                            created = true;
                            var value = await dbContext.NonLazyLoadEntity.AsNoTracking().Where(x => x.Id == savedEntity.Id).Select(x => x.Value).FirstAsync();
                            await Task.Delay(50);
                            return value;
                        }, cacheKey)
                        : dbQueryCacheHelper.GetOrAdd(typeof(NonLazyLoadEntity), dbContext, () =>
                        {
                            created = true;
                            var value = dbContext.NonLazyLoadEntity.AsNoTracking().Where(x => x.Id == savedEntity.Id).Select(x => x.Value).First();
                            Thread.Sleep(50);
                            return value;
                        }, cacheKey);

                    if (cached.HasValue)
                    {
                        Assert.True(cached.Value != created, $"Expected value to {(cached.Value ? "" : "not ")}be cached");
                    }

                    return (cachedEntity, fromDb);
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public virtual async Task Getting_From_Cache_Should_Not_Return_Old_Values_When_Multithread_Between_Multiple_Scopes_And_Transaction_On_Commit(bool async)
        {
            await Getting_From_Cache_Should_Not_Return_Old_Values_When_Multithread_Between_Multiple_Scopes_And_Transaction(async,
                async (async, dbContext, transaction) =>
                {
                    if (async)
                    {
                        await transaction.CommitAsync(TestContext.Current.CancellationToken);
                    }
                    else
                    {
                        transaction.Commit();
                    }
                });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public virtual async Task Getting_From_Cache_Should_Not_Return_Old_Values_When_Multithread_Between_Multiple_Scopes_And_Transaction_On_Rollback(bool async)
        {
            await Getting_From_Cache_Should_Not_Return_Old_Values_When_Multithread_Between_Multiple_Scopes_And_Transaction(async,
                async (async, dbContext, transaction) =>
                {
                    if (async)
                    {
                        await transaction.RollbackAsync(TestContext.Current.CancellationToken);
                    }
                    else
                    {
                        transaction.Rollback();
                    }
                });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public virtual async Task Getting_From_Cache_Should_Not_Return_Old_Values_When_Multithread_Between_Multiple_Scopes_And_Transaction_On_Dispose(bool async)
        {
            await Getting_From_Cache_Should_Not_Return_Old_Values_When_Multithread_Between_Multiple_Scopes_And_Transaction(async,
                async (async, dbContext, transaction) =>
                {
                    if (async)
                    {
                        await transaction.DisposeAsync();
                    }
                    else
                    {
                        transaction.Dispose();
                    }
                });
        }

        protected virtual async Task Getting_From_Cache_Should_Not_Return_Old_Values_When_Multithread_Between_Multiple_Scopes_And_Transaction(bool async, Func<bool, DbContext, IDbContextTransaction, Task> transactionFunc, [CallerMemberName] string caller = "")
        {
            var serviceProvider = CreatePooledProvider(false, caller);
            var savedEntity = new NonLazyLoadEntity
            {
                StringData = $"Test",
                Value = 0,
            };
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                await dbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
                await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

                dbContext.NonLazyLoadEntity.Add(savedEntity);

                await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var readTask = Parallel.ForAsync(0, 2000, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = TestContext.Current.CancellationToken }, async (i, cancellationToken) =>
            {
                await start.Task.WaitAsync(cancellationToken);

                using var scope = serviceProvider.CreateScope();

                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var dbQueryCacheHelper = scope.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();

                await AssertEntity(null, dbQueryCacheHelper, dbContext);
                await AssertEntity(null, dbQueryCacheHelper, dbContext);

                await dbContext.NonLazyLoadEntity.Where(x => x.Id == savedEntity.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.Value, x => x.Value + 1), cancellationToken: cancellationToken);

                await AssertEntity(null, dbQueryCacheHelper, dbContext);
                await AssertEntity(null, dbQueryCacheHelper, dbContext);
            });

            var transactionTask = Parallel.ForAsync(0, 2000, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = TestContext.Current.CancellationToken }, async (i, cancellationToken) =>
            {
                await start.Task.WaitAsync(cancellationToken);

                using var scope = serviceProvider.CreateScope();

                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var dbQueryCacheHelper = scope.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();

                await AssertEntity(null, dbQueryCacheHelper, dbContext);
                await AssertEntity(null, dbQueryCacheHelper, dbContext);

                using (var transaction = async ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : dbContext.Database.BeginTransaction())
                {
                    await dbContext.NonLazyLoadEntity.Where(x => x.Id == savedEntity.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.Value, x => x.Value + 1), cancellationToken: cancellationToken);
                    
                    await AssertEntity(null, dbQueryCacheHelper, dbContext);
                    await AssertEntity(null, dbQueryCacheHelper, dbContext);

                    await transactionFunc(async, dbContext, transaction).ConfigureAwait(false);

                    await AssertEntity(null, dbQueryCacheHelper, dbContext);
                    await AssertEntity(null, dbQueryCacheHelper, dbContext);
                }

                await AssertEntity(null, dbQueryCacheHelper, dbContext);
                await AssertEntity(null, dbQueryCacheHelper, dbContext);
            });

            start.SetResult();

            await Task.WhenAll(transactionTask, readTask);

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var dbQueryCacheHelper = scope.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();
                await AssertEntity(true, dbQueryCacheHelper, dbContext);
                await AssertEntity(true, dbQueryCacheHelper, dbContext);
            }

            async Task AssertEntity(bool? cached, IDbQueryCacheHelper dbQueryCacheHelper, TestDbContext dbContext)
            {
                var entitiesFromContext = await GetEntityFrom(dbQueryCacheHelper, dbContext);
                Assert.True(entitiesFromContext.FromDb <= entitiesFromContext.FromCache, $"Expected FromDb ({entitiesFromContext.FromDb}) to be less than or equal to FromCache ({entitiesFromContext.FromCache})");

                async Task<(int FromCache, int FromDb)> GetEntityFrom(IDbQueryCacheHelper dbQueryCacheHelper, TestDbContext dbContext)
                {
                    var fromDb = await dbContext.NonLazyLoadEntity.AsNoTracking().Where(x => x.Id == savedEntity.Id).Select(x => x.Value).FirstAsync();

                    var cacheKey = $"{nameof(Getting_From_Cache_Should_Not_Return_Old_Values_When_Multithread_Between_Multiple_Scopes_And_Transaction)}-cacheKeyAddToCache-{caller}-{savedEntity.Id}";

                    bool created = false;

                    var cachedEntity = async
                        ? await dbQueryCacheHelper.GetOrAddAsync(typeof(NonLazyLoadEntity), dbContext, () =>
                        {
                            created = true;
                            return dbContext.NonLazyLoadEntity.AsNoTracking().Where(x => x.Id == savedEntity.Id).Select(x => x.Value).FirstAsync();
                        }, cacheKey)
                        : dbQueryCacheHelper.GetOrAdd(typeof(NonLazyLoadEntity), dbContext, () =>
                        {
                            created = true;
                            return dbContext.NonLazyLoadEntity.AsNoTracking().Where(x => x.Id == savedEntity.Id).Select(x => x.Value).First();
                        }, cacheKey);

                    if (cached.HasValue)
                    {
                        Assert.True(cached.Value != created, $"Expected value to {(cached.Value ? "" : "not ")}be cached");
                    }

                    return (cachedEntity, fromDb);
                }
            }
        }
    }
}
