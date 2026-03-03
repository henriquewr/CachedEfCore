using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CachedEfCore.DependencyManager
{
    public class TypeScanner
    {
        /// <summary>
        /// Get all references that can escape from the type, including itself.
        /// Calling this method can be really slow, cache the result
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static HashSet<Type> GetAllReferencedTypes(Type type)
        {
            var result = GetAllReferencedTypesImpl(type);

            return result;
        }

        private static HashSet<Type> GetAllReferencedTypesImpl(Type type)
        {
            if (type is null)
            {
                return [];
            }

            var visited = new HashSet<Type>();
            var stack = new Stack<Type>();

            stack.Push(type);

            while (stack.Count > 0)
            {
                var currentType = stack.Pop();

                if (!visited.Add(currentType))
                {
                    continue;
                }

                var references = GetShallowReferencedTypes(currentType);

                foreach (var reference in references)
                {
                    stack.Push(reference);
                }
            }

            return visited;
        }

        const BindingFlags _allFlags = BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.Static;

        /// <summary>
        /// Get shallow references that can escape from the type, not including itself.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static HashSet<Type> GetShallowReferencedTypes(Type type, BindingFlags bindingFlags = _allFlags)
        {
            var references = GetShallowReferencedTypesImpl(type, bindingFlags).ToHashSet();

            var referencesWithGenericArgs = references.SelectMany(GetGenericArgumentsRecursiveImpl).ToHashSet();

            references.UnionWith(referencesWithGenericArgs);

            return references;

            static IEnumerable<Type> GetShallowReferencedTypesImpl(Type type, BindingFlags bindingFlags)
            {
                if (type.BaseType is not null && type.BaseType != typeof(object))
                {
                    yield return type.BaseType;
                }

                foreach (var @interface in type.GetInterfaces())
                {
                    yield return @interface;
                }

                if (type.HasElementType)
                {
                    yield return type.GetElementType()!;
                }

                if (type.IsEnum)
                {
                    yield return Enum.GetUnderlyingType(type);
                }

                if (type.IsGenericType)
                {
                    yield return type.GetGenericTypeDefinition();

                    foreach (var genericArg in type.GetGenericArguments())
                    {
                        if (!genericArg.IsGenericParameter)
                        {
                            yield return genericArg;
                        }
                    }
                }

                //if (type.DeclaringType is not null)
                //{
                //    that's difficult to do, but the idea is to somehow analize the class and see if the nested class is referencing some static thing from the parent class
                //    var shallowNonInstanceTypes = GetShallowTypes(type.DeclaringType, _allFlags & ~BindingFlags.Instance);

                //    foreach (var item in shallowNonInstanceTypes)
                //    {
                //         yield return item;
                //    }
                //}

                var fields = type.GetFields(bindingFlags);
                foreach (var field in fields)
                {
                    yield return field.FieldType;
                }

                var properties = type.GetProperties(bindingFlags);
                foreach (var property in properties)
                {
                    yield return property.PropertyType;
                }

                var methods = type.GetMethods(bindingFlags);
                foreach (var method in methods)
                {
                    yield return method.ReturnType;
                }
            }

            static HashSet<Type> GetGenericArgumentsRecursiveImpl(Type type)
            {
                var visited = new HashSet<Type>();
                var stack = new Stack<Type>();

                stack.Push(type);

                while (stack.Count > 0)
                {
                    var currentType = stack.Pop();

                    if (!visited.Add(currentType))
                    {
                        continue;
                    }

                    var references = GetGenericArguments(currentType);

                    foreach (var reference in references)
                    {
                        stack.Push(reference);
                    }
                }

                return visited;

                static IEnumerable<Type> GetGenericArguments(Type type)
                {
                    if (!type.IsGenericType)
                    {
                        yield break;
                    }

                    yield return type.GetGenericTypeDefinition();

                    foreach (var genericArg in type.GetGenericArguments())
                    {
                        if (!genericArg.IsGenericParameter)
                        {
                            yield return genericArg;
                        }
                    }
                }
            }
        }
    }
}
