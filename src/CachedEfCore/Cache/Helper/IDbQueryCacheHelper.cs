using CachedEfCore.Context;
using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CachedEfCore.Cache.Helper
{
    public interface IDbQueryCacheHelper
    {
        ReturnType? GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType?> getDataFromDatabase,
            ReadOnlySpan<object> query,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!);

        ReturnType? GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType?> getDataFromDatabase,
            Expression query,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!);

        ReturnType? GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType?> getDataFromDatabase,
            ReadOnlySpan<Expression> query,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!);

        ReturnType? GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType?> getDataFromDatabase,
            string key,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!);


        Task<ReturnType?> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType?>> getDataFromDatabase,
            object[] query,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!);

        Task<ReturnType?> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType?>> getDataFromDatabase,
            Expression query,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!);

        Task<ReturnType?> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType?>> getDataFromDatabase,
            Expression[] query,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!);

        Task<ReturnType?> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType?>> getDataFromDatabase,
            string key,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!);
    }
}
