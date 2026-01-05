using CachedEfCore.Context;
using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CachedEfCore.Cache.Helper
{
    public interface IDbQueryCacheHelper
    {
        ReturnType GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            ReadOnlySpan<object> query);

        ReturnType GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            Expression query);

        ReturnType GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            ReadOnlySpan<Expression> query);

        ReturnType GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            string key);


        ValueTask<ReturnType> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType>> getDataFromDatabase,
            object[] query);

        ValueTask<ReturnType> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType>> getDataFromDatabase,
            Expression query);

        ValueTask<ReturnType> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType>> getDataFromDatabase,
            Expression[] query);

        ValueTask<ReturnType> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType>> getDataFromDatabase,
            string key);
    }
}
