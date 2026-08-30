using CachedEfCore.Context;
using CachedEfCore.DependencyInjection;
using CachedEfCore.KeyGeneration.ExpressionKeyGen;
using CachedEfCore.SqlServer.Configuration;
using CachedEfCore.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;

namespace CachedEfCore.KeyGeneration.Tests
{
    public class KeyGeneratorVisitorTests : IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;
        public KeyGeneratorVisitorTests(ServiceProviderFixture serviceProviderFixture)
        {
            _serviceProviderFixture = serviceProviderFixture;
        }

        protected virtual IServiceProvider CreateProvider(params IEnumerable<Type> nonEvaluableTypes)
           => _serviceProviderFixture.CreateProvider(services =>
           {
               services.AddDbContext<CachedDbContext>((serviceProvider, options) =>
               {
                    options.UseSqlServer();
                    options.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

                    options.UseCachedEfCore(cachedEfCoreOptions =>
                    {
                        cachedEfCoreOptions.UseSqlServer();

                        cachedEfCoreOptions.ConfigureKeyGeneration(keyGen =>
                        {
                            keyGen.ConfigureNonEvaluableTypes(configuration =>
                            {
                                configuration.Clear();
                                configuration.AddRange(nonEvaluableTypes);

                                return configuration;
                            });
                        });
                    });
               });
           });

        private KeyGeneratorVisitor CreateVisitor(params IEnumerable<Type> nonEvaluableTypes)
        {
            var provider = CreateProvider(nonEvaluableTypes).CreateScope().ServiceProvider;

            var dbContext = provider.GetRequiredService<CachedDbContext>();

            var keyGenerator = dbContext.GetService<KeyGeneratorVisitor>();

            return keyGenerator;
        }

        private class TestClass
        {
            public string? Test;
            public int TestVal;
        }

        private struct TestStruct
        {
            public string Test;
            public int TestVal;
        }

        private static List<TestClass> GetDefaultList()
        {
            var list = new List<TestClass>
            {
                new TestClass { Test = "First", TestVal = 1 },
                new TestClass { Test = "Second", TestVal = 2 },
                new TestClass { Test = "Third", TestVal = 3 }
            };
            return list;
        }

        private static Dictionary<int, string> GetDefaultDict()
        {
            var dict = new Dictionary<int, string>
            {
                { 1, "First" },
                { 2, "Second" },
                { 3, "Third" },
            };
            return dict;
        }

        [Fact]
        public void Test_Variable_Evaluation()
        {
            var keyGeneratorVisitor = CreateVisitor([]);

            var variable = 1;

            Expression<Func<TestClass, bool>> expression = x => variable == x.TestVal;
            var result1 = keyGeneratorVisitor.ExpressionToString(expression);
            var variable2 = 1;

            Expression<Func<TestClass, bool>> expression2 = x => variable2 == x.TestVal;
            var result2 = keyGeneratorVisitor.ExpressionToString(expression2);
            var notTheSameVariable = 2;

            Expression<Func<TestClass, bool>> expression3 = x => notTheSameVariable == x.TestVal;
            var result3 = keyGeneratorVisitor.ExpressionToString(expression3);

            Assert.Equal(result1, result2);

            Assert.NotEqual(result1, result3);
        }

        [Fact]
        public void Test_Function_Evaluation_With_Printable_Values()
        {
            var keyGeneratorVisitor = CreateVisitor([]);

            var list1 = GetDefaultList();
            var variable1 = 1;

            Expression<Func<TestClass, bool>> expression = x => list1.First(l => l.TestVal == variable1).TestVal == x.TestVal;
            var result1 = keyGeneratorVisitor.ExpressionToString(expression);

            var list2 = GetDefaultList();
            var variable2 = 1;
            Expression<Func<TestClass, bool>> expression2 = x => list2.First(l => l.TestVal == variable2).TestVal == x.TestVal;
            var result2 = keyGeneratorVisitor.ExpressionToString(expression2);

            var list3 = GetDefaultList();
            var variable3 = 2;
            Expression<Func<TestClass, bool>> expression3 = x => list2.First(l => l.TestVal == variable3).TestVal == x.TestVal;
            var result3 = keyGeneratorVisitor.ExpressionToString(expression3);

            Assert.Equal(result1, result2);

            Assert.NotEqual(result1, result3);
        }

