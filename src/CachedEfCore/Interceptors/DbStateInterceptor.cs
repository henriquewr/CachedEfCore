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

        private void TryRemoveUncommitedTransactionData(DbContext? dbContext)
        {
            if (dbContext is not null && _conditionalWeakTable.Remove(dbContext, out var transactionData))
            {
                var dbQueryCacheStore = dbContext.GetService<IDbQueryCacheStore>();

                dbQueryCacheStore.RemoveAllByTransactionId(transactionData.TransactionId);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TrySetTransactionState(DbContext? dbContext, DbTransaction transaction, in Guid transactionId)
        {
            if (dbContext is not null)
            {
                var state = new TransactionState
                {
                    StateChangingEntities = new HashSet<IEntityType>(8),
                    TransactionId = transactionId,
                    CanHaveDirtyReads = transaction.IsolationLevel == IsolationLevel.ReadUncommitted,
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
        public Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            TryRemoveCommitedTransactionData(eventData.Context);

            return Task.CompletedTask;
        }

        public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
        {
            TryRemoveUncommitedTransactionData(eventData.Context);
        }
        public Task TransactionRolledBackAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            TryRemoveUncommitedTransactionData(eventData.Context);

            return Task.CompletedTask;
        }

        public void RolledBackToSavepoint(DbTransaction transaction, TransactionEventData eventData)
        {
            TryRemoveUncommitedTransactionData(eventData.Context);
        }
        public Task RolledBackToSavepointAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            CancellationToken cancellationToken = default)
        {
            TryRemoveUncommitedTransactionData(eventData.Context);

            return Task.CompletedTask;
        }

        public void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
        {
            TryRemoveUncommitedTransactionData(eventData.Context);
        }
        public Task TransactionFailedAsync(
            DbTransaction transaction,
            TransactionErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            TryRemoveUncommitedTransactionData(eventData.Context);

            return Task.CompletedTask;
        }

        #endregion Transactions

        public int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
        {
            CommandExecuted(command.CommandText, eventData.Context, eventData.CommandSource);

            return result;
        }
        public ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            CommandExecuted(command.CommandText, eventData.Context, eventData.CommandSource);

            return new(result);
        }

        public DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {
            CommandExecuted(command.CommandText, eventData.Context, eventData.CommandSource);

            return result;
        }
        public ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            CommandExecuted(command.CommandText, eventData.Context, eventData.CommandSource);

            return new(result);
        }

        public object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
        {
            CommandExecuted(command.CommandText, eventData.Context, eventData.CommandSource);

            return result;
        }
        public ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
        {
            CommandExecuted(command.CommandText, eventData.Context, eventData.CommandSource);

            return new(result);
        }

        private void CommandExecuted(string command, DbContext? context, CommandSource commandSource)
        {
            if (context is null)
            {
                return;
            }

            switch (commandSource)
            {
                case CommandSource.Migrations:
                case CommandSource.Unknown:
                case CommandSource.LinqQuery:
                    return;

                case CommandSource.SaveChanges:
                    var modifiedEntities = context.ChangeTracker.Entries().Where(static x => x.State != EntityState.Unchanged);
                    var modifiedEntitiesTypes = modifiedEntities
                      .Select(static e => e.Metadata)
                      .ToHashSet();

                    Invalidate(_conditionalWeakTable, context, modifiedEntitiesTypes);

                    return;

                case CommandSource.ExecuteUpdate:
                case CommandSource.ExecuteDelete:
                case CommandSource.ExecuteSqlRaw:
                case CommandSource.FromSqlQuery:
                default:
                    var tableEntityMapping = context.GetService<TableEntityMapping>();

                    var stateChangingEntities = _sqlQueryEntityExtractor.GetStateChangingEntityTypesFromSql(tableEntityMapping, command).ToHashSet();
                    Invalidate(_conditionalWeakTable, context, stateChangingEntities);
                return;
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
    }
}
