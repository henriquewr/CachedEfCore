using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CachedEfCore.KeyGeneration
{
    public class PrintabilityChecker : IPrintabilityChecker
    {
        private readonly HashSet<Type> _printableTypes = new HashSet<Type>
        {
            typeof(bool), typeof(bool?),
            typeof(byte), typeof(byte?),
            typeof(sbyte), typeof(sbyte?),

            typeof(short), typeof(short?),
            typeof(ushort), typeof(ushort?),

            typeof(char), typeof(char?),

            typeof(int), typeof(int?),
            typeof(uint), typeof(uint?),
            typeof(nint), typeof(nint?),

            typeof(nuint), typeof(nuint?),

            typeof(long), typeof(long?),
            typeof(ulong), typeof(ulong?),

            typeof(float), typeof(float?),
            typeof(double), typeof(double?),
            typeof(decimal), typeof(decimal?),

            typeof(DateTime), typeof(DateTime?),
            typeof(TimeSpan), typeof(TimeSpan?),
            typeof(Guid), typeof(Guid?),

            /*
                * typeof(string?) does not exists
                * string? nullableString = "";
                * nullableString.GetType() == typeof(string)
                * the expression returns true
                * both types are System.String
                */
            typeof(string),
        };

        public PrintabilityChecker()
        {
        }

        public PrintabilityChecker(IEnumerable<Type> printableTypes)
        {
            _printableTypes.UnionWith(printableTypes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsPrintable(object? value)
        {
            return IsPrintable(value, value?.GetType()!);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsPrintable(object? value, Type type)
        {
            if (value == null)
            {
                // null is always printable regardless of type
                return true;
            }

            return IsTypePrintable(type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsTypePrintable(Type type)
        {
            //Enums are always printable
            return type.IsEnum || _printableTypes.Contains(type);
        }
    }
}