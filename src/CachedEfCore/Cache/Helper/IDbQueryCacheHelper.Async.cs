using CachedEfCore.Context;
using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CachedEfCore.Cache.Helper
{
    public partial interface IDbQueryCacheHelper
    {
        ValueTask<TReturnType> GetOrAddAsync<TReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            object[] query);
        ValueTask<TReturnType> GetOrAddAsync<TReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            object[] query);

        ValueTask<TReturnType> GetOrAddAsync<TReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            Expression query);
        ValueTask<TReturnType> GetOrAddAsync<TReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            Expression query);

        ValueTask<TReturnType> GetOrAddAsync<TReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            Expression[] query);
        ValueTask<TReturnType> GetOrAddAsync<TReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            Expression[] query);

        ValueTask<TReturnType> GetOrAddAsync<TReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            string key);
        ValueTask<TReturnType> GetOrAddAsync<TReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            string key);
    }
}
