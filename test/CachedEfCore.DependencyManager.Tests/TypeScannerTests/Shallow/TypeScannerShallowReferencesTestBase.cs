using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.Shallow
{
    public class TypeScannerShallowReferencesTestBase
    {
        protected interface ITypeScannerShallowTestType
        {
            static abstract ImmutableHashSet<Type> Types { get; }
            static abstract Type TestType { get; }
        }

        protected class PointerTestClass : ITypeScannerShallowTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(int*);

            static PointerTestClass()
            {
                var references = TypeScanner.GetShallowReferencedTypes(typeof(int*)).ToHashSet();
                references.Add(typeof(int));

                Types = references.ToImmutableHashSet();
            }
        }

        protected class ArrayTestClass : ITypeScannerShallowTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(int[]);

            static ArrayTestClass()
            {
                var references = TypeScanner.GetShallowReferencedTypes(typeof(int[])).ToHashSet();
                references.Add(typeof(int));

                Types = references.ToImmutableHashSet();
            }
        }

        protected class FieldTestClass : ITypeScannerShallowTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(Field.TestClass);

            static FieldTestClass()
            {
                var references = new HashSet<Type>
                {
                    typeof(Field.InternalClass)
                };

                AddEmptyClassRef(references);

                Types = references.ToImmutableHashSet();
            }
        }

        protected class PropertyTestClass : ITypeScannerShallowTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(Property.TestClass);

            static PropertyTestClass()
            {
                var references = new HashSet<Type>
                {
                    typeof(Property.InternalClass)
                };

                AddEmptyClassRef(references);

                Types = references.ToImmutableHashSet();
            }
        }

        protected class EnumTestClass : ITypeScannerShallowTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(Enum.TestEnum);

            static EnumTestClass()
            {
                var references = TypeScanner.GetShallowReferencedTypes(typeof(Enum.TestEnum)).ToHashSet();

                references.Add(typeof(byte));

                Types = references.ToImmutableHashSet();
            }
        }

        protected class InheritanceTestClass : ITypeScannerShallowTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(Inheritance.TestClass);

            static InheritanceTestClass()
            {
                var references = new HashSet<Type>
                {
                    typeof(Inheritance.InternalClass),
                };
                AddEmptyClassRef(references);

                Types = references.ToImmutableHashSet();
            }
        }

        protected class AbstractClassTestClass : ITypeScannerShallowTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(Abstract.TestClass);

            static AbstractClassTestClass()
            {
                var references = new HashSet<Type>
                {
                    typeof(Abstract.InternalClass),
                };
                AddEmptyClassRef(references);

                Types = references.ToImmutableHashSet();
            }
        }

        protected class InterfaceTestClass : ITypeScannerShallowTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(Interface.TestClass);

            static InterfaceTestClass()
            {
                var references = new HashSet<Type>
                {
                    typeof(Interface.IInterface),
                    typeof(Interface.IInterface2),
                    typeof(Interface.IInterface3),
                    typeof(long),
                    typeof(int),
                };
                AddEmptyClassRef(references);

                Types = references.ToImmutableHashSet();
            }
        }

        protected class GenericTestClass : ITypeScannerShallowTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(Generic.TestClass);

            static GenericTestClass()
            {
                var references = new HashSet<Type>
                {
                    typeof(Generic.InternalClass<long>),
                    typeof(Generic.InternalClass<>),
                    typeof(long),
                };

                AddEmptyClassRef(references);

                Types = references.ToImmutableHashSet();
            }
        }

        protected class NestedGenericTestClass : ITypeScannerShallowTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(NestedGeneric.TestClass);

            static NestedGenericTestClass()
            {
                var references = new HashSet<Type>
                {
                    typeof(NestedGeneric.InternalClass<NestedGeneric.InternalClass<long>>),
                    typeof(NestedGeneric.InternalClass<long>),
                    typeof(NestedGeneric.InternalClass<>),
                    typeof(long),
                };

                AddEmptyClassRef(references);

                Types = references.ToImmutableHashSet();
            }
        }
        protected class AnonymousTypeTestClass : ITypeScannerShallowTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType { get; }

            static AnonymousTypeTestClass()
            {
                var references = new HashSet<Type>();

                var testAnonymousType = new
                {
                    A = new
                    {
                        Val = (AnonymousType.InternalClass)default!,

                        Nested = new
                        {
                            NestedVal = (AnonymousType.TestClass)default!
                        }
                    }
                };
                TestType = testAnonymousType.GetType();

                references.Add(testAnonymousType.A.GetType());
                references.UnionWith(TypeScanner.GetShallowReferencedTypes(TestType));

                Types = references.ToImmutableHashSet();
            }
        }

        protected class TupleLiteralTypeTestClass : ITypeScannerShallowTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType { get; }

            static TupleLiteralTypeTestClass()
            {
                var references = new HashSet<Type>();

                var testTupleLiteral =
                (
                    A:
                    (
                        Val: (TupleLiteral.NotTooNestedClass)default!,

                        Nested:
                        (
                            NestedVal: (TupleLiteral.TestClass)default!,
                            NestedVal2: (TupleLiteral.InternalClass)default!
                        )
                    ),
                    B: (TupleLiteral.OutsideClass)default!
                );

                TestType = testTupleLiteral.GetType();

                references.Add(testTupleLiteral.A.GetType());
                references.UnionWith(TypeScanner.GetShallowReferencedTypes(TestType));

                Types = references.ToImmutableHashSet();
            }
        }

        protected class MethodTestClass : ITypeScannerShallowTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(Method.TestClass);

            static MethodTestClass()
            {
                var references = new HashSet<Type>()
                {
                    typeof(Method.InternalClass),
                };

                AddEmptyClassRef(references);

                Types = references.ToImmutableHashSet();
            }
        }

        protected class NestedNoAccessTypeTestClass : ITypeScannerShallowTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(NestedNoAccess.TestClass.NestedClass);

            static NestedNoAccessTypeTestClass()
            {
                var references = AddEmptyClassRef(new HashSet<Type>());

                Types = references.ToImmutableHashSet();
            }
        }

        protected class NestedAccessingTypeTestClass : ITypeScannerShallowTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(NestedAccessing.TestClass.NestedClass);

            static NestedAccessingTypeTestClass()
            {
                var references = AddEmptyClassRef(new HashSet<Type>
                {
                    typeof(NestedAccessing.InternalClass),
                });

                Types = references.ToImmutableHashSet();
            }
        }

        protected class EvenMoreNestedNoAccessTypeTestClass : ITypeScannerShallowTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(ReallyNestedNoAccess.TestClass.NestedClass.EvenMoreNestedClass);

            static EvenMoreNestedNoAccessTypeTestClass()
            {
                var references = AddEmptyClassRef(new HashSet<Type>());

                Types = references.ToImmutableHashSet();
            }
        }

        protected class EvenMoreNestedAccessingTypeTestClass : ITypeScannerShallowTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(ReallyNestedAccessing.TestClass.NestedClass.EvenMoreNestedClass);

            static EvenMoreNestedAccessingTypeTestClass()
            {
                var references = AddEmptyClassRef(new HashSet<Type>
                {
                    typeof(ReallyNestedAccessing.InternalClass),
                });

                Types = references.ToImmutableHashSet();
            }
        }

        protected class NestedWithInstanceTypeTestClass : ITypeScannerShallowTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(NestedWithInstance.TestClass.NestedClass);

            static NestedWithInstanceTypeTestClass()
            {
                var references = AddEmptyClassRef(new HashSet<Type>());

                Types = references.ToImmutableHashSet();
            }
        }

        private static HashSet<Type> AddEmptyClassRef(HashSet<Type> references)
        {
            references.UnionWith(TypeScanner.GetShallowReferencedTypes(typeof(EmptyClass)));

            references.Remove(typeof(EmptyClass));

            return references;
        }
    }
    file class EmptyClass
    {
    }
}


namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.Shallow.Field
{
    file class TestClass
    {
        private readonly InternalClass _value = null!;
    }

    file class InternalClass
    {
        private readonly int _value;
    }
}

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.Shallow.Property
{
    file class TestClass
    {
        private InternalClass Value { get; set; } = null!;
    }

    file class InternalClass
    {
        private int Value { get; set; }
    }
}

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.Shallow.Enum
{
    file enum TestEnum : byte
    {

    }
}

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.Shallow.Inheritance
{
    file class TestClass : InternalClass
    {
    }
    file class InternalClass : OtherClass
    {
    }
    file class OtherClass
    {
        private int Value { get; set; }
    }
}

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.Shallow.Abstract
{
    file class TestClass : InternalClass
    {
    }

    file abstract class InternalClass : OtherClass
    {
    }

    file abstract class OtherClass
    {
        private int Value { get; set; }
    }
}

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.Shallow.Interface
{
    file class TestClass : IInterface, IInterface2
    {
        public long Value { get; set; }
        public int Value2 { get; set; }
    }
    file interface IInterface
    {
        long Value { get; set; }
    }

    file interface IInterface2 : IInterface3
    {
        int Value2 { get; set; }
    }

    file interface IInterface3
    {
    }
}

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.Shallow.Generic
{
    file class TestClass
    {
        private InternalClass<long> Value { get; set; } = null!;
    }

    file class InternalClass<T>
    {
        private int Value { get; set; }
    }
}

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.Shallow.NestedGeneric
{
    file class TestClass
    {
        private InternalClass<InternalClass<long>> Value { get; set; } = null!;
    }

    file class InternalClass<T>
    {
        private int Value { get; set; }
    }
}

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.Shallow.AnonymousType
{
    file class TestClass
    {
        private InternalClass Value { get; set; } = null!;
    }

    file class InternalClass
    {
        private int Value { get; set; }
    }
}

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.Shallow.TupleLiteral
{
    file class TestClass
    {
        private InternalClass Value { get; set; } = null!;
    }

    file class InternalClass
    {
        private int Value { get; set; }
    }

    file class OutsideClass
    {
    }

    file class NotTooNestedClass
    {
    }
}

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.Shallow.Method
{
    file class TestClass
    {
        private InternalClass Method()
        {
            return default!;
        }
    }

    file class InternalClass
    {
        private int Value { get; set; }
    }
}

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.Shallow.NestedNoAccess
{
    file class TestClass
    {
        //The value is not accessed inside NestedClass
        private static readonly InternalClass _value = null!;

        public class NestedClass
        {

        }
    }

    file class InternalClass
    {
    }
}

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.Shallow.NestedAccessing
{
    file class TestClass
    {
        //The value is accessed inside NestedClass
        private static readonly InternalClass _value = null!;

        public class NestedClass
        {
            public void Access()
            {
                _value.DoSomething();
            }
        }
    }

    file class InternalClass
    {
        public void DoSomething()
        {

        }
    }
}

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.Shallow.ReallyNestedNoAccess
{
    file class TestClass
    {
        //The value is not accessed inside NestedClass or EvenMoreNestedClass
        private static readonly InternalClass _value = null!;

        public class NestedClass
        {
            public class EvenMoreNestedClass
            {
            }
        }
    }

    file class InternalClass
    {
    }
}

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.Shallow.ReallyNestedAccessing
{
    file class TestClass
    {
        //The value is accessed inside EvenMoreNestedClass
        private static readonly InternalClass _value = null!;

        public class NestedClass
        {
            public class EvenMoreNestedClass
            {
                public void Access()
                {
                    _value.DoSomething();
                }
            }
        }
    }

    file class InternalClass
    {
        public void DoSomething()
        {

        }
    }
}

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.Shallow.NestedWithInstance
{
    file class TestClass
    {
        //The value can NOT be accessed inside NestedClass
        private readonly InternalClass _value = null!;

        public class NestedClass
        {

        }
    }

    file class InternalClass
    {
    }
}
