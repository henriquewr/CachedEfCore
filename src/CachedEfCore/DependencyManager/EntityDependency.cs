using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Collections.Frozen;
using System.Reflection;
using CachedEfCore.DependencyManager.Attributes;

namespace CachedEfCore.DependencyManager
{
    public class EntityDependency
    {
        private static readonly ConcurrentDictionary<Type, EntityDependency> _entityDependencyCache = new();

        public static EntityDependency GetOrAdd(DbContext dbContext)
        {
            return _entityDependencyCache.GetOrAdd(dbContext.GetType(), key => new(dbContext.Model));
        }

        private readonly ConcurrentDictionary<string, HashSet<IEntityType>> _underRelatedCache = new();
        private readonly ConcurrentDictionary<string, HashSet<IEntityType>> _aboveRelatedCache = new();
        private readonly ConcurrentDictionary<string, HashSet<IEntityType>> _allRelatedCache = new();
        private readonly ConcurrentDictionary<string, bool> _hasLazyLoadCache = new();

        private readonly FrozenDictionary<Type, IEntityType> _typeEntity;

        public EntityDependency(IModel dbModel)
        {
            _typeEntity = dbModel.GetEntityTypes().Select(x => new KeyValuePair<Type, IEntityType>(x.ClrType, x)).DistinctBy(x => x.Key).ToFrozenDictionary();
        }

        private IEnumerable<IEntityType> GetAllEntitiesByType(IEnumerable<Type> propsTypes)
        {
            foreach (var item in propsTypes)
            {
                if (_typeEntity.TryGetValue(item, out var entityType))
                {
                    yield return entityType;
                }
            }
        }

        private static IEnumerable<Type> GetAllTypes(Type type)
        {
            var propsTypes = type.GetProperties().SelectMany(x => x.PropertyType.GetGenericArguments().Append(x.PropertyType)).Append(type);

            return propsTypes;
        }

        private static HashSet<IEntityType> GetEntities(IEnumerable<IEntityType> entities, Func<IEntityType, IEnumerable<IEntityType>> getEntities)
        {
            var result = new HashSet<IEntityType>();

            foreach (var entity in entities)
            {
                result.UnionWith(getEntities(entity));
            }

            return result;
        }



        public HashSet<IEntityType> GetUnderRelatedEntities(IEntityType rootEntityType)
        {
            return GetUnderRelatedEntitiesImpl(rootEntityType, false);
        }

        public HashSet<IEntityType> GetUnderRelatedEntities(Type rootType)
        {
            return GetUnderRelatedEntitiesInternal(rootType, false);
        }

        private HashSet<IEntityType> GetUnderRelatedEntitiesInternal(Type rootType, bool ignoreDependentOnEntityAttribute)
        {
            if (_typeEntity.TryGetValue(rootType, out var entityType))
            {
                return GetUnderRelatedEntitiesImpl(entityType, ignoreDependentOnEntityAttribute);
            }

            var entities = GetAllEntitiesByType(GetAllTypes(rootType));

            return GetEntities(entities, entityType => GetUnderRelatedEntitiesImpl(entityType, ignoreDependentOnEntityAttribute));
        }

        private HashSet<IEntityType> GetUnderRelatedEntitiesImpl(IEntityType rootEntityType, bool ignoreDependentOnEntityAttribute)
        {
            var key = $"{ignoreDependentOnEntityAttribute}.{rootEntityType.ClrType.FullName}";

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

                var underRelated = GetShallowUnderRelatedEntities(currentEntity, ignoreDependentOnEntityAttribute);

                foreach (var underRelatedEntity in underRelated)
                {
                    stack.Push(underRelatedEntity);
                }
            }

            _underRelatedCache[key] = visited;

            return visited;
        }



        public HashSet<IEntityType> GetAboveRelatedEntities(Type rootType)
        {
            if (_typeEntity.TryGetValue(rootType, out var entityType))
            {
                return GetAboveRelatedEntitiesImpl(entityType);
            }

            var entities = GetAllEntitiesByType(GetAllTypes(rootType));

            return GetEntities(entities, GetAboveRelatedEntitiesImpl);
        }

