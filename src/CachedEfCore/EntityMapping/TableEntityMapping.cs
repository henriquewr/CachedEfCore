using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Linq;
using System.Collections.Immutable;

namespace CachedEfCore.EntityMapping
{
    public class TableEntityMapping
    {
        private static readonly ConcurrentDictionary<Type, TableEntityMapping> _tableEntityCache = new();

        public static TableEntityMapping GetOrAdd(DbContext dbContext)
        {
            return _tableEntityCache.GetOrAdd(dbContext.GetType(), key => new(dbContext.Model));
        }

        public readonly FrozenDictionary<string, ImmutableArray<IEntityType>> Mapping;

        public TableEntityMapping(IModel dbModel)
        {
            Mapping = GetTableEntity(dbModel);
        }

        private static FrozenDictionary<string, ImmutableArray<IEntityType>> GetTableEntity(IModel model)
        {
            var tableEntity = model.GetEntityTypes().GroupBy(x => x.GetTableName() ?? x.GetViewName()!).ToFrozenDictionary(k => k.Key, v => v.ToImmutableArray());
            return tableEntity;
        }
    }
}