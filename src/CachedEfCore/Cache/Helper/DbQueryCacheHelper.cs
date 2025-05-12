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
            var expression = "";
            var additionalJson = "";

            for (var i = 0; i < query.Length; i++)
            {
                var queryItem = query[i];

                if (queryItem is Expression expr)
                {
                    var keyGenerated = _keyGeneratorVisitor.SafeExpressionToString(expr);
                    if (keyGenerated is null)
                    {
                        return getDataFromDatabase();
                    }

                    expression += keyGenerated.Value.Expression;
                    if (keyGenerated.Value.AdditionalJson != null)
                    {
                        additionalJson += keyGenerated.Value.AdditionalJson;
                    }
                }
                else
                {
                    expression += queryItem.ToString();
                }
            }

            var entityType = typeof(TEntity);

            var cacheKey = new DbQueryCacheKey(entityType.FullName!, expression, additionalJson, getDataFromDatabaseStr);
            var result = dbContext.DbQueryCacheStore.GetOrAdd(dbContext.Id, entityType, cacheKey, getDataFromDatabase);

            return result;
        }

        public ReturnType? GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType?> getDataFromDatabase,
            Expression query,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!)
        {
            var keyGenerated = _keyGeneratorVisitor.SafeExpressionToString(query);
            if (keyGenerated is null)
            {
                return getDataFromDatabase();
            }

            var entityType = typeof(TEntity);

            var cacheKey = new DbQueryCacheKey(entityType.FullName!, keyGenerated.Value.Expression, keyGenerated.Value.AdditionalJson, getDataFromDatabaseStr);
            var result = dbContext.DbQueryCacheStore.GetOrAdd(dbContext.Id, entityType, cacheKey, getDataFromDatabase);

            return result;
        }

        public ReturnType? GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType?> getDataFromDatabase,
            ReadOnlySpan<Expression> query,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!)
        {
            var expression = "";
            var additionalJson = "";

            for (var i = 0; i < query.Length; i++)
            {
                var queryItem = query[i];

                var keyGenerated = _keyGeneratorVisitor.SafeExpressionToString(queryItem);
                if (keyGenerated is null)
                {
                    return getDataFromDatabase();
                }

                expression += keyGenerated.Value.Expression;
                if (keyGenerated.Value.AdditionalJson != null)
                {
                    additionalJson += keyGenerated.Value.AdditionalJson;
                }
            }

            var entityType = typeof(TEntity);

            var cacheKey = new DbQueryCacheKey(entityType.FullName!, expression, additionalJson, getDataFromDatabaseStr);
            var result = dbContext.DbQueryCacheStore.GetOrAdd(dbContext.Id, entityType, cacheKey, getDataFromDatabase);

            return result;
        }

        public ReturnType? GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType?> getDataFromDatabase,
            string key,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!)
        {
            var entityType = typeof(TEntity);

            var cacheKey = new DbQueryCacheKey(entityType.FullName!, key, null, getDataFromDatabaseStr);
            var result = dbContext.DbQueryCacheStore.GetOrAdd(dbContext.Id, entityType, cacheKey, getDataFromDatabase);

            return result;
        }



        public Task<ReturnType?> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType?>> getDataFromDatabase,
            object[] query,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!)
        {
            var expression = "";
            var additionalJson = "";

            for (var i = 0; i < query.Length; i++)
            {
                var queryItem = query[i];

                if (queryItem is Expression expr)
                {
                    var keyGenerated = _keyGeneratorVisitor.SafeExpressionToString(expr);
                    if (keyGenerated is null)
                    {
                        return getDataFromDatabase();
                    }

                    expression += keyGenerated.Value.Expression;
                    if (keyGenerated.Value.AdditionalJson != null)
                    {
                        additionalJson += keyGenerated.Value.AdditionalJson;
                    }
                }
                else
                {
                    expression += queryItem.ToString();
                }
            }

            var entityType = typeof(TEntity);

            var cacheKey = new DbQueryCacheKey(entityType.FullName!, expression, additionalJson, getDataFromDatabaseStr);
            var result = dbContext.DbQueryCacheStore.GetOrAddAsync(dbContext.Id, entityType, cacheKey, getDataFromDatabase);

            return result;
        }

        public Task<ReturnType?> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType?>> getDataFromDatabase,
            Expression query,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!)
        {
            var keyGenerated = _keyGeneratorVisitor.SafeExpressionToString(query);
            if (keyGenerated is null)
            {
                return getDataFromDatabase();
            }

            var entityType = typeof(TEntity);

            var cacheKey = new DbQueryCacheKey(entityType.FullName!, keyGenerated.Value.Expression, keyGenerated.Value.AdditionalJson, getDataFromDatabaseStr);
            var result = dbContext.DbQueryCacheStore.GetOrAddAsync(dbContext.Id, entityType, cacheKey, getDataFromDatabase);

            return result;
        }

        public Task<ReturnType?> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType?>> getDataFromDatabase,
            Expression[] query,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!)
        {
            var expression = "";
            var additionalJson = "";

            for (var i = 0; i < query.Length; i++)
            {
                var queryItem = query[i];

                var keyGenerated = _keyGeneratorVisitor.SafeExpressionToString(queryItem);
                if (keyGenerated is null)
                {
                    return getDataFromDatabase();
                }

                expression += keyGenerated.Value.Expression;
                if (keyGenerated.Value.AdditionalJson != null)
                {
                    additionalJson += keyGenerated.Value.AdditionalJson;
                }
            }

            var entityType = typeof(TEntity);

            var cacheKey = new DbQueryCacheKey(entityType.FullName!, expression, additionalJson, getDataFromDatabaseStr);
            var result = dbContext.DbQueryCacheStore.GetOrAddAsync(dbContext.Id, entityType, cacheKey, getDataFromDatabase);

            return result;
        }

        public Task<ReturnType?> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType?>> getDataFromDatabase,
            string key,
            [CallerArgumentExpression(nameof(getDataFromDatabase))] string getDataFromDatabaseStr = null!)
        {
            var entityType = typeof(TEntity);

            var cacheKey = new DbQueryCacheKey(entityType.FullName!, key, null, getDataFromDatabaseStr);
            var result = dbContext.DbQueryCacheStore.GetOrAddAsync(dbContext.Id, entityType, cacheKey, getDataFromDatabase);

            return result;
        }
    }
}