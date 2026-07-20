using System;
using System.Collections.Immutable;

namespace CachedEfCore.DependencyManager.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class DependentOnEntityAttribute : Attribute
    {
        public ImmutableHashSet<Type> DependentEntities { get; }

        public DependentOnEntityAttribute(params Type[] dependentEntities)
        {
            DependentEntities = dependentEntities.ToImmutableHashSet();
        }
    }
}