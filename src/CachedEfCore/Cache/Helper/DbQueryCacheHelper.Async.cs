using CachedEfCore.Cache.KeyGeneration;
using CachedEfCore.Cache.KeyGeneration.ExpressionKeyGen;
using CachedEfCore.Cache.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CachedEfCore.Cache.Helper
{
    public partial class DbQueryCacheHelper : IDbQueryCacheHelper
    {
        [OverloadResolutionPriority(-1)]
        public ValueTask<TReturnType> GetOrAddAsync<TReturnType, TEntity>(
            DbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            object?[] query)
        {
            return GetOrAddAsync<TReturnType>(typeof(TEntity), dbContext, getDataFromDatabase, query);
        }
        [OverloadResolutionPriority(-1)]
        public async ValueTask<TReturnType> GetOrAddAsync<TReturnType>(
            Type rootEntity,
            DbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            object?[] query)
        {
            var expressionKeyBuilder = new DbQueryCacheKey.ExpressionKey.Builder();

            var keyGeneratorVisitor = dbContext.GetService<KeyGeneratorVisitor>();
            var printabilityChecker = dbContext.GetService<IPrintabilityChecker>();
            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

            StringBuilder? stringBuilder = null;

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
                        stringBuilder ??= _stringBuilderPool.Get();

                        stringBuilder.Append(keyGenerated.Value.AdditionalJson);
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
            expressionKeyBuilder.AddExpression(printerResult);

            var expressionKey = expressionKeyBuilder.GetKey();

            string? additionalKey = null;
            if (stringBuilder is not null)
            {
                additionalKey = stringBuilder.ToString();
                _stringBuilderPool.Return(stringBuilder);
            }

            var cacheKey = new DbQueryCacheKey(rootEntity, expressionKey, additionalKey, getDataFromDatabase.Method, DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = await dbQueryCacheStore.GetOrAddAsync(rootEntity, cacheKey, getDataFromDatabase).ConfigureAwait(false);

            return result;
        }

        public ValueTask<TReturnType> GetOrAddAsync<TReturnType, TEntity>(
            DbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            Expression query)
        {
            return GetOrAddAsync<TReturnType>(typeof(TEntity), dbContext, getDataFromDatabase, query);
        }
        public async ValueTask<TReturnType> GetOrAddAsync<TReturnType>(
            Type rootEntity,
            DbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            Expression query)
        {
            var keyGeneratorVisitor = dbContext.GetService<KeyGeneratorVisitor>();
            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

            var keyGenerated = keyGeneratorVisitor.SafeExpressionToString(query);
            if (keyGenerated is null)
            {
                return await getDataFromDatabase().ConfigureAwait(false);
            }

            var expressionKey = new DbQueryCacheKey.ExpressionKey(keyGenerated.Value.Expression);

            var cacheKey = new DbQueryCacheKey(rootEntity, expressionKey, keyGenerated.Value.AdditionalJson, getDataFromDatabase.Method, DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = await dbQueryCacheStore.GetOrAddAsync(rootEntity, cacheKey, getDataFromDatabase).ConfigureAwait(false);

            return result;
        }

        public ValueTask<TReturnType> GetOrAddAsync<TReturnType, TEntity>(
            DbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            Expression[] query)
        {
            return GetOrAddAsync<TReturnType>(typeof(TEntity), dbContext, getDataFromDatabase, query);
        }
        public async ValueTask<TReturnType> GetOrAddAsync<TReturnType>(
            Type rootEntity,
            DbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            Expression[] query)
        {
            var keyGeneratorVisitor = dbContext.GetService<KeyGeneratorVisitor>();
            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

            var expressionKeyBuilder = new DbQueryCacheKey.ExpressionKey.Builder();

            StringBuilder? stringBuilder = null;

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
                    stringBuilder ??= _stringBuilderPool.Get();

                    stringBuilder.Append(keyGenerated.Value.AdditionalJson);
                }
            }

            var expressionKey = expressionKeyBuilder.GetKey();

            string? additionalKey = null;
            if (stringBuilder is not null)
            {
                additionalKey = stringBuilder.ToString();
                _stringBuilderPool.Return(stringBuilder);
            }

            var cacheKey = new DbQueryCacheKey(rootEntity, expressionKey, additionalKey, getDataFromDatabase.Method, DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = await dbQueryCacheStore.GetOrAddAsync(rootEntity, cacheKey, getDataFromDatabase).ConfigureAwait(false);

            return result;
        }

        public ValueTask<TReturnType> GetOrAddAsync<TReturnType, TEntity>(
            DbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            string key)
        {
            return GetOrAddAsync<TReturnType>(typeof(TEntity), dbContext, getDataFromDatabase, key);
        }
        public ValueTask<TReturnType> GetOrAddAsync<TReturnType>(
            Type rootEntity,
            DbContext dbContext,
            Func<Task<TReturnType>> getDataFromDatabase,
            string key)
        {
            var expressionKey = new DbQueryCacheKey.ExpressionKey(key);

            var cacheKey = new DbQueryCacheKey(rootEntity, expressionKey, null, getDataFromDatabase.Method, DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            
            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

            var result = dbQueryCacheStore.GetOrAddAsync(rootEntity, cacheKey, getDataFromDatabase);

            return result;
        }
    }
}
