using CachedEfCore.Context;
using System;
using System.Linq.Expressions;

namespace CachedEfCore.Cache.Helper
{
    public partial interface IDbQueryCacheHelper
    {
        ReturnType GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            ReadOnlySpan<object> query);
        ReturnType GetOrAdd<ReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            ReadOnlySpan<object> query);

        ReturnType GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            Expression query);
        ReturnType GetOrAdd<ReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            Expression query);

        ReturnType GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            ReadOnlySpan<Expression> query);
        ReturnType GetOrAdd<ReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            ReadOnlySpan<Expression> query);

        ReturnType GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            string key);
        ReturnType GetOrAdd<ReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            string key);
    }
}
