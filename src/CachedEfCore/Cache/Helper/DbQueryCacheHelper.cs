using CachedEfCore.Context;
using CachedEfCore.KeyGeneration;
using CachedEfCore.KeyGeneration.ExpressionKeyGen;
using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CachedEfCore.Cache.Helper
{
    public class DbQueryCacheHelper : IDbQueryCacheHelper
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
            ReadOnlySpan<object> query)
        {
            var expression = "";
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

                    expression += keyGenerated.Value.Expression;
                    if (keyGenerated.Value.AdditionalJson != null)
                    {
                        additionalJson += keyGenerated.Value.AdditionalJson;
                    }
                }
                else if (_printabilityChecker.IsPrintable(queryItem))
                {
                    expression += queryItem.ToString();
                }
                else
                {
                    _printerAsyncLocal.Value!.Print(queryItem);
                }
            }

            var printerResult = _printerAsyncLocal.Value!.GetResult();
            if (!string.IsNullOrEmpty(printerResult))
            {
                expression += printerResult;
            }

            var cacheKey = new DbQueryCacheKey(typeof(TEntity), expression, additionalJson, getDataFromDatabase.Method.MethodHandle.GetFunctionPointer(), DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = dbContext.DbQueryCacheStore.GetOrAdd(dbContext, typeof(TEntity), cacheKey, getDataFromDatabase);

            return result;
        }

        public ReturnType GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            Expression query)
        {
            var keyGenerated = _keyGeneratorVisitor.SafeExpressionToString(query);
            if (keyGenerated is null)
            {
                return getDataFromDatabase();
            }

            var cacheKey = new DbQueryCacheKey(typeof(TEntity), keyGenerated.Value.Expression, keyGenerated.Value.AdditionalJson, getDataFromDatabase.Method.MethodHandle.GetFunctionPointer(), DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = dbContext.DbQueryCacheStore.GetOrAdd(dbContext, typeof(TEntity), cacheKey, getDataFromDatabase);

            return result;
        }

        public ReturnType GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            ReadOnlySpan<Expression> query)
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

            var cacheKey = new DbQueryCacheKey(typeof(TEntity), expression, additionalJson, getDataFromDatabase.Method.MethodHandle.GetFunctionPointer(), DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = dbContext.DbQueryCacheStore.GetOrAdd(dbContext, typeof(TEntity), cacheKey, getDataFromDatabase);

            return result;
        }

        public ReturnType GetOrAdd<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<ReturnType> getDataFromDatabase,
            string key)
        {
            var cacheKey = new DbQueryCacheKey(typeof(TEntity), key, null, getDataFromDatabase.Method.MethodHandle.GetFunctionPointer(), DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = dbContext.DbQueryCacheStore.GetOrAdd(dbContext, typeof(TEntity), cacheKey, getDataFromDatabase);

            return result;
        }


        [OverloadResolutionPriority(-1)]
        public async ValueTask<ReturnType> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType>> getDataFromDatabase,
            object[] query)
        {
            var expression = "";
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
                        return await getDataFromDatabase();
                    }

                    expression += keyGenerated.Value.Expression;
                    if (keyGenerated.Value.AdditionalJson != null)
                    {
                        additionalJson += keyGenerated.Value.AdditionalJson;
                    }
                }
                else if (_printabilityChecker.IsPrintable(queryItem))
                {
                    expression += queryItem.ToString();
                }
                else
                {
                    _printerAsyncLocal.Value!.Print(queryItem);
                }
            }

            var printerResult = _printerAsyncLocal.Value!.GetResult();
            if (!string.IsNullOrEmpty(printerResult))
            {
                expression += printerResult;
            }

            var cacheKey = new DbQueryCacheKey(typeof(TEntity), expression, additionalJson, getDataFromDatabase.Method.MethodHandle.GetFunctionPointer(), DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = await dbContext.DbQueryCacheStore.GetOrAddAsync(dbContext, typeof(TEntity), cacheKey, getDataFromDatabase);

            return result;
        }

        public async ValueTask<ReturnType> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType>> getDataFromDatabase,
            Expression query)
        {
            var keyGenerated = _keyGeneratorVisitor.SafeExpressionToString(query);
            if (keyGenerated is null)
            {
                return await getDataFromDatabase();
            }

            var cacheKey = new DbQueryCacheKey(typeof(TEntity), keyGenerated.Value.Expression, keyGenerated.Value.AdditionalJson, getDataFromDatabase.Method.MethodHandle.GetFunctionPointer(), DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = await dbContext.DbQueryCacheStore.GetOrAddAsync(dbContext, typeof(TEntity), cacheKey, getDataFromDatabase);

            return result;
        }

        public async ValueTask<ReturnType> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType>> getDataFromDatabase,
            Expression[] query)
        {
            var expression = "";
            var additionalJson = "";

            for (var i = 0; i < query.Length; i++)
            {
                var queryItem = query[i];

                var keyGenerated = _keyGeneratorVisitor.SafeExpressionToString(queryItem);
                if (keyGenerated is null)
                {
                    return await getDataFromDatabase();
                }

                expression += keyGenerated.Value.Expression;
                if (keyGenerated.Value.AdditionalJson != null)
                {
                    additionalJson += keyGenerated.Value.AdditionalJson;
                }
            }

            var cacheKey = new DbQueryCacheKey(typeof(TEntity), expression, additionalJson, getDataFromDatabase.Method.MethodHandle.GetFunctionPointer(), DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = await dbContext.DbQueryCacheStore.GetOrAddAsync(dbContext, typeof(TEntity), cacheKey, getDataFromDatabase);

            return result;
        }

        public ValueTask<ReturnType> GetOrAddAsync<ReturnType, TEntity>(
            ICachedDbContext dbContext,
            Func<Task<ReturnType>> getDataFromDatabase,
            string key)
        {
            var cacheKey = new DbQueryCacheKey(typeof(TEntity), key, null, getDataFromDatabase.Method.MethodHandle.GetFunctionPointer(), DependentDbContext(dbContext, getDataFromDatabase.Method.ReturnType));
            var result = dbContext.DbQueryCacheStore.GetOrAddAsync(dbContext, typeof(TEntity), cacheKey, getDataFromDatabase);

            return result;
        }

        private static Guid? DependentDbContext(ICachedDbContext dbContext, Type returnType)
        {
            var isDependent = dbContext.DependencyManager.HasLazyLoad(returnType);

            return isDependent ? dbContext.Id : null;
        }
    }
}