        [Fact]
        public void Test_Function_Evaluation_With_Non_Printable_Values()
        {
            var keyGeneratorVisitor = CreateVisitor([]);

            var list1 = GetDefaultList();
            var variable1 = 1;

            Expression<Func<TestClass, bool>> expression = x => list1.First(l => l.TestVal == variable1) == null;
            var result1 = keyGeneratorVisitor.ExpressionToString(expression);

            var list2 = GetDefaultList();
            var variable2 = 1;
            Expression<Func<TestClass, bool>> expression2 = x => list2.First(l => l.TestVal == variable2) == null;
            var result2 = keyGeneratorVisitor.ExpressionToString(expression2);

            var list3 = GetDefaultList();
            var variable3 = 2;
            Expression<Func<TestClass, bool>> expression3 = x => list2.First(l => l.TestVal == variable3) == null;
            var result3 = keyGeneratorVisitor.ExpressionToString(expression3);

            Assert.Equal(result1, result2);

            Assert.NotEqual(result1, result3);
        }

        [Fact]
        public void Test_Enumerable_Printer_With_Printable_Values()
        {
            var keyGeneratorVisitor = CreateVisitor([]);

            var list1 = GetDefaultList();
            var listWithPrintableValues = list1.Select(x => x.TestVal).ToList();

            Expression<Func<TestClass, bool>> expression = x => listWithPrintableValues.Contains(x.TestVal);
            var result1 = keyGeneratorVisitor.ExpressionToString(expression);


            var list2 = GetDefaultList();
            Expression<Func<TestClass, bool>> expression2 = x => list1.Select(l => l.TestVal).ToList().Contains(x.TestVal);
            var result2 = keyGeneratorVisitor.ExpressionToString(expression2);


            var list3 = GetDefaultList();
            list3.Add(new TestClass { TestVal = 1234, Test = "different" });
            var listWithPrintableValues3 = list3.Select(x => x.TestVal).ToList();

            Expression<Func<TestClass, bool>> expression3 = x => listWithPrintableValues3.Contains(x.TestVal);
            var result3 = keyGeneratorVisitor.ExpressionToString(expression3);

            Assert.Equal(result1, result2);

            Assert.NotEqual(result1, result3);
        }

        [Fact]
        public void Test_Enumerable_Printer_With_Non_Printable_Values()
        {
            var keyGeneratorVisitor = CreateVisitor([]);

            var list1 = GetDefaultList();
            Expression<Func<TestClass, bool>> expression = x => list1.Contains(x);
            var result1 = keyGeneratorVisitor.ExpressionToString(expression);

            var list2 = GetDefaultList();
            Expression<Func<TestClass, bool>> expression2 = x => list2.Contains(x);
            var result2 = keyGeneratorVisitor.ExpressionToString(expression2);

            var list3 = GetDefaultList();
            list3.Add(new TestClass { TestVal = 1234, Test = "different" });
            Expression<Func<TestClass, bool>> expression3 = x => list3.Contains(x);
            var result3 = keyGeneratorVisitor.ExpressionToString(expression3);

            Assert.Equal(result1, result2);

            Assert.NotEqual(result1, result3);
        }

        [Fact]
        public void Test_Enumerable_Printer_With_Printable_Null_Values()
        {
            var keyGeneratorVisitor = CreateVisitor([]);

            var list1 = GetDefaultList().Select(x => (TestClass)null!).ToList();
            Expression<Func<TestClass, bool>> expression = x => list1.Contains(x);
            var result1 = keyGeneratorVisitor.ExpressionToString(expression);


            var list2 = GetDefaultList();
            Expression<Func<TestClass, bool>> expression2 = x => list2.Select(l => (TestClass)null!).ToList().Contains(x);
            var result2 = keyGeneratorVisitor.ExpressionToString(expression2);


            var list3 = GetDefaultList().Select(x => (TestClass)null!).ToList();
            list3.Add(null!);
            list3.Add(null!);
            Expression<Func<TestClass, bool>> expression3 = x => list3.Contains(x);
            var result3 = keyGeneratorVisitor.ExpressionToString(expression3);

            Assert.Equal(result1, result2);

            Assert.NotEqual(result1, result3);
        }

