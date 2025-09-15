using System;

namespace CachedEfCore.KeyGeneration.EvalTypeChecker
{
    public interface ITypeCompatibilityChecker
    {
        bool IsCompatible(Type type);
    }
}
