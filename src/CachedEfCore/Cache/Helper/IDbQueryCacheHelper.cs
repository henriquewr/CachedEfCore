using CachedEfCore.Context;
using System;
using System.Linq.Expressions;

namespace CachedEfCore.Cache.Helper
{
    public partial interface IDbQueryCacheHelper
    {
        TReturnType GetOrAdd<TReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<TReturnType> getDataFromDatabase,
            ReadOnlySpan<object> query);
        TReturnType GetOrAdd<TReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<TReturnType> getDataFromDatabase,
            ReadOnlySpan<object> query);

        TReturnType GetOrAdd<TReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<TReturnType> getDataFromDatabase,
            Expression query);
        TReturnType GetOrAdd<TReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<TReturnType> getDataFromDatabase,
            Expression query);

        TReturnType GetOrAdd<TReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<TReturnType> getDataFromDatabase,
            ReadOnlySpan<Expression> query);
        TReturnType GetOrAdd<TReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<TReturnType> getDataFromDatabase,
            ReadOnlySpan<Expression> query);

        TReturnType GetOrAdd<TReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<TReturnType> getDataFromDatabase,
            string key);
        TReturnType GetOrAdd<TReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<TReturnType> getDataFromDatabase,
            string key);
    }
}
