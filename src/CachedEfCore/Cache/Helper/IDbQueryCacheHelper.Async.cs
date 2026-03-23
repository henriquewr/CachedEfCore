using CachedEfCore.Context;
using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CachedEfCore.Cache.Helper
{
    public partial interface IDbQueryCacheHelper
    {
        ValueTask<ReturnType> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType>> getDataFromDatabase,
            object[] query);
        ValueTask<ReturnType> GetOrAddAsync<ReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<Task<ReturnType>> getDataFromDatabase,
            object[] query);

        ValueTask<ReturnType> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType>> getDataFromDatabase,
            Expression query);
        ValueTask<ReturnType> GetOrAddAsync<ReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<Task<ReturnType>> getDataFromDatabase,
            Expression query);

        ValueTask<ReturnType> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType>> getDataFromDatabase,
            Expression[] query);
        ValueTask<ReturnType> GetOrAddAsync<ReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<Task<ReturnType>> getDataFromDatabase,
            Expression[] query);

        ValueTask<ReturnType> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType>> getDataFromDatabase,
            string key);
        ValueTask<ReturnType> GetOrAddAsync<ReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<Task<ReturnType>> getDataFromDatabase,
            string key);
    }
}
