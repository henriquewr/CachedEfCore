using System;

namespace CachedEfCore.Cache.KeyGeneration.TypeCompatibility
{
    public interface ITypeCompatibilityChecker
    {
        bool IsCompatible(Type type);
    }
}
