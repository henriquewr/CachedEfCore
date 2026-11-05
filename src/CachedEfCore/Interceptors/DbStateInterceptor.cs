using CachedEfCore.Cache.Store;
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
    public class DbStateInterceptor : IDbCommandInterceptor, IDbTransactionInterceptor, ISingletonInterceptor
    {
        private readonly ISqlQueryEntityExtractor _sqlQueryEntityExtractor;
        public DbStateInterceptor(ISqlQueryEntityExtractor sqlQueryEntityExtractor)
        {
            _sqlQueryEntityExtractor = sqlQueryEntityExtractor;
        }

        #region Transactions

        private class TransactionState
        {
            public required Guid TransactionId { get; init; }
            public required HashSet<IEntityType> StateChangingEntities { get; init; }
            public required bool CanHaveDirtyReads { get; init; }
        }

        private readonly ConditionalWeakTable<DbContext, TransactionState> _conditionalWeakTable = new ConditionalWeakTable<DbContext, TransactionState>();

        private void TryRemoveCommitedTransactionData(DbContext? dbContext)
        {
            if (dbContext is not null && _conditionalWeakTable.Remove(dbContext, out var transactionData))
            {
                var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

                dbQueryCacheStore.RemoveAllByTransactionId(transactionData.TransactionId);

                if (transactionData.StateChangingEntities.Count != 0)
                {
                    dbQueryCacheStore.RemoveRootEntities(transactionData.StateChangingEntities);
                }
            }
        }
        private async ValueTask TryRemoveCommitedTransactionDataAsync(DbContext? dbContext)
        {
            if (dbContext is not null && _conditionalWeakTable.Remove(dbContext, out var transactionData))
            {
                var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

                await dbQueryCacheStore.RemoveAllByTransactionIdAsync(transactionData.TransactionId).ConfigureAwait(false);

                if (transactionData.StateChangingEntities.Count != 0)
                {
                    await dbQueryCacheStore.RemoveRootEntitiesAsync(transactionData.StateChangingEntities).ConfigureAwait(false);
                }
            }
        }

        private void TryRemoveUncommitedTransactionData(DbContext? dbContext)
        {
            if (dbContext is not null && _conditionalWeakTable.Remove(dbContext, out var transactionData))
            {
                var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

                dbQueryCacheStore.RemoveAllByTransactionId(transactionData.TransactionId);
            }
        }
        private async ValueTask TryRemoveUncommitedTransactionDataAsync(DbContext? dbContext)
        {
            if (dbContext is not null && _conditionalWeakTable.Remove(dbContext, out var transactionData))
            {
                var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

                await dbQueryCacheStore.RemoveAllByTransactionIdAsync(transactionData.TransactionId).ConfigureAwait(false);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TrySetTransactionState(DbContext? dbContext, DbTransaction? transaction, in Guid transactionId)
        {
            if (dbContext is not null)
            {
                var state = new TransactionState
                {
                    StateChangingEntities = new HashSet<IEntityType>(8),
                    TransactionId = transactionId,
                    CanHaveDirtyReads = transaction?.IsolationLevel == IsolationLevel.ReadUncommitted,
                };

                _conditionalWeakTable.AddOrUpdate(dbContext, state);
            }
        }

        public DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
        {
            TrySetTransactionState(eventData.Context, eventData.Transaction, eventData.TransactionId);

            return result;
        }
        public ValueTask<DbTransaction> TransactionStartedAsync(
            DbConnection connection,
            TransactionEndEventData eventData,
            DbTransaction result,
            CancellationToken cancellationToken = default)
        {
            TrySetTransactionState(eventData.Context, eventData.Transaction, eventData.TransactionId);

            return new(result);
        }

        public DbTransaction TransactionUsed(DbConnection connection, TransactionEventData eventData, DbTransaction result)
        {
            TrySetTransactionState(eventData.Context, eventData.Transaction, eventData.TransactionId);

            return result;
        }
        public ValueTask<DbTransaction> TransactionUsedAsync(
            DbConnection connection,
            TransactionEventData eventData,
            DbTransaction result,
            CancellationToken cancellationToken = default)
        {
            TrySetTransactionState(eventData.Context, eventData.Transaction, eventData.TransactionId);

            return new(result);
        }

        public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
        {
            TryRemoveCommitedTransactionData(eventData.Context);
        }
        public async Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            await TryRemoveCommitedTransactionDataAsync(eventData.Context).ConfigureAwait(false);
        }

        public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
        {
            TryRemoveUncommitedTransactionData(eventData.Context);
        }
        public async Task TransactionRolledBackAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            await TryRemoveUncommitedTransactionDataAsync(eventData.Context).ConfigureAwait(false);
        }

        public void RolledBackToSavepoint(DbTransaction transaction, TransactionEventData eventData)
        {
            TryRemoveUncommitedTransactionData(eventData.Context);
        }
        public async Task RolledBackToSavepointAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            CancellationToken cancellationToken = default)
        {
            await TryRemoveUncommitedTransactionDataAsync(eventData.Context).ConfigureAwait(false);
        }

        public void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
        {
            TryRemoveUncommitedTransactionData(eventData.Context);
        }
        public async Task TransactionFailedAsync(
            DbTransaction transaction,
            TransactionErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            await TryRemoveUncommitedTransactionDataAsync(eventData.Context).ConfigureAwait(false);
        }

        #endregion Transactions

        public int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
        {
            CommandExecuted(command.CommandText, eventData.Context, eventData.CommandSource);

            return result;
        }
        public async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            await CommandExecutedAsync(command.CommandText, eventData.Context, eventData.CommandSource).ConfigureAwait(false);

            return result;
        }

        public DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {
            CommandExecuted(command.CommandText, eventData.Context, eventData.CommandSource);

            return result;
        }
        public async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            await CommandExecutedAsync(command.CommandText, eventData.Context, eventData.CommandSource).ConfigureAwait(false);

            return result;
        }

        public object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
        {
            CommandExecuted(command.CommandText, eventData.Context, eventData.CommandSource);

            return result;
        }
        public async ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
        {
            await CommandExecutedAsync(command.CommandText, eventData.Context, eventData.CommandSource).ConfigureAwait(false);

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

        private void CommandExecuted(string command, DbContext? context, CommandSource commandSource)
        {
            if (TryGetStateChangingEntities(command, context, commandSource, out var stateChangingEntities))
            {
                Invalidate(_conditionalWeakTable, context, stateChangingEntities);
            }

            static void Invalidate(
                ConditionalWeakTable<DbContext, TransactionState> conditionalWeakTable, 
                DbContext dbContext,
                HashSet<IEntityType> stateChangingEntities)
            {
                if (stateChangingEntities.Count == 0)
                {
                    return;
                }

                var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

                if (conditionalWeakTable.TryGetValue(dbContext, out var transactionState))
                {
                    transactionState.StateChangingEntities.UnionWith(stateChangingEntities);
                    dbQueryCacheStore.RemoveAllByTransactionId(transactionState.TransactionId);

                    if (transactionState.CanHaveDirtyReads)
                    {
                        dbQueryCacheStore.RemoveRootEntities(stateChangingEntities);
                    }
                }
                else
                {
                    dbQueryCacheStore.RemoveRootEntities(stateChangingEntities);
                }
            }
        }

        private async ValueTask CommandExecutedAsync(string command, DbContext? context, CommandSource commandSource)
        {
            if (TryGetStateChangingEntities(command, context, commandSource, out var stateChangingEntities))
            {
                await InvalidateAsync(_conditionalWeakTable, context, stateChangingEntities).ConfigureAwait(false);
            }

            static async ValueTask InvalidateAsync(
                ConditionalWeakTable<DbContext, TransactionState> conditionalWeakTable, 
                DbContext dbContext,
                HashSet<IEntityType> stateChangingEntities)
            {
                if (stateChangingEntities.Count == 0)
                {
                    return;
                }

                var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

                if (conditionalWeakTable.TryGetValue(dbContext, out var transactionState))
                {
                    transactionState.StateChangingEntities.UnionWith(stateChangingEntities);

                    await dbQueryCacheStore.RemoveAllByTransactionIdAsync(transactionState.TransactionId).ConfigureAwait(false);

                    if (transactionState.CanHaveDirtyReads)
                    {
                        await dbQueryCacheStore.RemoveRootEntitiesAsync(stateChangingEntities).ConfigureAwait(false);
                    }
                }
                else
                {
                    await dbQueryCacheStore.RemoveRootEntitiesAsync(stateChangingEntities).ConfigureAwait(false);
                }
            }
        }
    }
}
