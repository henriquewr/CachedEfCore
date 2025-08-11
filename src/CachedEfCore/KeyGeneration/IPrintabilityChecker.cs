using System;

namespace CachedEfCore.KeyGeneration
{
    public interface IPrintabilityChecker
    {
        bool IsPrintable(object? value);
        bool IsPrintable(object? value, Type type);
        bool IsTypePrintable(Type type);
    }
}
