using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CachedEfCore.KeyGeneration.EvalTypeChecker
{
    public class TypeCompatibilityChecker : ITypeCompatibilityChecker
    {
        public readonly ImmutableArray<Type> CompatibleTypes;
        private readonly ConcurrentDictionary<Type, bool> _assignalableToAnyType;

        public TypeCompatibilityChecker(IEnumerable<Type> types)
        {
            CompatibleTypes = types.ToImmutableArray();
            _assignalableToAnyType = new();
        }

        public bool IsCompatible(Type type)
        {
            bool isCompatible;

            if (_assignalableToAnyType.TryGetValue(type, out isCompatible))
            {
                return isCompatible;
            }

            isCompatible = CompatibleTypes.Any(item =>
                IsTypeCompatible(type, item)
            );

            _assignalableToAnyType[type] = isCompatible;
            return isCompatible;
        }

        private static bool IsTypeCompatible(Type type, Type to)
        {
            if (type == to)
            {
                return true;
            }

            if (to.IsGenericTypeDefinition)
            {
                if (type.IsGenericTypeDefinition)
                {
                    return IsOpenGenericAssignableTo(type, to);
                }

                return ImplementsOpenGeneric(type, to);
            }

            return type.IsAssignableTo(to);
        }

        private static bool IsOpenGenericAssignableTo(Type from, Type to)
        {
            if (from == to)
            {
                return true;
            }

            foreach (var it in from.GetInterfaces())
            {
                if (it.IsGenericType && it.GetGenericTypeDefinition() == to)
                {
                    return true;
                }
            }

            var baseType = from.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == to)
                {
                    return true;
                }

                baseType = baseType.BaseType;
            }

            return false;
        }

        private static bool ImplementsOpenGeneric(Type from, Type openGeneric)
        {
            if (from.IsGenericType && from.GetGenericTypeDefinition() == openGeneric)
            {
                return true;
            }

            return IsOpenGenericAssignableTo(from, openGeneric);
        }
    }
}