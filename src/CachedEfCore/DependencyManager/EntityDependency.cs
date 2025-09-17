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

        private readonly ConcurrentDictionary<string, FrozenSet<IEntityType>> _underRelatedCache = new();
        private readonly ConcurrentDictionary<string, FrozenSet<IEntityType>> _upperRelatedCache = new();
        private readonly ConcurrentDictionary<string, FrozenSet<IEntityType>> _allRelatedCache = new();
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

        private static FrozenSet<IEntityType> GetEntities(IEnumerable<IEntityType> entities, Func<IEntityType, IEnumerable<IEntityType>> getEntities)
        {
            var result = new HashSet<IEntityType>();

            foreach (var entity in entities)
            {
                result.UnionWith(getEntities(entity));
            }

            return result.ToFrozenSet();
        }


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
            if (_typeEntity.TryGetValue(rootType, out var entityType))
            {
                return GetUnderRelatedEntitiesImpl(entityType, relatedInFks, ignoreDependentOnEntityAttribute);
            }

            var entities = GetAllEntitiesByType(GetAllTypes(rootType));

            return GetEntities(entities, entityType => GetUnderRelatedEntitiesImpl(entityType, relatedInFks, ignoreDependentOnEntityAttribute));
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



        public FrozenSet<IEntityType> GetUpperRelatedEntities(Type rootType)
        {
            if (_typeEntity.TryGetValue(rootType, out var entityType))
            {
                return GetUpperRelatedEntitiesImpl(entityType);
            }

            var entities = GetAllEntitiesByType(GetAllTypes(rootType));

            return GetEntities(entities, GetUpperRelatedEntitiesImpl);
        }

        public FrozenSet<IEntityType> GetUpperRelatedEntities(IEntityType rootEntityType)
        {
            return GetUpperRelatedEntitiesImpl(rootEntityType);
        }

        private FrozenSet<IEntityType> GetUpperRelatedEntitiesImpl(IEntityType rootEntityType)
        {
            var key = rootEntityType.Name;

            if (_upperRelatedCache.TryGetValue(key, out var cached))
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
            }

            var cache = visited.ToFrozenSet();

            _upperRelatedCache[key] = cache;

            return cache;
        }



        public FrozenSet<IEntityType> GetAllRelatedEntities(Type rootType, bool underRelatedIncludingFks)
        {
            if (_typeEntity.TryGetValue(rootType, out var entityType))
            {
                return GetAllRelatedEntitiesImpl(entityType, underRelatedIncludingFks);
            }

            var entities = GetAllEntitiesByType(GetAllTypes(rootType));

            return GetEntities(entities, x => GetAllRelatedEntities(x, underRelatedIncludingFks));
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



        private IEnumerable<IEntityType> GetShallowUpperRelatedEntities(IEntityType entityType)
        {
            var withAttr = GetEntitiesReferencingTypeInDependentOnEntityAttribute(entityType.ClrType);
            foreach (var item in withAttr)
            {
                yield return item;
            }

            /*
                EntityManyToManySkipNavigation.GetSkipNavigations() will return OtherEntityManyToManySkipNavigation

                class EntityManyToManySkipNavigation
                {
                    //skip navigation
                    public virtual ICollection<OtherEntityManyToManySkipNavigation>? OtherEntityManyToManySkipNavigation { get; set; }
                }

                class OtherEntityManyToManySkipNavigation
                {
                    //skip navigation
                    public virtual ICollection<EntityManyToManySkipNavigation>? EntityManyToManySkipNavigation { get; set; }
                }

                Ef creates that:
                class JunctionEntity
                {
                    public int OtherEntityManyToManySkipNavigationId { get; set; }
                    public int EntityManyToManySkipNavigationId { get; set; }
                }   
              */
            var skipNavigations = entityType.GetSkipNavigations();
            foreach (var skipNavigation in skipNavigations)
            {
                yield return skipNavigation.TargetEntityType;
            }

            var foreignKeys = entityType.GetForeignKeys();
            foreach (var foreignKey in foreignKeys)
            {
                var referencingSkipNavigations = foreignKey.GetReferencingSkipNavigations();

                foreach (var item in referencingSkipNavigations)
                {
                    if (item.ForeignKey == foreignKey && item.IsCollection == true)
                    {
                        yield return item.TargetEntityType;
                    }
                }

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

        private IEnumerable<IEntityType> GetEntitiesReferencingTypeInDependentOnEntityAttribute(Type typeInAttribute)
        {
            var typesWithAttr = _typeEntity.Where(x => x.Key.GetCustomAttribute<DependentOnEntity>()?.DependentEntities.Contains(typeInAttribute) == true).Select(x => x.Value);

            return typesWithAttr;
        }

        public bool HasLazyLoad(Type rootType)
        {
            var key = rootType.FullName!;

            if (_hasLazyLoadCache.TryGetValue(key, out var value))
            {
                return value;
            }

            bool result;

            if (_typeEntity.TryGetValue(rootType, out var entityType))
            {
                result = HasLazyLoad(entityType);
            }
            else
            {
                var entities = GetAllEntitiesByType(GetAllTypes(rootType));

                result = entities.Any(HasLazyLoad);
            }

            _hasLazyLoadCache[key] = result;

            return result;
        }

        public bool HasLazyLoad(IEntityType entityType)
        {
            var key = entityType.Name;
            if (_hasLazyLoadCache.TryGetValue(key, out var value))
            {
                return value;
            }

            var anyHasLazyLoading = entityType.GetNavigations().Any(navigation => navigation.LazyLoadingEnabled);

            _hasLazyLoadCache[key] = anyHasLazyLoading;

            return anyHasLazyLoading;
        }
    }
}