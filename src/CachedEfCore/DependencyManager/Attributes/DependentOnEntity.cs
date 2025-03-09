using System;
using System.Collections.Immutable;

namespace CachedEfCore.DependencyManager.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class DependentOnEntity : Attribute
    {
        public readonly ImmutableHashSet<Type> DependentEntities;

        public DependentOnEntity(params Type[] dependentEntities)
        {
            DependentEntities = dependentEntities.ToImmutableHashSet();
        }
    }
}