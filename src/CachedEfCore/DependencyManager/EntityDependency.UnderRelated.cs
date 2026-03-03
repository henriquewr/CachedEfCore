using CachedEfCore.DependencyManager.Attributes;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace CachedEfCore.DependencyManager
{
    public partial class EntityDependency
    {
        private record class AllEntitiesUnderRelatedCacheKey(Type Type, bool RelatedInFks, bool IgnoreDependentOnEntityAttribute);

        private readonly ConcurrentDictionary<string, FrozenSet<IEntityType>> _underRelatedCache = new();
        private readonly ConcurrentDictionary<AllEntitiesUnderRelatedCacheKey, FrozenSet<IEntityType>> _allEntitiesUnderRelatedCache = new();


        /// <summary>
        /// 
        /// </summary>
        /// <param name="rootEntityType"></param>
        /// <param name="includingRelatedInFks">Uses Foreign Keys in the search, in C# entities cannot have the Navigation, so in C# it's not related, but still related using DB logic (using Fks)</param>
        /// <returns></returns>
        public FrozenSet<IEntityType> GetUnderRelatedEntities(IEntityType rootEntityType, bool includingRelatedInFks)
        {
            return GetUnderRelatedEntitiesImpl(rootEntityType, includingRelatedInFks, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rootType"></param>
        /// <param name="includingRelatedInFks">Uses Foreign Keys in the search, in C# entities cannot have the Navigation, so in C# it's not related, but still related using DB logic (using Fks)</param>
        /// <returns></returns>
        public FrozenSet<IEntityType> GetUnderRelatedEntities(Type rootType, bool includingRelatedInFks)
        {
            return GetUnderRelatedEntitiesInternal(rootType, includingRelatedInFks, false);
        }

        private FrozenSet<IEntityType> GetUnderRelatedEntitiesInternal(Type rootType, bool relatedInFks, bool ignoreDependentOnEntityAttribute)
        {
            if (TryGetIEntityType(rootType, out var fastEntityType))
            {
                return GetUnderRelatedEntitiesImpl(fastEntityType, relatedInFks, ignoreDependentOnEntityAttribute);
            }

            var cacheKey = new AllEntitiesUnderRelatedCacheKey(rootType, relatedInFks, ignoreDependentOnEntityAttribute);

            if (!_allEntitiesUnderRelatedCache.TryGetValue(cacheKey, out var underRelatedEntities))
            {
                var entities = GetAllEntitiesByType(GetAllTypes(rootType));

                underRelatedEntities = GetEntities(entities, entityType => GetUnderRelatedEntitiesImpl(entityType, relatedInFks, ignoreDependentOnEntityAttribute));

                _allEntitiesUnderRelatedCache[cacheKey] = underRelatedEntities;
            }

            return underRelatedEntities;
        }

        private FrozenSet<IEntityType> GetUnderRelatedEntitiesImpl(IEntityType rootEntityType, bool relatedInFks, bool ignoreDependentOnEntityAttribute)
        {
            var key = $"{relatedInFks}.{ignoreDependentOnEntityAttribute}.{rootEntityType.Name}";

            if (_underRelatedCache.TryGetValue(key, out var cached))
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

                var underRelated = GetShallowUnderRelatedEntities(currentEntity, relatedInFks, ignoreDependentOnEntityAttribute);

                foreach (var underRelatedEntity in underRelated)
                {
                    stack.Push(underRelatedEntity);
                }
            }

            var cache = visited.ToFrozenSet();

            _underRelatedCache[key] = cache;

            return cache;
        }

        private IEnumerable<IEntityType> GetShallowUnderRelatedEntities(IEntityType entityType, bool underRelatedIncludingFks, bool ignoreDependentOnEntityAttribute)
        {
            if (!ignoreDependentOnEntityAttribute)
            {
                var withAttr = GetEntitiesDependentOnEntityAttribute(entityType.ClrType);
                foreach (var item in withAttr)
                {
                    yield return item;
                }
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
            var dependentOnEntity = type.GetCustomAttribute<DependentOnEntity>();

            if (dependentOnEntity != null)
            {
                foreach (var item in dependentOnEntity.DependentEntities)
                {
                    if (_typeEntity.TryGetValue(item, out var entityType))
                    {
                        yield return entityType;
                    }
                }
            }
        }
    }
}
