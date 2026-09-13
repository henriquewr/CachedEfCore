using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Linq;

namespace CachedEfCore.EntityMapping
{
    public class TableEntityMapping
    {
        private static readonly ConcurrentDictionary<IModel, TableEntityMapping> _tableEntityCache = new();

        public static TableEntityMapping GetOrAdd(IModel model)
        {
            return _tableEntityCache.GetOrAdd(model, key => new(model));
        }

        public FrozenDictionary<string, ImmutableArray<IEntityType>> Mapping { get; }

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