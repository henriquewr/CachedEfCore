using CachedEfCore.DependencyManager.Attributes;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace CachedEfCore.DependencyManager
{
    public partial class EntityDependency
    {
        private sealed record class AllEntitiesUnderRelatedCacheKey(Type Type, bool RelatedInFks);
        private sealed record class UnderRelatedCacheKey(string Name, bool RelatedInFks);


        private readonly ConcurrentDictionary<UnderRelatedCacheKey, FrozenSet<IEntityType>> _underRelatedCache = new();
        private readonly ConcurrentDictionary<AllEntitiesUnderRelatedCacheKey, FrozenSet<IEntityType>> _allEntitiesUnderRelatedCache = new();


        /// <summary>
        /// 
        /// </summary>
        /// <param name="rootEntityType"></param>
        /// <param name="includingRelatedInFks">Uses Foreign Keys in the search, in C# entities cannot have the Navigation, so in C# it's not related, but still related using DB logic (using Fks)</param>
        /// <returns></returns>
        public FrozenSet<IEntityType> GetUnderRelatedEntities(IEntityType rootEntityType, bool includingRelatedInFks)
        {
            return GetUnderRelatedEntitiesSingleCachedImpl(rootEntityType, includingRelatedInFks);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rootType"></param>
        /// <param name="includingRelatedInFks">Uses Foreign Keys in the search, in C# entities cannot have the Navigation, so in C# it's not related, but still related using DB logic (using Fks)</param>
        /// <returns></returns>
        public FrozenSet<IEntityType> GetUnderRelatedEntities(Type rootType, bool includingRelatedInFks)
        {
            return GetUnderRelatedEntitiesInternal(rootType, includingRelatedInFks);
        }

        private FrozenSet<IEntityType> GetUnderRelatedEntitiesInternal(Type rootType, bool relatedInFks)
        {
            if (TryGetIEntityTypes(rootType, out var fastEntityTypes))
            {
                return GetUnderRelatedEntitiesMultipleCachedImpl(fastEntityTypes, relatedInFks);
            }

            var cacheKey = new AllEntitiesUnderRelatedCacheKey(rootType, relatedInFks);

            if (!_allEntitiesUnderRelatedCache.TryGetValue(cacheKey, out var underRelatedEntities))
            {
                var entities = GetAllEntitiesByType(GetAllTypes(rootType));

                underRelatedEntities = GetEntities(entities, entityType => GetUnderRelatedEntitiesSingleImpl(entityType, relatedInFks));

                _allEntitiesUnderRelatedCache[cacheKey] = underRelatedEntities;
            }

            return underRelatedEntities;
        }

        private FrozenSet<IEntityType> GetUnderRelatedEntitiesMultipleCachedImpl(ImmutableArray<IEntityType> rootEntityTypes, bool relatedInFks)
        {
            if (rootEntityTypes.Length == 1)
            {
                return GetUnderRelatedEntitiesSingleCachedImpl(rootEntityTypes[0], relatedInFks);
            }

            var entities = string.Join(',', rootEntityTypes.Select(x => x.Name));
            var key = new UnderRelatedCacheKey($"[{entities}]", relatedInFks);

            if (_underRelatedCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var allVisited = new HashSet<IEntityType>();

            foreach (var rootEntityType in rootEntityTypes)
            {
                var visited = GetUnderRelatedEntitiesSingleImpl(rootEntityType, relatedInFks);
                allVisited.UnionWith(visited);
            }

            var cache = allVisited.ToFrozenSet();

            _underRelatedCache[key] = cache;

            return cache;
        }

        private FrozenSet<IEntityType> GetUnderRelatedEntitiesSingleCachedImpl(IEntityType rootEntityType, bool relatedInFks)
        {
            var key = new UnderRelatedCacheKey(rootEntityType.Name, relatedInFks);

            if (_underRelatedCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var visited = GetUnderRelatedEntitiesSingleImpl(rootEntityType, relatedInFks);

            var cache = visited.ToFrozenSet();

            _underRelatedCache[key] = cache;

            return cache;
        }

        private HashSet<IEntityType> GetUnderRelatedEntitiesSingleImpl(IEntityType rootEntityType, bool relatedInFks)
        {
            var visited = new HashSet<IEntityType>();
            var stack = new Stack<IEntityType>();

            stack.Push(rootEntityType);

            while (stack.Count > 0)
            {
                var currentEntity = stack.Pop();

                if (!visited.Add(currentEntity))
                {
                    continue;
                }

                var underRelated = GetShallowUnderRelatedEntities(currentEntity, relatedInFks);

                foreach (var underRelatedEntity in underRelated)
                {
                    stack.Push(underRelatedEntity);
                }
            }

            return visited;
        }

        private IEnumerable<IEntityType> GetShallowUnderRelatedEntities(IEntityType entityType, bool underRelatedIncludingFks)
        {
            var withAttr = GetEntitiesDependentOnEntityAttribute(entityType.ClrType);
            foreach (var item in withAttr)
            {
                yield return item;
            }

            if (underRelatedIncludingFks)
            {
                var foreignKeys = entityType.GetForeignKeys();
                foreach (var foreignKey in foreignKeys)
                {
                    yield return foreignKey.PrincipalEntityType;
                }
            }

            var navigations = entityType.GetNavigations();
            foreach (var navigation in navigations)
            {
                yield return navigation.TargetEntityType;
            }

            var skipNavigations = entityType.GetSkipNavigations();
            foreach (var skipNavigation in skipNavigations)
            {
                yield return skipNavigation.JoinEntityType;
                yield return skipNavigation.TargetEntityType;
            }
        }

        private IEnumerable<IEntityType> GetEntitiesDependentOnEntityAttribute(Type type)
        {
            var dependentOnEntity = type.GetCustomAttribute<DependentOnEntityAttribute>();

            if (dependentOnEntity is not null)
            {
                foreach (var item in dependentOnEntity.DependentEntities)
                {
                    if (_typeEntityMapping.Mapping.TryGetValue(item, out var entityTypes))
                    {
                        foreach (var entityType in entityTypes)
                        {
                            yield return entityType;
                        }
                    }
                }
            }
        }
    }
}
