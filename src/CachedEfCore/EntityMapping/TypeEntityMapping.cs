using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Linq;

namespace CachedEfCore.EntityMapping
{
    public class TypeEntityMapping
    {
        private static readonly ConcurrentDictionary<IModel, TypeEntityMapping> _typeEntityMapping = new();

        public static TypeEntityMapping GetOrAdd(IModel model)
        {
            return _typeEntityMapping.GetOrAdd(model, static (key, model) => new(model), model);
        }

        public FrozenDictionary<Type, ImmutableArray<IEntityType>> Mapping { get; }

        public TypeEntityMapping(IModel dbModel)
        {
            Mapping = GetTableEntity(dbModel);
        }

        private static FrozenDictionary<Type, ImmutableArray<IEntityType>> GetTableEntity(IModel model)
        {
            var tableEntity = model.GetEntityTypes().Where(static x => x.ClrType != null).GroupBy(static x => x.ClrType).ToFrozenDictionary(static k => k.Key, static v => v.ToImmutableArray());
            return tableEntity;
        }
    }
}
