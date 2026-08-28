using CachedEfCore.Context;
using CachedEfCore.KeyGeneration;
using CachedEfCore.KeyGeneration.ExpressionKeyGen;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CachedEfCore.Cache.Helper
{
    public partial class DbQueryCacheHelper : IDbQueryCacheHelper
    {
        [OverloadResolutionPriority(-1)]
        public ValueTask<TReturnType> GetOrAddAsync<TReturnType, TEntity>(
           ICachedDbContext dbContext,
           Func<Task<TReturnType>> getDataFromDatabase,
           object?[] query)
        {
            return GetOrAddAsync<TReturnType>(typeof(TEntity), dbContext, getDataFromDatabase, query);
        }
        [OverloadResolutionPriority(-1)]
        public async ValueTask<TReturnType> GetOrAddAsync<TReturnType>(
           Type rootEntity,
           ICachedDbContext dbContext,
           Func<Task<TReturnType>> getDataFromDatabase,
           object?[] query)
        {
            var expressionKeyBuilder = new DbQueryCacheKey.ExpressionKey.Builder();

            var keyGeneratorVisitor = dbContext.DbContext.GetService<KeyGeneratorVisitor>();
            var printabilityChecker = dbContext.DbContext.GetService<IPrintabilityChecker>();

            var additionalJson = "";

            ResetAsyncLocalPrinter();

            for (var i = 0; i < query.Length; i++)
            {
                var queryItem = query[i];

                if (queryItem is Expression expr)
                {
                    var keyGenerated = keyGeneratorVisitor.SafeExpressionToString(expr);
                    if (keyGenerated is null)
                    {
                        return await getDataFromDatabase().ConfigureAwait(false);
                    }

                    expressionKeyBuilder.AddExpression(keyGenerated.Value.Expression);
                    if (keyGenerated.Value.AdditionalJson != null)
                    {
                        additionalJson += keyGenerated.Value.AdditionalJson;
                    }
                }
                else if (printabilityChecker.IsPrintable(queryItem))
                {
                    expressionKeyBuilder.AddExpression(queryItem?.ToString());
                }
                else
                {
                    _printerAsyncLocal.Value!.Print(queryItem);
                }
            }

            var printerResult = _printerAsyncLocal.Value!.GetResult();
            if (!string.IsNullOrEmpty(printerResult))
            {
                expressionKeyBuilder.AddExpression(printerResult);
            }

            var expressionKey = expressionKeyBuilder.GetKey();

            var cacheKey = new DbQueryCacheKey(rootEntity, expressionKey, additionalJson, getDataFromDatabase.Method, DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = await dbContext.DbQueryCacheStore.GetOrAddAsync(dbContext, rootEntity, cacheKey, getDataFromDatabase).ConfigureAwait(false);

            return result;
        }

        public ValueTask<TReturnType> GetOrAddAsync<TReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            Expression query)
        {
            return GetOrAddAsync<TReturnType>(typeof(TEntity), dbContext, getDataFromDatabase, query);
        }
        public async ValueTask<TReturnType> GetOrAddAsync<TReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            Expression query)
        {
            var keyGeneratorVisitor = dbContext.DbContext.GetService<KeyGeneratorVisitor>();

            var keyGenerated = keyGeneratorVisitor.SafeExpressionToString(query);
            if (keyGenerated is null)
            {
                return await getDataFromDatabase().ConfigureAwait(false);
            }

            var expressionKey = new DbQueryCacheKey.ExpressionKey(keyGenerated.Value.Expression);

            var cacheKey = new DbQueryCacheKey(rootEntity, expressionKey, keyGenerated.Value.AdditionalJson, getDataFromDatabase.Method, DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = await dbContext.DbQueryCacheStore.GetOrAddAsync(dbContext, rootEntity, cacheKey, getDataFromDatabase).ConfigureAwait(false);

            return result;
        }

        public ValueTask<TReturnType> GetOrAddAsync<TReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            Expression[] query)
        {
            return GetOrAddAsync<TReturnType>(typeof(TEntity), dbContext, getDataFromDatabase, query);
        }
        public async ValueTask<TReturnType> GetOrAddAsync<TReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            Expression[] query)
        {
            var keyGeneratorVisitor = dbContext.DbContext.GetService<KeyGeneratorVisitor>();

            var expressionKeyBuilder = new DbQueryCacheKey.ExpressionKey.Builder();

            var additionalJson = "";

            for (var i = 0; i < query.Length; i++)
            {
                var queryItem = query[i];

                var keyGenerated = keyGeneratorVisitor.SafeExpressionToString(queryItem);
                if (keyGenerated is null)
                {
                    return await getDataFromDatabase().ConfigureAwait(false);
                }

                expressionKeyBuilder.AddExpression(keyGenerated.Value.Expression);
                if (keyGenerated.Value.AdditionalJson != null)
                {
                    additionalJson += keyGenerated.Value.AdditionalJson;
                }
            }

            var expressionKey = expressionKeyBuilder.GetKey();

            var cacheKey = new DbQueryCacheKey(rootEntity, expressionKey, additionalJson, getDataFromDatabase.Method, DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = await dbContext.DbQueryCacheStore.GetOrAddAsync(dbContext, rootEntity, cacheKey, getDataFromDatabase).ConfigureAwait(false);

            return result;
        }

        public ValueTask<TReturnType> GetOrAddAsync<TReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            string key)
        {
            return GetOrAddAsync<TReturnType>(typeof(TEntity), dbContext, getDataFromDatabase, key);
        }
        public ValueTask<TReturnType> GetOrAddAsync<TReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            string key)
        {
            var expressionKey = new DbQueryCacheKey.ExpressionKey(key);

            var cacheKey = new DbQueryCacheKey(rootEntity, expressionKey, null, getDataFromDatabase.Method, DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = dbContext.DbQueryCacheStore.GetOrAddAsync(dbContext, rootEntity, cacheKey, getDataFromDatabase);

            return result;
        }
    }
}
