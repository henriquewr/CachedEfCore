using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace CachedEfCore.DependencyManager
{
    public partial class EntityDependency
    {
        private readonly ConcurrentDictionary<string, bool> _hasLazyLoadCache = new();

        public bool HasLazyLoad(Type rootType)
        {
            var key = rootType.FullName!;

            if (_hasLazyLoadCache.TryGetValue(key, out var value))
            {
                return value;
            }

            bool result;

            if (TryGetIEntityType(rootType, out var fastEntityType))
            {
                result = HasLazyLoad(fastEntityType);
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
