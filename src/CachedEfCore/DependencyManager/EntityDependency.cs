using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CachedEfCore.DependencyManager
{
    public partial class EntityDependency
    {
        private static readonly ConcurrentDictionary<Type, EntityDependency> _entityDependencyCache = new();

        public static EntityDependency GetOrAdd(DbContext dbContext)
        {
            return _entityDependencyCache.GetOrAdd(dbContext.GetType(), key => new(dbContext.Model));
        }

        private readonly FrozenDictionary<Type, IEntityType> _typeEntity;

        public EntityDependency(IModel dbModel)
        {
            _typeEntity = dbModel.GetEntityTypes().Select(x => new KeyValuePair<Type, IEntityType>(x.ClrType, x)).DistinctBy(x => x.Key).ToFrozenDictionary();
        }

        private IEnumerable<IEntityType> GetAllEntitiesByType(HashSet<Type> propsTypes)
        {
            foreach (var item in propsTypes)
            {
                if (_typeEntity.TryGetValue(item, out var entityType))
                {
                    yield return entityType;
                }
            }
        }

        private static HashSet<Type> GetAllTypes(Type type)
        {
            return TypeScanner.GetAllReferencedTypes(type);
        }

        private static HashSet<Type> GetShallowTypes(Type type)
        {
            return TypeScanner.GetShallowReferencedTypes(type).ToHashSet();
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
        private bool TryGetIEntityType(Type type, [NotNullWhen(true)] out IEntityType? entityType)
        {
            var success = _typeEntity.TryGetValue(type, out entityType) ||
                //Probably Lazy load proxies
                (type.BaseType is not null && _typeEntity.TryGetValue(type.BaseType, out entityType));

            return success;
        }
    }
}