        public HashSet<IEntityType> GetAboveRelatedEntities(IEntityType rootEntityType)
        {
            return GetAboveRelatedEntitiesImpl(rootEntityType);
        }

        private HashSet<IEntityType> GetAboveRelatedEntitiesImpl(IEntityType rootEntityType)
        {
            var key = rootEntityType.ClrType.FullName!;

            if (_aboveRelatedCache.TryGetValue(key, out var cached))
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

                var aboveRelated = GetShallowAboveRelatedEntities(currentEntity);

                foreach (var aboveRelatedEntity in aboveRelated)
                {
                    stack.Push(aboveRelatedEntity);
                }
            }

            _aboveRelatedCache[key] = visited;

            return visited;
        }



        public HashSet<IEntityType> GetAllRelatedEntities(Type rootType)
        {
            if (_typeEntity.TryGetValue(rootType, out var entityType))
            {
                return GetAllRelatedEntitiesImpl(entityType);
            }

            var entities = GetAllEntitiesByType(GetAllTypes(rootType));

            return GetEntities(entities, GetAllRelatedEntities);
        }

        public HashSet<IEntityType> GetAllRelatedEntities(IEntityType rootEntityType)
        {
            return GetAllRelatedEntitiesImpl(rootEntityType);
        }

        private HashSet<IEntityType> GetAllRelatedEntitiesImpl(IEntityType rootEntityType)
        {
            var key = rootEntityType.ClrType.FullName!;

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

                var aboveRelated = GetShallowAboveRelatedEntities(currentEntity);

                foreach (var aboveRelatedEntity in aboveRelated)
                {
                    stack.Push(aboveRelatedEntity);
                }

                var underRelated = GetShallowUnderRelatedEntities(currentEntity, false);

                foreach (var underRelatedEntity in underRelated)
                {
                    stack.Push(underRelatedEntity);
                }
            }

            _allRelatedCache[key] = visited;

            return visited;
        }



        private IEnumerable<IEntityType> GetShallowAboveRelatedEntities(IEntityType entityType)
        {
            var withAttr = GetEntitiesReferencingTypeInDependentOnEntityAttribute(entityType.ClrType);
            foreach (var item in withAttr)
            {
                yield return item;
            }

            var foreignKeys = entityType.GetForeignKeys();

            foreach (var foreignKey in foreignKeys)
            {
                var navigation = foreignKey.GetNavigation(false);

                if (navigation is not null && navigation.ForeignKey == foreignKey && navigation.IsCollection == true)
                {
                    yield return foreignKey.PrincipalEntityType;
                }
            }

            var referencingForeignKeys = entityType.GetReferencingForeignKeys();
            foreach (var referencingForeignKey in referencingForeignKeys)
            {
                yield return referencingForeignKey.DeclaringEntityType;
            }
        }

        private IEnumerable<IEntityType> GetShallowUnderRelatedEntities(IEntityType entityType, bool ignoreDependentOnEntityAttribute)
        {
            if (!ignoreDependentOnEntityAttribute)
            {
                var withAttr = GetEntitiesDependentOnEntityAttribute(entityType.ClrType);
                foreach (var item in withAttr)
                {
                    yield return item;
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

        private IEnumerable<IEntityType> GetEntitiesReferencingTypeInDependentOnEntityAttribute(Type typeInAttribute)
        {
            var typesWithAttr = _typeEntity.Where(x => x.Key.GetCustomAttribute<DependentOnEntity>()?.DependentEntities.Contains(typeInAttribute) == true).Select(x => x.Value);

            return typesWithAttr;
        }

        public bool HasLazyLoad(Type entityType)
        {
            var key = $"{nameof(HasLazyLoad)}Entity:{entityType.FullName}";
            if (_hasLazyLoadCache.TryGetValue(key, out bool cached))
            {
                return cached;
            }

            var underRelated = GetUnderRelatedEntitiesInternal(entityType, true);
            var hasLazyLoad = underRelated.Count > 1;

            _hasLazyLoadCache[key] = hasLazyLoad;

            return hasLazyLoad;
        }

        public bool HasLazyLoad(IEntityType entityType)
        {
            return HasLazyLoad(entityType.ClrType);
        }
    }
}