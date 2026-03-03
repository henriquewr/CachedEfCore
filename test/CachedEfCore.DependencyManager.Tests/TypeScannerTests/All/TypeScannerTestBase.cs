using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.All
{
    public class TypeScannerTestBase
    {
        protected interface ITypeScannerTestType
        {
            static abstract ImmutableHashSet<Type> Types { get; }
            static abstract Type TestType { get; }
        }

        protected class PointerTestClass : ITypeScannerTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(int*);

            static PointerTestClass()
            {
                var references = TypeScanner.GetAllReferencedTypes(typeof(int)).ToHashSet();
                references.UnionWith(TypeScanner.GetAllReferencedTypes(typeof(int*)));

                Types = references.ToImmutableHashSet();
            }
        }

        protected class ArrayTestClass : ITypeScannerTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(int[]);

            static ArrayTestClass()
            {
                var references = TypeScanner.GetAllReferencedTypes(typeof(int)).ToHashSet();
                references.UnionWith(TypeScanner.GetAllReferencedTypes(typeof(int[])));

                Types = references.ToImmutableHashSet();
            }
        }

        protected class FieldTestClass : ITypeScannerTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(Field.TestClass);

            static FieldTestClass()
            {
                var references = TypeScanner.GetAllReferencedTypes(typeof(int)).ToHashSet();
                AddEmptyClassRef(references);

                references.Add(typeof(Field.InternalClass));
                references.Add(typeof(Field.TestClass));

                Types = references.ToImmutableHashSet();
            }
        }

        protected class PropertyTestClass : ITypeScannerTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(Property.TestClass);

            static PropertyTestClass()
            {
                var references = TypeScanner.GetAllReferencedTypes(typeof(int)).ToHashSet();
                AddEmptyClassRef(references);

                references.Add(typeof(Property.InternalClass));
                references.Add(typeof(Property.TestClass));

                Types = references.ToImmutableHashSet();
            }
        }

        protected class EnumTestClass : ITypeScannerTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(Enum.TestEnum);

            static EnumTestClass()
            {
                var references = TypeScanner.GetAllReferencedTypes(typeof(byte)).ToHashSet();
                references.Add(TestType);

                Types = references.ToImmutableHashSet();
            }
        }

        protected class InheritanceTestClass : ITypeScannerTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(Inheritance.TestClass);

            static InheritanceTestClass()
            {
                var references = TypeScanner.GetAllReferencedTypes(typeof(int)).ToHashSet();
                AddEmptyClassRef(references);

                references.Add(typeof(Inheritance.TestClass));
                references.Add(typeof(Inheritance.InternalClass));
                references.Add(typeof(Inheritance.OtherClass));

                Types = references.ToImmutableHashSet();
            }
        }

        protected class AbstractClassTestClass : ITypeScannerTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(Abstract.TestClass);

            static AbstractClassTestClass()
            {
                var references = TypeScanner.GetAllReferencedTypes(typeof(int)).ToHashSet();
                AddEmptyClassRef(references);

                references.Add(typeof(Abstract.TestClass));
                references.Add(typeof(Abstract.InternalClass));
                references.Add(typeof(Abstract.OtherClass));

                Types = references.ToImmutableHashSet();
            }
        }

        protected class InterfaceTestClass : ITypeScannerTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(Interface.TestClass);

            static InterfaceTestClass()
            {
                var references = TypeScanner.GetAllReferencedTypes(typeof(int)).ToHashSet();
                references.UnionWith(TypeScanner.GetAllReferencedTypes(typeof(long)));
                AddEmptyClassRef(references);

                references.Add(typeof(Interface.TestClass));
                references.Add(typeof(Interface.IInterface));
                references.Add(typeof(Interface.IInterface2));
                references.Add(typeof(Interface.IInterface3));

                Types = references.ToImmutableHashSet();
            }
        }

        protected class GenericTestClass : ITypeScannerTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(Generic.TestClass);

            static GenericTestClass()
            {
                var references = TypeScanner.GetAllReferencedTypes(typeof(int)).ToHashSet();
                references.UnionWith(TypeScanner.GetAllReferencedTypes(typeof(long)));

                AddEmptyClassRef(references);

                references.Add(typeof(Generic.InternalClass<long>));
                references.Add(typeof(Generic.InternalClass<>));
                references.Add(typeof(Generic.TestClass));

                Types = references.ToImmutableHashSet();
            }
        }

        protected class NestedGenericTestClass : ITypeScannerTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(NestedGeneric.TestClass);

            static NestedGenericTestClass()
            {
                var references = TypeScanner.GetAllReferencedTypes(typeof(int)).ToHashSet();
                references.UnionWith(TypeScanner.GetAllReferencedTypes(typeof(long)));

                AddEmptyClassRef(references);

                references.Add(typeof(NestedGeneric.InternalClass<NestedGeneric.InternalClass<long>>));
                references.Add(typeof(NestedGeneric.InternalClass<long>));
                references.Add(typeof(NestedGeneric.InternalClass<>));
                references.Add(typeof(NestedGeneric.TestClass));

                Types = references.ToImmutableHashSet();
            }
        }

        protected class AnonymousTypeTestClass : ITypeScannerTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType { get; }

            static AnonymousTypeTestClass()
            {
                var references = TypeScanner.GetAllReferencedTypes(typeof(int)).ToHashSet();

                references.Add(typeof(AnonymousType.InternalClass));
                references.Add(typeof(AnonymousType.TestClass));

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
                references.Add(testAnonymousType.A.GetType());
                references.Add(testAnonymousType.A.Nested.GetType());

                TestType = testAnonymousType.GetType();

                references.UnionWith(TypeScanner.GetAllReferencedTypes(TestType));

                Types = references.ToImmutableHashSet();
            }
        }

        protected class TupleLiteralTypeTestClass : ITypeScannerTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType { get; }

            static TupleLiteralTypeTestClass()
            {
                var references = TypeScanner.GetAllReferencedTypes(typeof(int)).ToHashSet();

                references.Add(typeof(TupleLiteral.TestClass));
                references.Add(typeof(TupleLiteral.InternalClass));
                references.Add(typeof(TupleLiteral.OutsideClass));
                references.Add(typeof(TupleLiteral.NotTooNestedClass));

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

                references.UnionWith(TypeScanner.GetAllReferencedTypes(TestType));

                references.Add(testTupleLiteral.A.GetType());
                references.Add(testTupleLiteral.A.Nested.GetType());

                Types = references.ToImmutableHashSet();
            }
        }

        protected class MethodTestClass : ITypeScannerTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(Method.TestClass);

            static MethodTestClass()
            {
                var references = TypeScanner.GetAllReferencedTypes(typeof(int)).ToHashSet();

                AddEmptyClassRef(references);

                references.Add(typeof(Method.InternalClass));
                references.Add(typeof(Method.TestClass));

                Types = references.ToImmutableHashSet();
            }
        }

        protected class NestedNoAccessTypeTestClass : ITypeScannerTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(NestedNoAccess.TestClass.NestedClass);

            static NestedNoAccessTypeTestClass()
            {
                var references = AddEmptyClassRef(new HashSet<Type>
                {
                    typeof(NestedNoAccess.TestClass.NestedClass)
                });

                Types = references.ToImmutableHashSet();
            }
        }

        protected class NestedAccessingTypeTestClass : ITypeScannerTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(NestedAccessing.TestClass.NestedClass);

            static NestedAccessingTypeTestClass()
            {
                var references = AddEmptyClassRef(new HashSet<Type>
                {
                    typeof(NestedAccessing.InternalClass),
                    typeof(NestedAccessing.TestClass.NestedClass)
                });

                Types = references.ToImmutableHashSet();
            }
        }

        protected class EvenMoreNestedNoAccessTypeTestClass : ITypeScannerTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(ReallyNestedNoAccess.TestClass.NestedClass.EvenMoreNestedClass);

            static EvenMoreNestedNoAccessTypeTestClass()
            {
                var references = AddEmptyClassRef(new HashSet<Type>
                {
                    typeof(ReallyNestedNoAccess.TestClass.NestedClass.EvenMoreNestedClass)
                });

                Types = references.ToImmutableHashSet();
            }
        }

        protected class EvenMoreNestedAccessingTypeTestClass : ITypeScannerTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(ReallyNestedAccessing.TestClass.NestedClass.EvenMoreNestedClass);

            static EvenMoreNestedAccessingTypeTestClass()
            {
                var references = AddEmptyClassRef(new HashSet<Type>
                {
                    typeof(ReallyNestedAccessing.InternalClass),
                    typeof(ReallyNestedAccessing.TestClass.NestedClass.EvenMoreNestedClass)
                });

                Types = references.ToImmutableHashSet();
            }
        }

        protected class NestedWithInstanceTypeTestClass : ITypeScannerTestType
        {
            public static ImmutableHashSet<Type> Types { get; }
            public static Type TestType => typeof(NestedWithInstance.TestClass.NestedClass);

            static NestedWithInstanceTypeTestClass()
            {
                var references = AddEmptyClassRef(new HashSet<Type>
                {
                    typeof(NestedWithInstance.TestClass.NestedClass)
                });

                Types = references.ToImmutableHashSet();
            }
        }

        private static HashSet<Type> AddEmptyClassRef(HashSet<Type> references)
        {
            references.UnionWith(TypeScanner.GetAllReferencedTypes(typeof(EmptyClass)));

            references.Remove(typeof(EmptyClass));

            return references;
        }
    }

    file class EmptyClass
    {
    }
}

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.All.Field
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

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.All.Property
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

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.All.Enum
{
    file enum TestEnum : byte
    {
        
    }
}

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.All.Inheritance
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

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.All.Abstract
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

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.All.Interface
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

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.All.Generic
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

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.All.NestedGeneric
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

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.All.AnonymousType
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

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.All.TupleLiteral
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

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.All.Method
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

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.All.NestedNoAccess
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

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.All.NestedAccessing
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

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.All.ReallyNestedNoAccess
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

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.All.ReallyNestedAccessing
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

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.All.NestedWithInstance
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
