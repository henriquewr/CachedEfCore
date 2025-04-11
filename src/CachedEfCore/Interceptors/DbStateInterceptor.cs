using CachedEfCore.Context;
using CachedEfCore.SqlAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CachedEfCore.Interceptors
{
    public class DbStateInterceptor : DbCommandInterceptor
    {
        private readonly ISqlQueryEntityExtractor _sqlQueryEntityExtractor;
        public DbStateInterceptor(ISqlQueryEntityExtractor sqlQueryEntityExtractor)
        {
            _sqlQueryEntityExtractor = sqlQueryEntityExtractor;
        }

        private void CommandExecuting(string command, ICachedDbContext context, ChangeTracker contextChangeTracker, CommandSource commandSource)
        {
            switch (commandSource)
            {
                case CommandSource.Migrations:
                case CommandSource.Unknown:
                case CommandSource.LinqQuery:
                    return;

                case CommandSource.SaveChanges:
                    var modifiedEntities = contextChangeTracker.Entries().Where(x => x.State != EntityState.Unchanged);
                    var modifiedEntitiesTypes = modifiedEntities
                      .Select(e => e.Metadata)
                      .ToHashSet();

                    if (modifiedEntitiesTypes.Count != 0)
                    {
                        context.DbQueryCacheStore.RemoveRootEntities(modifiedEntitiesTypes, context.DependencyManager);
                    }
                    return;

                case CommandSource.ExecuteUpdate:
                case CommandSource.ExecuteDelete:
                case CommandSource.ExecuteSqlRaw:
                case CommandSource.FromSqlQuery:
                default:
                    var stateChangingEntities = _sqlQueryEntityExtractor.GetStateChangingEntityTypesFromSql(context.TableEntity, command).ToHashSet();

                    if (stateChangingEntities.Count != 0)
                    {
                        context.DbQueryCacheStore.RemoveRootEntities(stateChangingEntities, context.DependencyManager);
                    }
                return;
            }
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            if (eventData.Context is ICachedDbContext cachedDbContext)
            {
                CommandExecuting(command.CommandText, cachedDbContext, eventData.Context.ChangeTracker, eventData.CommandSource);
            }

            return base.ReaderExecuting(command, eventData, result);
        }

        public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            if (eventData.Context is ICachedDbContext cachedDbContext)
            {
                CommandExecuting(command.CommandText, cachedDbContext, eventData.Context.ChangeTracker, eventData.CommandSource);
            }

            return base.ScalarExecuting(command, eventData, result);
        }

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            if (eventData.Context is ICachedDbContext cachedDbContext)
            {
                CommandExecuting(command.CommandText, cachedDbContext, eventData.Context.ChangeTracker, eventData.CommandSource);
            }

            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is ICachedDbContext cachedDbContext)
            {
                CommandExecuting(command.CommandText, cachedDbContext, eventData.Context.ChangeTracker, eventData.CommandSource);
            }

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is ICachedDbContext cachedDbContext)
            {
                CommandExecuting(command.CommandText, cachedDbContext, eventData.Context.ChangeTracker, eventData.CommandSource);
            }

            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is ICachedDbContext cachedDbContext)
            {
                CommandExecuting(command.CommandText, cachedDbContext, eventData.Context.ChangeTracker, eventData.CommandSource);
            }

            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
