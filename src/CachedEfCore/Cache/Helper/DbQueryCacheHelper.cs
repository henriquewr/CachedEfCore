using CachedEfCore.Context;
using CachedEfCore.KeyGeneration;
using CachedEfCore.KeyGeneration.ExpressionKeyGen;
using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CachedEfCore.Cache.Helper
{
    public partial class DbQueryCacheHelper : IDbQueryCacheHelper
    {
        private static readonly AsyncLocal<ValuePrinter> _printerAsyncLocal = new();

        private static void ResetAsyncLocalPrinter()
        {
            if (_printerAsyncLocal.Value is null || _printerAsyncLocal.Value.IsDisposed)
            {
                _printerAsyncLocal.Value = new();
            }
            else
            {
                _printerAsyncLocal.Value.ResetState();
            }
        }

        private readonly KeyGeneratorVisitor _keyGeneratorVisitor;
        private readonly IPrintabilityChecker _printabilityChecker;

        public DbQueryCacheHelper(KeyGeneratorVisitor keyGeneratorVisitor,
            IPrintabilityChecker printabilityChecker)
        {
            _keyGeneratorVisitor = keyGeneratorVisitor;
            _printabilityChecker = printabilityChecker;
        }

        [OverloadResolutionPriority(-1)]
        public ReturnType GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            ReadOnlySpan<object?> query)
        {
            return GetOrAdd<ReturnType>(typeof(TEntity), dbContext, getDataFromDatabase, query);
        }
        [OverloadResolutionPriority(-1)]
        public ReturnType GetOrAdd<ReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            ReadOnlySpan<object?> query)
        {
            var expressionKeyBuilder = new DbQueryCacheKey.ExpressionKey.Builder();

            var additionalJson = "";

            ResetAsyncLocalPrinter();

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

                    expressionKeyBuilder.AddExpression(keyGenerated.Value.Expression);
                    if (keyGenerated.Value.AdditionalJson != null)
                    {
                        additionalJson += keyGenerated.Value.AdditionalJson;
                    }
                }
                else if (_printabilityChecker.IsPrintable(queryItem))
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
            var result = dbContext.DbQueryCacheStore.GetOrAdd(dbContext, rootEntity, cacheKey, getDataFromDatabase);

            return result;
        }

        public ReturnType GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            Expression query)
        {
            return GetOrAdd<ReturnType>(typeof(TEntity), dbContext, getDataFromDatabase, query);
        }
        public ReturnType GetOrAdd<ReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            Expression query)
        {
            var keyGenerated = _keyGeneratorVisitor.SafeExpressionToString(query);
            if (keyGenerated is null)
            {
                return getDataFromDatabase();
            }

            var expressionKey = new DbQueryCacheKey.ExpressionKey(keyGenerated.Value.Expression);

            var cacheKey = new DbQueryCacheKey(rootEntity, expressionKey, keyGenerated.Value.AdditionalJson, getDataFromDatabase.Method, DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = dbContext.DbQueryCacheStore.GetOrAdd(dbContext, rootEntity, cacheKey, getDataFromDatabase);

            return result;
        }

        public ReturnType GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            ReadOnlySpan<Expression> query)
        {
            return GetOrAdd<ReturnType>(typeof(TEntity), dbContext, getDataFromDatabase, query);
        }
        public ReturnType GetOrAdd<ReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            ReadOnlySpan<Expression> query)
        {
            var expressionKeyBuilder = new DbQueryCacheKey.ExpressionKey.Builder();

            var additionalJson = "";

            for (var i = 0; i < query.Length; i++)
            {
                var queryItem = query[i];

                var keyGenerated = _keyGeneratorVisitor.SafeExpressionToString(queryItem);
                if (keyGenerated is null)
                {
                    return getDataFromDatabase();
                }

                expressionKeyBuilder.AddExpression(keyGenerated.Value.Expression);
                if (keyGenerated.Value.AdditionalJson != null)
                {
                    additionalJson += keyGenerated.Value.AdditionalJson;
                }
            }

            var expressionKey = expressionKeyBuilder.GetKey();

            var cacheKey = new DbQueryCacheKey(rootEntity, expressionKey, additionalJson, getDataFromDatabase.Method, DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = dbContext.DbQueryCacheStore.GetOrAdd(dbContext, rootEntity, cacheKey, getDataFromDatabase);

            return result;
        }

        public ReturnType GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            string key)
        {
            return GetOrAdd<ReturnType>(typeof(TEntity), dbContext, getDataFromDatabase, key);
        }
        public ReturnType GetOrAdd<ReturnType>(
            Type rootEntity,
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            string key)
        {
            var expressionKey = new DbQueryCacheKey.ExpressionKey(key);

            var cacheKey = new DbQueryCacheKey(rootEntity, expressionKey, null, getDataFromDatabase.Method, DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = dbContext.DbQueryCacheStore.GetOrAdd(dbContext, rootEntity, cacheKey, getDataFromDatabase);

            return result;
        }

        private static Guid? DependentDbContext(ICachedDbContext dbContext, Type returnType)
        {
            var isDependent = dbContext.DependencyManager.HasLazyLoad(returnType);

            return isDependent ? dbContext.Id : null;
        }
    }
}