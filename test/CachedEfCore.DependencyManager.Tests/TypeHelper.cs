using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CachedEfCore.DependencyManager.Tests
{
    internal static class TypeHelper
    {
        public static class AnonymousType
        {
            public static Type Create<T>() => new { A = default(T) }.GetType();

            public static Type Create(Type type)
            {
                var method = typeof(AnonymousType)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Single(m =>
                        m.IsGenericMethodDefinition &&
                        m.ReturnType == typeof(Type));

                return (Type)method
                    .MakeGenericMethod(type)
                    .Invoke(null, null)!;
            }

            public static Type CreateGeneric(Type type)
            {
                var enumerableType = typeof(IEnumerable<>).MakeGenericType(type);
                return AnonymousType.Create(enumerableType);
            }
        }

        public static class Tuple
        {
            public static Type Create<T>() => (first: default(T), sec: default(T)).GetType();

            public static Type Create(Type type)
            {
                var method = typeof(Tuple)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Single(m =>
                        m.IsGenericMethodDefinition &&
                        m.ReturnType == typeof(Type));

                return (Type)method
                   .MakeGenericMethod(type)
                   .Invoke(null, null)!;
            }

            public static Type CreateGeneric(Type type)
            {
                var enumerableType = typeof(IEnumerable<>).MakeGenericType(type);
                return Tuple.Create(enumerableType);
            }
        }
    }
}
