using CachedEfCore.Context;
using CachedEfCore.ExpressionKeyGen;
using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CachedEfCore.Cache.Helper
{
    public class DbQueryCacheHelper : IDbQueryCacheHelper
    {
        private readonly KeyGeneratorVisitor _keyGeneratorVisitor;

        public DbQueryCacheHelper(KeyGeneratorVisitor keyGeneratorVisitor)
        {
            _keyGeneratorVisitor = keyGeneratorVisitor;
        }

        public ReturnType? GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType?> getDataFromDatabase,
            ReadOnlySpan<object> query,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!)
        {
            var key = "";

            for (var i = 0; i < query.Length; i++)
            {
                var queryItem = query[i];

                if (queryItem is Expression expr)
                {
                    var keyGenerated = _keyGeneratorVisitor.SafeExpressionToStringIfPrintable(expr);
                    if (keyGenerated is null)
                    {
                        return getDataFromDatabase();
                    }

                    key += keyGenerated;
                }
                else
                {
                    key += queryItem.ToString();
                }
            }

            return GetOrAdd<ReturnType, TEntity>(dbContext, getDataFromDatabase, key, getDataFromDatabaseStr);
        }

        public ReturnType? GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType?> getDataFromDatabase,
            Expression query,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!)
        {
            var keyGenerated = _keyGeneratorVisitor.SafeExpressionToStringIfPrintable(query);
            if (keyGenerated is null)
            {
                return getDataFromDatabase();
            }

            return GetOrAdd<ReturnType, TEntity>(dbContext, getDataFromDatabase, keyGenerated, getDataFromDatabaseStr);
        }


        public ReturnType? GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType?> getDataFromDatabase,
            ReadOnlySpan<Expression> query,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!)
        {
            var key = "";

            for (var i = 0; i < query.Length; i++)
            {
                var queryItem = query[i];

                var keyGenerated = _keyGeneratorVisitor.SafeExpressionToStringIfPrintable(queryItem);
                if (keyGenerated is null)
                {
                    return getDataFromDatabase();
                }

                key += keyGenerated;
            }

            return GetOrAdd<ReturnType, TEntity>(dbContext, getDataFromDatabase, key, getDataFromDatabaseStr);
        }

        public ReturnType? GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType?> getDataFromDatabase,
            string key,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!)
        {
            var cacheKey = GenCacheKey(key, getDataFromDatabaseStr);

            var result = dbContext.DbQueryCacheStore.GetOrAdd(dbContext.Id, typeof(TEntity), cacheKey, getDataFromDatabase);

            return result;
        }



        public Task<ReturnType?> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType?>> getDataFromDatabase,
            object[] query,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!)
        {
            var key = "";

            for (var i = 0; i < query.Length; i++)
            {
                var queryItem = query[i];

                if (queryItem is Expression expr)
                {
                    var keyGenerated = _keyGeneratorVisitor.SafeExpressionToStringIfPrintable(expr);
                    if (keyGenerated is null)
                    {
                        return getDataFromDatabase();
                    }

                    key += keyGenerated;
                }
                else
                {
                    key += queryItem.ToString();
                }
            }

            return GetOrAddAsync<ReturnType, TEntity>(dbContext, getDataFromDatabase, key, getDataFromDatabaseStr);
        }

        public Task<ReturnType?> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType?>> getDataFromDatabase,
            Expression query,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!)
        {
            var keyGenerated = _keyGeneratorVisitor.SafeExpressionToStringIfPrintable(query);
            if (keyGenerated is null)
            {
                return getDataFromDatabase();
            }

            return GetOrAddAsync<ReturnType, TEntity>(dbContext, getDataFromDatabase, keyGenerated, getDataFromDatabaseStr);
        }

        public Task<ReturnType?> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType?>> getDataFromDatabase,
            Expression[] query,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!)
        {
            var key = "";

            for (var i = 0; i < query.Length; i++)
            {
                var queryItem = query[i];

                var keyGenerated = _keyGeneratorVisitor.SafeExpressionToStringIfPrintable(queryItem);
                if (keyGenerated is null)
                {
                    return getDataFromDatabase();
                }

                key += keyGenerated;
            }

            return GetOrAddAsync<ReturnType, TEntity>(dbContext, getDataFromDatabase, key, getDataFromDatabaseStr);
        }

        public Task<ReturnType?> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType?>> getDataFromDatabase,
            string key,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!)
        {
            var cacheKey = GenCacheKey(key, getDataFromDatabaseStr);
            var result = dbContext.DbQueryCacheStore.GetOrAddAsync(dbContext.Id, typeof(TEntity), cacheKey, getDataFromDatabase);

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GenCacheKey(string query, string getFromDb)
        {
            return $"{getFromDb}.{query}";
        }
    }
}
