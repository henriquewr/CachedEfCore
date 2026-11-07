using CachedEfCore.EntityMapping;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace CachedEfCore.DependencyManager
{
    public partial class EntityDependency
    {
        private static readonly ConcurrentDictionary<IModel, EntityDependency> _entityDependencyCache = new();

        public static EntityDependency GetOrAdd(IModel model)
        {
            return _entityDependencyCache.GetOrAdd(model, key => new(model));
        }

        private readonly TypeEntityMapping _typeEntityMapping;

        public EntityDependency(IModel dbModel)
        {
            _typeEntityMapping = TypeEntityMapping.GetOrAdd(dbModel);
        }

        private IEnumerable<IEntityType> GetAllEntitiesByType(HashSet<Type> propsTypes)
        {
            var mapping = _typeEntityMapping.Mapping;
            foreach (var item in propsTypes)
            {
                if (mapping.TryGetValue(item, out var entityType))
                {
                    foreach (var entity in entityType)
                    {
                        yield return entity;
                    }
                }
            }
        }

        private static HashSet<Type> GetAllTypes(Type type)
        {
            return TypeScanner.GetAllReferencedTypes(type);
        }

        private static HashSet<Type> GetShallowTypes(Type type)
        {
            return TypeScanner.GetShallowReferencedTypes(type);
        }

        private static FrozenSet<IEntityType> GetEntities(IEnumerable<IEntityType> entities, Func<IEntityType, IEnumerable<IEntityType>> getEntities)
        {
            var result = new HashSet<IEntityType>();

            foreach (var entity in entities)
            {
                result.UnionWith(getEntities(entity));
            }

            return result.ToFrozenSet();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetIEntityTypes(Type type, out ImmutableArray<IEntityType> entityTypes)
        {
            var success = _typeEntityMapping.Mapping.TryGetValue(type, out entityTypes) ||
                //Probably Lazy load proxies
                (type.BaseType is not null && _typeEntityMapping.Mapping.TryGetValue(type.BaseType, out entityTypes));

            return success;
        }
    }
}