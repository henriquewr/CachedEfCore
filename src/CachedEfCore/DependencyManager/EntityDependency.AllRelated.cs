using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace CachedEfCore.DependencyManager
{
    public partial class EntityDependency
    {
        private record class AllEntitiesAllRelatedCacheKey(Type Type, bool UnderRelatedIncludingFks);

        private readonly ConcurrentDictionary<string, FrozenSet<IEntityType>> _allRelatedCache = new();
        private readonly ConcurrentDictionary<AllEntitiesAllRelatedCacheKey, FrozenSet<IEntityType>> _allEntitiesAllRelatedCache = new();

        public FrozenSet<IEntityType> GetAllRelatedEntities(Type rootType, bool underRelatedIncludingFks)
        {
            if (TryGetIEntityType(rootType, out var fastEntityType))
            {
                return GetAllRelatedEntitiesImpl(fastEntityType, underRelatedIncludingFks);
            }

            var cacheKey = new AllEntitiesAllRelatedCacheKey(rootType, underRelatedIncludingFks);

            if (!_allEntitiesAllRelatedCache.TryGetValue(cacheKey, out var allRelatedEntities))
            {
                var entities = GetAllEntitiesByType(GetAllTypes(rootType));

                allRelatedEntities = GetEntities(entities, x => GetAllRelatedEntities(x, underRelatedIncludingFks));

                _allEntitiesAllRelatedCache[cacheKey] = allRelatedEntities;
            }

            return allRelatedEntities;
        }

        public FrozenSet<IEntityType> GetAllRelatedEntities(IEntityType rootEntityType, bool underRelatedIncludingFks)
        {
            return GetAllRelatedEntitiesImpl(rootEntityType, underRelatedIncludingFks);
        }

        private FrozenSet<IEntityType> GetAllRelatedEntitiesImpl(IEntityType rootEntityType, bool underRelatedIncludingFks)
        {
            var key = $"{underRelatedIncludingFks}.{rootEntityType.Name}";
            if (_allRelatedCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

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

                var upperRelated = GetShallowUpperRelatedEntities(currentEntity);

                foreach (var upperRelatedEntity in upperRelated)
                {
                    stack.Push(upperRelatedEntity);
                }

                var underRelated = GetShallowUnderRelatedEntities(currentEntity, underRelatedIncludingFks, false);

                foreach (var underRelatedEntity in underRelated)
                {
                    stack.Push(underRelatedEntity);
                }
            }

            var cache = visited.ToFrozenSet();

            _allRelatedCache[key] = cache;

            return cache;
        }
    }
}