        [Fact]
        public void Test_Dictionary_Printer_With_Printable_Values()
        {
            var keyGeneratorVisitor = CreateVisitor([]);

            var dict1 = GetDefaultDict();
            Expression<Func<TestClass, bool>> expression = x => dict1.ContainsKey(x.TestVal);
            var result1 = keyGeneratorVisitor.ExpressionToString(expression);

            var dict2 = GetDefaultDict();
            Expression<Func<TestClass, bool>> expression2 = x => dict2.ContainsKey(x.TestVal);
            var result2 = keyGeneratorVisitor.ExpressionToString(expression2);

            var dict3 = GetDefaultDict();
            dict3.Add(1234, "different");

            Expression<Func<TestClass, bool>> expression3 = x => dict3.ContainsKey(x.TestVal);
            var result3 = keyGeneratorVisitor.ExpressionToString(expression3);

            Assert.Equal(result1, result2);

            Assert.NotEqual(result1, result3);
        }

        private interface INonEvaluable;
        public class NonEvaluableTestClass : INonEvaluable
        {
            public bool Evaluated { get; set; } = false;

            public int AnyMethod()
            {
                Evaluated = true;
                return 1;
            }

            public int Anything 
            { 
                get 
                {
                    Evaluated = true;
                    return 2;
                }
            }
        }

        public static TheoryData<Expression, Type[], NonEvaluableTestClass> GetNonEvaluableTypesTestCases()
        {
            var variable = new NonEvaluableTestClass();
            Expression<Func<int>> test1 = () => variable.AnyMethod();
            Expression<Func<int>> test2 = () => variable.Anything;
            Expression<Func<int>> test3 = () => variable.Anything + variable.Anything;
            return new()
            {
                {
                    test1,
                    [typeof(INonEvaluable)],
                    variable
                },
                {
                    test1,
                    [typeof(NonEvaluableTestClass)],
                    variable
                },

                {
                    test2,
                    [typeof(INonEvaluable)],
                    variable
                },
                {
                    test2,
                    [typeof(NonEvaluableTestClass)],
                    variable
                },

                {
                    test3,
                    [typeof(INonEvaluable)],
                    variable
                },
                {
                    test3,
                    [typeof(NonEvaluableTestClass)],
                    variable
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetNonEvaluableTypesTestCases))]
        public void KeyGeneratorVisitor_Should_Not_Eval_Non_Evaluable_Types(Expression expression, Type[] nonEvalutableTypes, NonEvaluableTestClass instance)
        {
            instance.Evaluated = false;

            var keyGeneratorVisitor = CreateVisitor(nonEvalutableTypes);

            var result = keyGeneratorVisitor.ExpressionToString(expression);

            Assert.False(instance.Evaluated);
        }

        [Fact]
        public void Test_KeyGenerator_Is_Thread_Safe()
        {
            var keyGeneratorVisitor = CreateVisitor([]);

            var nonPrintableType = new TestClass();
            var nonPrintableType2 = new TestClass
            {
                Test = "",
                TestVal = 1
            };

            Expression<Func<TestClass, bool>> expression1 = x => nonPrintableType == null;
            Expression<Func<TestClass, bool>> expression2 = x => 1 == 2;

            Expression<Func<TestClass, bool>> expression3 = x => nonPrintableType2 == null;
            Expression<Func<TestClass, bool>> expression4 = x => "1" == null;

            var expressions = new List<(Expression, KeyGeneratorResult<string>?)>
            {
                (expression1, keyGeneratorVisitor.SafeExpressionToString(expression1)),
                (expression2, keyGeneratorVisitor.SafeExpressionToString(expression2)),
                (expression3, keyGeneratorVisitor.SafeExpressionToString(expression3)),
                (expression4, keyGeneratorVisitor.SafeExpressionToString(expression4)),
            };

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 10
            };
      
            Parallel.For(0, 5000, parallelOptions, i =>
            {
                Parallel.ForEach(expressions, parallelOptions, x =>
                {
                    var keyStr = keyGeneratorVisitor.SafeExpressionToString(x.Item1);

                    Assert.Equal(x.Item2, keyStr);
                });
            });
        }
    }
}