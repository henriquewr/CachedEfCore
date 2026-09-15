using CachedEfCore.Cache.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CachedEfCore.Cache.Query
{
    public static class CachedQuery
    {
        private static long _nextQueryId;

        public static CachedQueryBuilder<TEntity> For<TEntity>()
        {
            return CachedQueryBuilderCache<TEntity>.Instance;
        }

        internal static long GetNextQueryId()
        {
            return Interlocked.Increment(ref _nextQueryId);
        }

        private static class CachedQueryBuilderCache<TEntity>
        {
            internal static CachedQueryBuilder<TEntity> Instance { get; } = new();
        }
    }

    public sealed class CachedQueryBuilder<TEntity>
    {
        internal CachedQueryBuilder()
        {
        }

        public CachedQuery<TContext, TParameter, TResult> Compile<TContext, TParameter, TResult>(
            Expression<Func<TContext, TParameter, TResult>> queryExpression)
            where TContext : DbContext
        {
            ArgumentNullException.ThrowIfNull(queryExpression);

            return new CachedQuery<TContext, TParameter, TResult>(
                typeof(TEntity),
                EF.CompileQuery(queryExpression)
            );
        }

        public CachedAsyncQuery<TContext, TParameter, TResult> CompileAsync<TContext, TParameter, TResult>(
            Expression<Func<TContext, TParameter, CancellationToken, TResult>> queryExpression)
            where TContext : DbContext
        {
            ArgumentNullException.ThrowIfNull(queryExpression);

            return new CachedAsyncQuery<TContext, TParameter, TResult>(
                typeof(TEntity),
                EF.CompileAsyncQuery(queryExpression)
            );
        }
    }

    public sealed class CachedQuery<TContext, TParameter, TResult>
        where TContext : DbContext
    {
        private static readonly Func<QueryState, TResult> _executeQuery = ExecuteQuery;

        private readonly long _queryId = CachedQuery.GetNextQueryId();
        private readonly Type _rootEntityType;
        private readonly Func<TContext, TParameter, TResult> _query;
        private readonly CachedQueryModelState<TResult> _modelState = new();

        internal CachedQuery(Type rootEntityType, Func<TContext, TParameter, TResult> query)
        {
            _rootEntityType = rootEntityType;
            _query = query;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult GetOrAdd(TContext dbContext, TParameter parameter)
        {
            return GetOrAddCore(dbContext, parameter, default(SharedCachePartition));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult GetOrAdd<TCachePartition>(TContext dbContext, TParameter parameter, TCachePartition cachePartition)
        {
            return GetOrAddCore(dbContext, parameter, cachePartition);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TResult GetOrAddCore<TCachePartition>(TContext dbContext, TParameter parameter, TCachePartition cachePartition)
        {
            ArgumentNullException.ThrowIfNull(dbContext);

            var model = _modelState.GetModel(dbContext, out var dependentDbContextId);
            var cacheKey = new CompiledQueryCacheKey<TParameter, TCachePartition>(_queryId, model, parameter, cachePartition, dependentDbContextId);
            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

            if (dbQueryCacheStore is IStatefulDbQueryCacheStore statefulCacheStore)
            {
                var state = new QueryState(_query, dbContext, parameter);

                return statefulCacheStore.GetOrAdd(_rootEntityType, cacheKey, state, _executeQuery);
            }

            return dbQueryCacheStore.GetOrAdd(_rootEntityType, cacheKey, () => _query(dbContext, parameter));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TResult ExecuteQuery(QueryState state)
        {
            return state.Query(state.DbContext, state.Parameter);
        }

        private readonly struct QueryState
        {
            public QueryState(Func<TContext, TParameter, TResult> query, TContext dbContext, TParameter parameter)
            {
                Query = query;
                DbContext = dbContext;
                Parameter = parameter;
            }

            public Func<TContext, TParameter, TResult> Query { get; }
            public TContext DbContext { get; }
            public TParameter Parameter { get; }
        }
    }

    public sealed class CachedAsyncQuery<TContext, TParameter, TResult>
        where TContext : DbContext
    {
        private static readonly Func<QueryState, Task<TResult>> _executeQuery = ExecuteQuery;

        private readonly long _queryId = CachedQuery.GetNextQueryId();
        private readonly Type _rootEntityType;
        private readonly Func<TContext, TParameter, CancellationToken, Task<TResult>> _query;
        private readonly CachedQueryModelState<TResult> _modelState = new();

        internal CachedAsyncQuery(Type rootEntityType, Func<TContext, TParameter, CancellationToken, Task<TResult>> query)
        {
            _rootEntityType = rootEntityType;
            _query = query;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<TResult> GetOrAddAsync(TContext dbContext, TParameter parameter, CancellationToken cancellationToken = default)
        {
            return GetOrAddAsyncCore(dbContext, parameter, default(SharedCachePartition), cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<TResult> GetOrAddAsync<TCachePartition>(
            TContext dbContext,
            TParameter parameter,
            TCachePartition cachePartition,
            CancellationToken cancellationToken = default)
        {
            return GetOrAddAsyncCore(dbContext, parameter, cachePartition, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueTask<TResult> GetOrAddAsyncCore<TCachePartition>(
            TContext dbContext,
            TParameter parameter,
            TCachePartition cachePartition,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            cancellationToken.ThrowIfCancellationRequested();

            var model = _modelState.GetModel(dbContext, out var dependentDbContextId);
            var cacheKey = new CompiledQueryCacheKey<TParameter, TCachePartition>(_queryId, model, parameter, cachePartition, dependentDbContextId);
            var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

            if (dbQueryCacheStore is IStatefulDbQueryCacheStore statefulCacheStore)
            {
                var state = new QueryState(_query, dbContext, parameter, cancellationToken);

                return statefulCacheStore.GetOrAddAsync(_rootEntityType, cacheKey, state, _executeQuery);
            }

            return dbQueryCacheStore.GetOrAddAsync(
                _rootEntityType,
                cacheKey,
                () => _query(dbContext, parameter, cancellationToken)
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Task<TResult> ExecuteQuery(QueryState state)
        {
            return state.Query(state.DbContext, state.Parameter, state.CancellationToken);
        }

        private readonly struct QueryState
        {
            public QueryState(
                Func<TContext, TParameter, CancellationToken, Task<TResult>> query,
                TContext dbContext,
                TParameter parameter,
                CancellationToken cancellationToken)
            {
                Query = query;
                DbContext = dbContext;
                Parameter = parameter;
                CancellationToken = cancellationToken;
            }

            public Func<TContext, TParameter, CancellationToken, Task<TResult>> Query { get; }
            public TContext DbContext { get; }
            public TParameter Parameter { get; }
            public CancellationToken CancellationToken { get; }
        }
    }
}
