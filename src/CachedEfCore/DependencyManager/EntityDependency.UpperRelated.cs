using CachedEfCore.DependencyManager.Attributes;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CachedEfCore.DependencyManager
{
    public partial class EntityDependency
    {
        private readonly ConcurrentDictionary<string, FrozenSet<IEntityType>> _upperRelatedCache = new();
        private readonly ConcurrentDictionary<Type, FrozenSet<IEntityType>> _allEntitiesUpperRelatedCache = new();

        public FrozenSet<IEntityType> GetUpperRelatedEntities(Type rootType)
        {
            if (TryGetIEntityType(rootType, out var fastEntityType))
            {
                return GetUpperRelatedEntitiesImpl(fastEntityType);
            }

            if (!_allEntitiesUpperRelatedCache.TryGetValue(rootType, out var underRelatedEntities))
            {
                var entities = GetAllEntitiesByType(GetShallowTypes(rootType));

                underRelatedEntities = GetEntities(entities, GetUpperRelatedEntitiesImpl);

                _allEntitiesUpperRelatedCache[rootType] = underRelatedEntities;
            }

            return underRelatedEntities;
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

                if (navigation is not null && navigation.ForeignKey == foreignKey)
                {
                    yield return foreignKey.PrincipalEntityType;
                }
            }

            /*
                public class Referenced
                {
                    public int Id { get; set; }
                }

                public class EntityReferencingForeignKey
                {
                    public int ReferencedId { get; set; }
                    public Referenced Referenced { get; set; }
                }

                ((IEntity)Referenced).GetReferencingForeignKeys(); => EntityReferencingForeignKey
            */
            var referencingForeignKeys = entityType.GetReferencingForeignKeys();
            foreach (var referencingForeignKey in referencingForeignKeys)
            {
                yield return referencingForeignKey.DeclaringEntityType;
            }
        }

        private IEnumerable<IEntityType> GetEntitiesReferencingTypeInDependentOnEntityAttribute(Type typeInAttribute)
        {
            var typesWithAttr = _typeEntity.Where(x => x.Key.GetCustomAttribute<DependentOnEntity>()?.DependentEntities.Contains(typeInAttribute) == true).Select(x => x.Value);

            return typesWithAttr;
        }
    }
}
