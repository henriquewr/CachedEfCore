using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CachedEfCore.DependencyManager.Tests.TypeScannerTests.Shallow
{
    public class TypeScannerShallowReferencesTest : TypeScannerShallowReferencesTestBase
    {
        private static void Test<T>()
            where T : ITypeScannerShallowTestType
        {
            var shallowTypes = TypeScanner.GetShallowReferencedTypes(T.TestType).ToHashSet();

            if (!shallowTypes.SetEquals(T.Types))
            {
                var missing = T.Types.Except(shallowTypes);
                var unexpected = shallowTypes.Except(T.Types);

                Assert.Fail(@$"Missing: {Format(missing)}
                    Unexpected: {Format(unexpected)}"
                );
            }

            static string Format(IEnumerable<Type> set)
            {
                return string.Join(", ", set.Select(t => t.Name));
            }
        }

        [Fact]
        public void TypeScanner_Should_See_Pointer()
        {
            Test<PointerTestClass>();
        }

        [Fact]
        public void TypeScanner_Should_See_Array()
        {
            Test<ArrayTestClass>();
        }

        [Fact]
        public void TypeScanner_Should_See_Fields()
        {
            Test<FieldTestClass>();
        }

        [Fact]
        public void TypeScanner_Should_See_Properties()
        {
            Test<PropertyTestClass>();
        }

        [Fact]
        public void TypeScanner_Should_See_Enum()
        {
            Test<EnumTestClass>();
        }

        [Fact]
        public void TypeScanner_Should_See_Inheritance()
        {
            Test<InheritanceTestClass>();
        }

        [Fact]
        public void TypeScanner_Should_See_AbstractClasses()
        {
            Test<AbstractClassTestClass>();
        }

        [Fact]
        public void TypeScanner_Should_See_Interfaces()
        {
            Test<InterfaceTestClass>();
        }

        [Fact]
        public void TypeScanner_Should_See_Generic_Arguments()
        {
            Test<GenericTestClass>();
        }

        [Fact]
        public void TypeScanner_Should_See_Nested_Generic_Arguments()
        {
            Test<NestedGenericTestClass>();
        }

        [Fact]
        public void TypeScanner_Should_See_AnonymoustType()
        {
            Test<AnonymousTypeTestClass>();
        }

        [Fact]
        public void TypeScanner_Should_See_TupleLiteral()
        {
            Test<TupleLiteralTypeTestClass>();
        }

        [Fact]
        public void TypeScanner_Should_See_Method()
        {
            Test<MethodTestClass>();
        }

        [Fact]
        public void TypeScanner_Should_See_Nested_No_Access()
        {
            Test<NestedNoAccessTypeTestClass>();
        }

        // See comments on TypeScanner
        //[Fact]
        //public void TypeScanner_Should_See_Nested_Accessing()
        //{
        //    Test<NestedAccessingTypeTestClass>();
        //}

        [Fact]
        public void TypeScanner_Should_See_Nested_Independent_Of_Depth_No_Access()
        {
            Test<EvenMoreNestedNoAccessTypeTestClass>();
        }

        // See comments on TypeScanner
        //[Fact]
        //public void TypeScanner_Should_See_Nested_Independent_Of_Depth_Accessing()
        //{
        //    Test<EvenMoreNestedAccessingTypeTestClass>();
        //}

        [Fact]
        public void TypeScanner_Should_Not_See_Nested_With_Instance()
        {
            Test<NestedWithInstanceTypeTestClass>();
        }
    }
}
