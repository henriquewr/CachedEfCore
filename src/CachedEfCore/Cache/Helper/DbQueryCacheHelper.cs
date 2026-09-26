using CachedEfCore.Cache.KeyGeneration;
using CachedEfCore.Cache.KeyGeneration.ExpressionKeyGen;
using CachedEfCore.Cache.Store;
using CachedEfCore.DependencyManager;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.ObjectPool;
using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace CachedEfCore.Cache.Helper
{
    public partial class DbQueryCacheHelper : IDbQueryCacheHelper
    {
        private static readonly AsyncLocal<ValuePrinter> _printerAsyncLocal = new();
        private static readonly ObjectPool<StringBuilder> _stringBuilderPool = new DefaultObjectPoolProvider().CreateStringBuilderPool();

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

        [OverloadResolutionPriority(-1)]
        public TReturnType GetOrAdd<TReturnType, TEntity>(
            DbContext dbContext,
            Func<TReturnType> getDataFromDatabase,
            ReadOnlySpan<object?> query)
        {
            return GetOrAdd<TReturnType>(typeof(TEntity), dbContext, getDataFromDatabase, query);
        }
        [OverloadResolutionPriority(-1)]
        public TReturnType GetOrAdd<TReturnType>(
            Type rootEntity,
            DbContext dbContext,
            Func<TReturnType> getDataFromDatabase,
            ReadOnlySpan<object?> query)
        {
            var expressionKeyBuilder = new DbQueryCacheKey.ExpressionKey.Builder();

            StringBuilder? stringBuilder = null;

            ResetAsyncLocalPrinter();

            var keyGeneratorVisitor = dbContext.GetService<KeyGeneratorVisitor>();
            var printabilityChecker = dbContext.GetService<IPrintabilityChecker>();
            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

            for (var i = 0; i < query.Length; i++)
            {
                var queryItem = query[i];

                if (queryItem is Expression expr)
                {
                    var keyGenerated = keyGeneratorVisitor.SafeExpressionToString(expr);
                    if (keyGenerated is null)
                    {
                        return getDataFromDatabase();
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
            if (!string.IsNullOrEmpty(printerResult))
            {
                expressionKeyBuilder.AddExpression(printerResult);
            }

            var expressionKey = expressionKeyBuilder.GetKey();

            string? additionalKey = null;
            if (stringBuilder is not null)
            {
                additionalKey = stringBuilder.ToString();
                _stringBuilderPool.Return(stringBuilder);
            }

            var cacheKey = new DbQueryCacheKey(rootEntity, expressionKey, additionalKey, getDataFromDatabase.Method, DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = dbQueryCacheStore.GetOrAdd(rootEntity, cacheKey, getDataFromDatabase);

            return result;
        }

        public TReturnType GetOrAdd<TReturnType, TEntity>(
            DbContext dbContext,
            Func<TReturnType> getDataFromDatabase,
            Expression query)
        {
            return GetOrAdd<TReturnType>(typeof(TEntity), dbContext, getDataFromDatabase, query);
        }
        public TReturnType GetOrAdd<TReturnType>(
            Type rootEntity,
            DbContext dbContext,
            Func<TReturnType> getDataFromDatabase,
            Expression query)
        {
            var keyGeneratorVisitor = dbContext.GetService<KeyGeneratorVisitor>();
            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

            var keyGenerated = keyGeneratorVisitor.SafeExpressionToString(query);
            if (keyGenerated is null)
            {
                return getDataFromDatabase();
            }

            var expressionKey = new DbQueryCacheKey.ExpressionKey(keyGenerated.Value.Expression);

            var cacheKey = new DbQueryCacheKey(rootEntity, expressionKey, keyGenerated.Value.AdditionalJson, getDataFromDatabase.Method, DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = dbQueryCacheStore.GetOrAdd(rootEntity, cacheKey, getDataFromDatabase);

            return result;
        }

        public TReturnType GetOrAdd<TReturnType, TEntity>(
            DbContext dbContext,
            Func<TReturnType> getDataFromDatabase,
            ReadOnlySpan<Expression> query)
        {
            return GetOrAdd<TReturnType>(typeof(TEntity), dbContext, getDataFromDatabase, query);
        }
        public TReturnType GetOrAdd<TReturnType>(
            Type rootEntity,
            DbContext dbContext,
            Func<TReturnType> getDataFromDatabase,
            ReadOnlySpan<Expression> query)
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
                    return getDataFromDatabase();
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
            var result = dbQueryCacheStore.GetOrAdd(rootEntity, cacheKey, getDataFromDatabase);

            return result;
        }

        public TReturnType GetOrAdd<TReturnType, TEntity>(
            DbContext dbContext,
            Func<TReturnType> getDataFromDatabase,
            string key)
        {
            return GetOrAdd<TReturnType>(typeof(TEntity), dbContext, getDataFromDatabase, key);
        }
        public TReturnType GetOrAdd<TReturnType>(
            Type rootEntity,
            DbContext dbContext,
            Func<TReturnType> getDataFromDatabase,
            string key)
        {
            var expressionKey = new DbQueryCacheKey.ExpressionKey(key);
            var cacheKey = new DbQueryCacheKey(rootEntity, expressionKey, null, getDataFromDatabase.Method, DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            
            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();
            var result = dbQueryCacheStore.GetOrAdd(rootEntity, cacheKey, getDataFromDatabase);

            return result;
        }

        private static DbContextId? DependentDbContext(DbContext dbContext, Type returnType)
        {
            var dependencyManager = dbContext.GetService<EntityDependency>();

            var isDependent = dependencyManager.HasLazyLoad(returnType);

            return isDependent ? dbContext.ContextId : null;
        }
    }
}