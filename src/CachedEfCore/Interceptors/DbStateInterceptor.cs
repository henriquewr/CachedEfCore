using CachedEfCore.Cache.Store;
using CachedEfCore.DependencyManager;
using CachedEfCore.EntityMapping;
using CachedEfCore.SqlAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CachedEfCore.Interceptors
{
    public partial class DbStateInterceptor : ISingletonInterceptor
    {
        private readonly ISqlQueryEntityExtractor _sqlQueryEntityExtractor;
        private readonly ConditionalWeakTable<DbContext, State> _stateTable;

        public DbStateInterceptor(ISqlQueryEntityExtractor sqlQueryEntityExtractor)
        {
            _sqlQueryEntityExtractor = sqlQueryEntityExtractor;
            _stateTable = new ConditionalWeakTable<DbContext, State>();
        }

        private void ReplaceState(DbContext dbContext, State state)
        {
            DisposeState(dbContext);

            _stateTable.AddOrUpdate(dbContext, state);
        }
        private async ValueTask ReplaceStateAsync(DbContext dbContext, State state)
        {
            await DisposeStateAsync(dbContext).ConfigureAwait(false);

            _stateTable.AddOrUpdate(dbContext, state);
        }

        private void DisposeState(DbContext? dbContext)
        {
            if (dbContext is not null && _stateTable.Remove(dbContext, out var state))
            {
                state.Dispose();
            }
        }
        private async ValueTask DisposeStateAsync(DbContext? dbContext)
        {
            if (dbContext is not null && _stateTable.Remove(dbContext, out var state))
            {
                await state.DisposeAsync().ConfigureAwait(false);
            }
        }

        private sealed class State : IDisposable, IAsyncDisposable
        {
            public State(TransactionState? transaction, QueryState? query)
            {
                Transaction = transaction;
                Query = query;
            }

            public TransactionState? Transaction { get; private set; }
            public void ReplaceTransaction(TransactionState? transaction)
            {
                Transaction?.Dispose();
                Transaction = transaction;
            }
            public async ValueTask ReplaceTransactionAsync(TransactionState? transaction)
            {
                if (Transaction is not null)
                {
                    await Transaction.DisposeAsync().ConfigureAwait(false);
                }
                Transaction = transaction;
            }

            public QueryState? Query { get; private set; }
            public void ReplaceQuery(QueryState? query)
            {
                Query?.Dispose();
                Query = query;
            }
            public async ValueTask ReplaceQueryAsync(QueryState? query)
            {
                if (Query is not null)
                {
                    await Query.DisposeAsync().ConfigureAwait(false);
                }
                Query = query;
            }

            public void Dispose()
            {
                GC.SuppressFinalize(this);
                if (Query is not null)
                {
                    Query.Dispose();
                    Query = null;
                }
                if (Transaction is not null)
                {
                    Transaction.Dispose();
                    Transaction = null;
                }
            }

            public async ValueTask DisposeAsync()
            {
                GC.SuppressFinalize(this);
                if (Query is not null)
                {
                    await Query.DisposeAsync();
                    Query = null;
                }
                if (Transaction is not null)
                {
                    await Transaction.DisposeAsync();
                    Transaction = null;
                }
            }
            ~State()
            {
                if (Query is not null)
                {
                    Query.Dispose();
                    Query = null;
                }
                if (Transaction is not null)
                {
                    Transaction.Dispose();
                    Transaction = null;
                }
            }
        }
    }

    // IDbTransactionInterceptor
    public partial class DbStateInterceptor : IDbTransactionInterceptor
    {
        public sealed class TransactionState : IDisposable, IAsyncDisposable
        {
            public required Guid TransactionId { get; init; }
            /// <summary>
            /// This isn't root entities, but every upper related entity from the root entities that are being changed in the transaction.
            /// </summary>
            public required HashSet<IEntityType> StateChangingEntities { get; init; }
            public IPendingEntities? PendingEntities { get; private set; }

            public void ReplacePendingEntities(IPendingEntities? pendingEntities)
            {
                if (PendingEntities is not null)
                {
                    PendingEntities.Dispose();
                }

                PendingEntities = pendingEntities;
            }
            public async ValueTask ReplacePendingEntitiesAsync(IPendingEntities? pendingEntities)
            {
                if (PendingEntities is not null)
                {
                    await PendingEntities.DisposeAsync().ConfigureAwait(false);
                }

                PendingEntities = pendingEntities;
            }

            public required bool CanHaveDirtyReads { get; init; }

            public void Dispose()
                => ReplacePendingEntities(null);
            public ValueTask DisposeAsync()
                => ReplacePendingEntitiesAsync(null);
        }

        public bool TryGetTransactionState([NotNullWhen(true)] DbContext? dbContext, [NotNullWhen(true)] out TransactionState? transactionState)
        {
            if (dbContext is not null && _stateTable.TryGetValue(dbContext, out var state)
                && state.Transaction is not null)
            {
                if (dbContext.Database.CurrentTransaction is null)
                {
                    // Transaction was disposed
                    // When disposing transactions, there's no way to properly remove the state
                    var disposeTask = state.ReplaceTransactionAsync(null);

                    if (!disposeTask.IsCompleted)
                    {
                        _ = disposeTask.AsTask().ConfigureAwait(false);
                    }

                    transactionState = null;
                    return false;
                }

                transactionState = state.Transaction;
                return true;
            }

            transactionState = null;
            return false;
        }

        private void TryRemoveCommittedTransactionData(DbContext? dbContext)
        {
            if (dbContext is not null && _stateTable.Remove(dbContext, out var state))
            {
                if (state.Transaction is not null)
                {
                    var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

                    dbQueryCacheStore.RemoveAllByTransactionId(state.Transaction.TransactionId);

                    if (state.Transaction.StateChangingEntities.Count != 0)
                    {
                        dbQueryCacheStore.RemoveDependentEntities(state.Transaction.StateChangingEntities);
                    }
                }

                state.Dispose();
            }
        }
        private async ValueTask TryRemoveCommittedTransactionDataAsync(DbContext? dbContext)
        {
            if (dbContext is not null && _stateTable.Remove(dbContext, out var state))
            {
                if (state.Transaction is not null)
                {
                    var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

                    await dbQueryCacheStore.RemoveAllByTransactionIdAsync(state.Transaction.TransactionId).ConfigureAwait(false);

                    if (state.Transaction.StateChangingEntities.Count != 0)
                    {
                        await dbQueryCacheStore.RemoveDependentEntitiesAsync(state.Transaction.StateChangingEntities).ConfigureAwait(false);
                    }
                }

                await state.DisposeAsync().ConfigureAwait(false);
            }
        }

        private void TryRemoveUncommittedTransactionData(DbContext? dbContext)
        {
            if (dbContext is not null && _stateTable.Remove(dbContext, out var state))
            {
                if (state.Transaction is not null)
                {
                    var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

                    dbQueryCacheStore.RemoveAllByTransactionId(state.Transaction.TransactionId);
                }

                state.Dispose();
            }
        }
        private async ValueTask TryRemoveUncommittedTransactionDataAsync(DbContext? dbContext)
        {
            if (dbContext is not null && _stateTable.Remove(dbContext, out var state))
            {
                if (state.Transaction is not null)
                {
                    var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

                    await dbQueryCacheStore.RemoveAllByTransactionIdAsync(state.Transaction.TransactionId).ConfigureAwait(false);
                }

                await state.DisposeAsync().ConfigureAwait(false);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TrySetTransactionState(DbContext? dbContext, DbTransaction? transaction, Guid transactionId)
        {
            if (dbContext is not null)
            {
                var state = new State(new TransactionState
                {
                    StateChangingEntities = new HashSet<IEntityType>(32),
                    TransactionId = transactionId,
                    CanHaveDirtyReads = transaction?.IsolationLevel == IsolationLevel.ReadUncommitted,
                }, null);

                ReplaceState(dbContext, state);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async ValueTask TrySetTransactionStateAsync(DbContext? dbContext, DbTransaction? transaction, Guid transactionId)
        {
            if (dbContext is not null)
            {
                var state = new State(new TransactionState
                {
                    StateChangingEntities = new HashSet<IEntityType>(32),
                    TransactionId = transactionId,
                    CanHaveDirtyReads = transaction?.IsolationLevel == IsolationLevel.ReadUncommitted,
                }, null);

                await ReplaceStateAsync(dbContext, state).ConfigureAwait(false);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TryAddTransactionPendingEntities(DbContext? dbContext)
        {
            if (dbContext is not null && _stateTable.TryGetValue(dbContext, out var state) && state.Transaction is not null)
            {
                var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();
                var pendingEntities = dbQueryCacheStore.AddPendingEntities(state.Transaction.StateChangingEntities);

                state.Transaction.ReplacePendingEntities(pendingEntities);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async ValueTask TryAddTransactionPendingEntitiesAsync(DbContext? dbContext)
        {
            if (dbContext is not null && _stateTable.TryGetValue(dbContext, out var state) && state.Transaction is not null)
            {
                var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();
                var pendingEntities = await dbQueryCacheStore.AddPendingEntitiesAsync(state.Transaction.StateChangingEntities);

                await state.Transaction.ReplacePendingEntitiesAsync(pendingEntities).ConfigureAwait(false);
            }
        }

        DbTransaction IDbTransactionInterceptor.TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
        {
            TrySetTransactionState(eventData.Context, eventData.Transaction, eventData.TransactionId);

            return result;
        }
        async ValueTask<DbTransaction> IDbTransactionInterceptor.TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken)
        {
            await TrySetTransactionStateAsync(eventData.Context, eventData.Transaction, eventData.TransactionId).ConfigureAwait(false);

            return result;
        }

        DbTransaction IDbTransactionInterceptor.TransactionUsed(DbConnection connection, TransactionEventData eventData, DbTransaction result)
        {
            TrySetTransactionState(eventData.Context, eventData.Transaction, eventData.TransactionId);

            return result;
        }
        async ValueTask<DbTransaction> IDbTransactionInterceptor.TransactionUsedAsync(DbConnection connection, TransactionEventData eventData, DbTransaction result, CancellationToken cancellationToken)
        {
            await TrySetTransactionStateAsync(eventData.Context, eventData.Transaction, eventData.TransactionId).ConfigureAwait(false);

            return result;
        }


        InterceptionResult IDbTransactionInterceptor.TransactionCommitting(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result)
        {
            TryAddTransactionPendingEntities(eventData.Context);
            return result;
        }
        void IDbTransactionInterceptor.TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
        {
            TryRemoveCommittedTransactionData(eventData.Context);
        }

        async ValueTask<InterceptionResult> IDbTransactionInterceptor.TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken)
        {
            await TryAddTransactionPendingEntitiesAsync(eventData.Context).ConfigureAwait(false);
            return result;
        }
        async Task IDbTransactionInterceptor.TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken)
        {
            await TryRemoveCommittedTransactionDataAsync(eventData.Context).ConfigureAwait(false);
        }


        InterceptionResult IDbTransactionInterceptor.TransactionRollingBack(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result)
        {
            TryAddTransactionPendingEntities(eventData.Context);
            return result;
        }
        void IDbTransactionInterceptor.TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
        {
            TryRemoveUncommittedTransactionData(eventData.Context);
        }

        async ValueTask<InterceptionResult> IDbTransactionInterceptor.TransactionRollingBackAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken)
        {
            await TryAddTransactionPendingEntitiesAsync(eventData.Context).ConfigureAwait(false);
            return result;
        }
        async Task IDbTransactionInterceptor.TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken)
        {
            await TryRemoveUncommittedTransactionDataAsync(eventData.Context).ConfigureAwait(false);
        }


        InterceptionResult IDbTransactionInterceptor.RollingBackToSavepoint(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result)
        {
            TryAddTransactionPendingEntities(eventData.Context);
            return result;
        }
        void IDbTransactionInterceptor.RolledBackToSavepoint(DbTransaction transaction, TransactionEventData eventData)
        {
            TryRemoveUncommittedTransactionData(eventData.Context);
        }

        async ValueTask<InterceptionResult> IDbTransactionInterceptor.RollingBackToSavepointAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken)
        {
            await TryAddTransactionPendingEntitiesAsync(eventData.Context).ConfigureAwait(false);
            return result;
        }
        async Task IDbTransactionInterceptor.RolledBackToSavepointAsync(DbTransaction transaction, TransactionEventData eventData, CancellationToken cancellationToken)
        {
            await TryRemoveUncommittedTransactionDataAsync(eventData.Context).ConfigureAwait(false);
        }


        void IDbTransactionInterceptor.TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
        {
            TryRemoveUncommittedTransactionData(eventData.Context);
        }
        async Task IDbTransactionInterceptor.TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken)
        {
            await TryRemoveUncommittedTransactionDataAsync(eventData.Context).ConfigureAwait(false);
        }
    }

    // IDbCommandInterceptor
    public partial class DbStateInterceptor : IDbCommandInterceptor
    {
        private sealed class QueryState : IDisposable, IAsyncDisposable
        {
            /// <summary>
            /// This isn't root entities, but every upper related entity from the root entities that are being changed in the transaction.
            /// </summary>
            public required HashSet<IEntityType> StateChangingEntities { get; init; }
            public required IPendingEntities PendingEntities { get; init; }

            public void Dispose()
            {
                PendingEntities.Dispose();
            }

            public ValueTask DisposeAsync()
            {
                return PendingEntities.DisposeAsync();
            }
        }


        InterceptionResult<int> IDbCommandInterceptor.NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            CommandExecuting(command.CommandText, eventData.Context, eventData.CommandSource);

            return result;
        }
        int IDbCommandInterceptor.NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
        {
            CommandExecuted(eventData.Context);

            return result;
        }

        async ValueTask<InterceptionResult<int>> IDbCommandInterceptor.NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken)
        {
            await CommandExecutingAsync(command.CommandText, eventData.Context, eventData.CommandSource).ConfigureAwait(false);

            return result;
        }
        async ValueTask<int> IDbCommandInterceptor.NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken)
        {
            await CommandExecutedAsync(eventData.Context).ConfigureAwait(false);

            return result;
        }


        InterceptionResult<DbDataReader> IDbCommandInterceptor.ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            CommandExecuting(command.CommandText, eventData.Context, eventData.CommandSource);

            return result;
        }
        DbDataReader IDbCommandInterceptor.ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {
            CommandExecuted(eventData.Context);

            return result;
        }

        async ValueTask<InterceptionResult<DbDataReader>> IDbCommandInterceptor.ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken)
        {
            await CommandExecutingAsync(command.CommandText, eventData.Context, eventData.CommandSource).ConfigureAwait(false);

            return result;
        }
        async ValueTask<DbDataReader> IDbCommandInterceptor.ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken)
        {
            await CommandExecutedAsync(eventData.Context).ConfigureAwait(false);

            return result;
        }


        InterceptionResult<object> IDbCommandInterceptor.ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            CommandExecuting(command.CommandText, eventData.Context, eventData.CommandSource);

            return result;
        }
        object? IDbCommandInterceptor.ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
        {
            CommandExecuted(eventData.Context);

            return result;
        }

        async ValueTask<InterceptionResult<object>> IDbCommandInterceptor.ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken)
        {
            await CommandExecutingAsync(command.CommandText, eventData.Context, eventData.CommandSource).ConfigureAwait(false);

            return result;
        }
        async ValueTask<object?> IDbCommandInterceptor.ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken)
        {
            await CommandExecutedAsync(eventData.Context).ConfigureAwait(false);

            return result;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetStateChangingEntities(string command, [NotNullWhen(true)] DbContext? context, CommandSource commandSource, [NotNullWhen(true)] out HashSet<IEntityType>? stateChangingEntities)
        {
            if (context is null)
            {
                stateChangingEntities = null;
                return false;
            }

            switch (commandSource)
            {
                case CommandSource.Migrations:
                case CommandSource.Unknown:
                case CommandSource.LinqQuery:
                    {
                        stateChangingEntities = null;
                        return false;
                    }

                case CommandSource.SaveChanges:
                    {
                        var modifiedEntities = context.ChangeTracker.Entries().Where(static x => x.State != EntityState.Unchanged);
                        stateChangingEntities = modifiedEntities
                          .Select(static e => e.Metadata)
                          .ToHashSet();

                        return stateChangingEntities.Count != 0;
                    }

                case CommandSource.ExecuteUpdate:
                case CommandSource.ExecuteDelete:
                case CommandSource.ExecuteSqlRaw:
                case CommandSource.FromSqlQuery:
                default:
                    {
                        var tableEntityMapping = context.GetService<TableEntityMapping>();

                        stateChangingEntities = _sqlQueryEntityExtractor.GetStateChangingEntityTypesFromSql(tableEntityMapping, command).ToHashSet();

                        return stateChangingEntities.Count != 0;
                    }
            }
        }

        void IDbCommandInterceptor.CommandCanceled(DbCommand command, CommandEndEventData eventData)
        {
            CommandExecuted(eventData.Context);
        }
        async Task IDbCommandInterceptor.CommandCanceledAsync(DbCommand command, CommandEndEventData eventData, CancellationToken cancellationToken)
        {
            await CommandExecutedAsync(eventData.Context).ConfigureAwait(false);
        }

        void IDbCommandInterceptor.CommandFailed(DbCommand command, CommandErrorEventData eventData)
        {
            CommandExecuted(eventData.Context);
        }
        async Task IDbCommandInterceptor.CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken)
        {
            await CommandExecutedAsync(eventData.Context).ConfigureAwait(false);
        }

        InterceptionResult IDbCommandInterceptor.DataReaderClosing(DbCommand command, DataReaderClosingEventData eventData, InterceptionResult result)
        {
            CommandExecuted(eventData.Context);
            return result;
        }
        async ValueTask<InterceptionResult> IDbCommandInterceptor.DataReaderClosingAsync(DbCommand command, DataReaderClosingEventData eventData, InterceptionResult result)
        {
            await CommandExecutedAsync(eventData.Context).ConfigureAwait(false);
            return result;
        }

        InterceptionResult IDbCommandInterceptor.DataReaderDisposing(DbCommand command, DataReaderDisposingEventData eventData, InterceptionResult result)
        {
            CommandExecuted(eventData.Context);
            return result;
        }

        private void CommandExecuted(DbContext? context)
        {
            if (context is not null && _stateTable.TryGetValue(context, out var state))
            {
                Invalidate(context, state);
            }

            void Invalidate(
                DbContext dbContext,
                State state)
            {
                if (state.Query is null)
                {
                    return;
                }

                var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

                if (state.Transaction is not null)
                {
                    dbQueryCacheStore.RemoveAllByTransactionId(state.Transaction.TransactionId);

                    if (state.Transaction.CanHaveDirtyReads)
                    {
                        dbQueryCacheStore.RemoveDependentEntities(state.Query.StateChangingEntities);
                    }

                    state.Transaction.StateChangingEntities.UnionWith(state.Query.StateChangingEntities);
                }
                else
                {
                    dbQueryCacheStore.RemoveDependentEntities(state.Query.StateChangingEntities);
                }

                RemoveQueryState(dbContext, state);
            }
        }
        private async ValueTask CommandExecutedAsync(DbContext? context)
        {
            if (context is not null && _stateTable.TryGetValue(context, out var state))
            {
                await InvalidateAsync(context, state).ConfigureAwait(false);
            }

            async ValueTask InvalidateAsync(
                DbContext dbContext,
                State state)
            {
                if (state.Query is null)
                {
                    return;
                }

                var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

                if (state.Transaction is not null)
                {
                    await dbQueryCacheStore.RemoveAllByTransactionIdAsync(state.Transaction.TransactionId).ConfigureAwait(false);

                    if (state.Transaction.CanHaveDirtyReads)
                    {
                        await dbQueryCacheStore.RemoveDependentEntitiesAsync(state.Query.StateChangingEntities).ConfigureAwait(false);
                    }

                    state.Transaction.StateChangingEntities.UnionWith(state.Query.StateChangingEntities);
                }
                else
                {
                    await dbQueryCacheStore.RemoveDependentEntitiesAsync(state.Query.StateChangingEntities).ConfigureAwait(false);
                }

                await RemoveQueryStateAsync(dbContext, state).ConfigureAwait(false);
            }
        }


        private void CommandExecuting(string command, DbContext? context, CommandSource commandSource)
        {
            if (TryGetStateChangingEntities(command, context, commandSource, out var stateChangingEntities) && stateChangingEntities.Count > 0)
            {
                var entityDependency = context.GetService<EntityDependency>();

                var entities = new HashSet<IEntityType>(32);
                foreach (var item in stateChangingEntities)
                {
                    entities.UnionWith(entityDependency.GetUpperRelatedEntities(item));
                }

                ReplaceQueryState(context, entities);
            }
            else
            {
                TryRemoveQueryState(context);
            }
        }
        private async ValueTask CommandExecutingAsync(string command, DbContext? context, CommandSource commandSource)
        {
            if (TryGetStateChangingEntities(command, context, commandSource, out var stateChangingEntities) && stateChangingEntities.Count > 0)
            {
                var entityDependency = context.GetService<EntityDependency>();

                var entities = new HashSet<IEntityType>(32);
                foreach (var item in stateChangingEntities)
                {
                    entities.UnionWith(entityDependency.GetUpperRelatedEntities(item));
                }

                await ReplaceQueryStateAsync(context, entities).ConfigureAwait(false);
            }
            else
            {
                await TryRemoveQueryStateAsync(context).ConfigureAwait(false);
            }
        }

        private void ReplaceQueryState(DbContext? dbContext, HashSet<IEntityType> stateChangingEntities)
        {
            if (dbContext is not null)
            {
                var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();
                var pendingEntities = dbQueryCacheStore.AddPendingEntities(stateChangingEntities);

                var queryState = new QueryState
                {
                    PendingEntities = pendingEntities,
                    StateChangingEntities = stateChangingEntities
                };

                if (_stateTable.TryGetValue(dbContext, out var state))
                {
                    state.ReplaceQuery(queryState);
                }
                else
                {
                    ReplaceState(dbContext, new State(null, queryState));
                }
            }
        }
        private async ValueTask ReplaceQueryStateAsync(DbContext? dbContext, HashSet<IEntityType> stateChangingEntities)
        {
            if (dbContext is not null)
            {
                var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();
                var pendingEntities = dbQueryCacheStore.AddPendingEntities(stateChangingEntities);

                var queryState = new QueryState
                {
                    PendingEntities = pendingEntities,
                    StateChangingEntities = stateChangingEntities
                };

                if (_stateTable.TryGetValue(dbContext, out var state))
                {
                    await state.ReplaceQueryAsync(queryState).ConfigureAwait(false);
                }
                else
                {
                    await ReplaceStateAsync(dbContext, new State(null, queryState)).ConfigureAwait(false);
                }
            }
        }

        private void TryRemoveQueryState(DbContext? dbContext)
        {
            if (dbContext is not null && _stateTable.TryGetValue(dbContext, out var state))
            {
                RemoveQueryState(dbContext, state);
            }
        }
        private void RemoveQueryState(DbContext dbContext, State state)
        {
            if (state.Transaction is null)
            {
                DisposeState(dbContext);
            }
            else if (state.Query is not null)
            {
                state.ReplaceQuery(null);
            }
        }

        private async ValueTask TryRemoveQueryStateAsync(DbContext? dbContext)
        {
            if (dbContext is not null && _stateTable.TryGetValue(dbContext, out var state))
            {
                await RemoveQueryStateAsync(dbContext, state).ConfigureAwait(false);
            }
        }
        private async ValueTask RemoveQueryStateAsync(DbContext dbContext, State state)
        {
            if (state.Transaction is null)
            {
                await DisposeStateAsync(dbContext).ConfigureAwait(false);
            }
            else if (state.Query is not null)
            {
                await state.ReplaceQueryAsync(null).ConfigureAwait(false);
            }
        }
    }

    // IDbConnectionInterceptor
    public partial class DbStateInterceptor : IDbConnectionInterceptor
    {
        InterceptionResult IDbConnectionInterceptor.ConnectionClosing(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        {
            DisposeState(eventData.Context);

            return result;
        }
        async ValueTask<InterceptionResult> IDbConnectionInterceptor.ConnectionClosingAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        {
            await DisposeStateAsync(eventData.Context).ConfigureAwait(false);

            return result;
        }

        void IDbConnectionInterceptor.ConnectionClosed(DbConnection connection, ConnectionEndEventData eventData)
        {
            DisposeState(eventData.Context);
        }
        async Task IDbConnectionInterceptor.ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData)
        {
            await DisposeStateAsync(eventData.Context).ConfigureAwait(false);
        }

        InterceptionResult IDbConnectionInterceptor.ConnectionDisposing(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        {
            DisposeState(eventData.Context);
            return result;
        }
        async ValueTask<InterceptionResult> IDbConnectionInterceptor.ConnectionDisposingAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        {
            await DisposeStateAsync(eventData.Context).ConfigureAwait(false);
            return result;
        }

        void IDbConnectionInterceptor.ConnectionDisposed(DbConnection connection, ConnectionEndEventData eventData)
        {
            DisposeState(eventData.Context);
        }
        async Task IDbConnectionInterceptor.ConnectionDisposedAsync(DbConnection connection, ConnectionEndEventData eventData)
        {
            await DisposeStateAsync(eventData.Context).ConfigureAwait(false);
        }

        void IDbConnectionInterceptor.ConnectionFailed(DbConnection connection, ConnectionErrorEventData eventData)
        {
            DisposeState(eventData.Context);
        }

        async Task IDbConnectionInterceptor.ConnectionFailedAsync(DbConnection connection, ConnectionErrorEventData eventData, CancellationToken cancellationToken)
        {
            await DisposeStateAsync(eventData.Context).ConfigureAwait(false);
        }

        void IDbConnectionInterceptor.ConnectionCanceled(DbConnection connection, ConnectionEndEventData eventData)
        {
            DisposeState(eventData.Context);
        }

        async Task IDbConnectionInterceptor.ConnectionCanceledAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken)
        {
            await DisposeStateAsync(eventData.Context).ConfigureAwait(false);
        }
    }
}
