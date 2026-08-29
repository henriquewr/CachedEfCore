using System;

namespace CachedEfCore.KeyGeneration.TypeCompatibility
{
    public interface ITypeCompatibilityChecker
    {
        bool IsCompatible(Type type);
    }
}
