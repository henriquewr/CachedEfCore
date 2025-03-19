using CachedEfCore.ExpressionKeyGen;
using System.Linq.Expressions;

namespace CachedEfCore.KeyGeneration.Tests
{
    public class Tests
    {
        private readonly KeyGeneratorVisitor _keyGeneratorVisitor = new();

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

        [SetUp]
        public void Test_Variable_Evaluation()
        {
            var variable = 1;

            Expression<Func<TestClass, bool>> expression = x => variable == x.TestVal;
            var result1 = _keyGeneratorVisitor.ExpressionToString(expression);
            var variable2 = 1;

            Expression<Func<TestClass, bool>> expression2 = x => variable2 == x.TestVal;
            var result2 = _keyGeneratorVisitor.ExpressionToString(expression2);
            var notTheSameVariable = 2;

            Expression<Func<TestClass, bool>> expression3 = x => notTheSameVariable == x.TestVal;
            var result3 = _keyGeneratorVisitor.ExpressionToString(expression3);

            Assert.That(result1, Is.EqualTo(result2));

            Assert.That(result1, Is.Not.EqualTo(result3));
        }

        [Test]
        public void Test_Function_Evaluation_With_Printable_Values()
        {
            var list1 = GetDefaultList();
            var variable1 = 1;

            Expression<Func<TestClass, bool>> expression = x => list1.First(l => l.TestVal == variable1).TestVal == x.TestVal;
            var result1 = _keyGeneratorVisitor.ExpressionToString(expression);

            var list2 = GetDefaultList();
            var variable2 = 1;
            Expression<Func<TestClass, bool>> expression2 = x => list2.First(l => l.TestVal == variable2).TestVal == x.TestVal;
            var result2 = _keyGeneratorVisitor.ExpressionToString(expression2);

            var list3 = GetDefaultList();
            var variable3 = 2;
            Expression<Func<TestClass, bool>> expression3 = x => list2.First(l => l.TestVal == variable3).TestVal == x.TestVal;
            var result3 = _keyGeneratorVisitor.ExpressionToString(expression3);

            Assert.That(result1, Is.EqualTo(result2));

            Assert.That(result1, Is.Not.EqualTo(result3));
        }

        [Test]
        public void Test_Function_Evaluation_With_Non_Printable_Values()
        {
            var list1 = GetDefaultList();
            var variable1 = 1;

            Expression<Func<TestClass, bool>> expression = x => list1.First(l => l.TestVal == variable1) == null;
            var result1 = _keyGeneratorVisitor.ExpressionToString(expression);

            var list2 = GetDefaultList();
            var variable2 = 1;

            Expression<Func<TestClass, bool>> expression2 = x => list2.First(l => l.TestVal == variable2) == null;
            var result2 = _keyGeneratorVisitor.ExpressionToString(expression2);

            if (result1.IsPrintable || result2.IsPrintable)
            {
                // check in the visitor if the type is printable
                // if not, you probably broke something
                Assert.Fail("One of the results is printable");
            }
            else
            {
                Assert.That(result1.IsPrintable, Is.EqualTo(result2.IsPrintable));
            }
        }

        [Test]
        public void Test_Null_Enumerable_Printer()
        {
            Expression<Func<TestClass, bool>> expression = x => ((List<TestClass>)null!).Contains(x);
            var result = _keyGeneratorVisitor.ExpressionToString(expression);

            Assert.That(result.IsPrintable, Is.True);
        }

        [Test]
        public void Test_Enumerable_Printer_With_Printable_Values()
        {
            var list1 = GetDefaultList();
            var listWithPrintableValues = list1.Select(x => x.TestVal).ToList();

            Expression<Func<TestClass, bool>> expression = x => listWithPrintableValues.Contains(x.TestVal);
            var result1 = _keyGeneratorVisitor.ExpressionToString(expression);


            var list2 = GetDefaultList();
            Expression<Func<TestClass, bool>> expression2 = x => list1.Select(l => l.TestVal).Contains(x.TestVal);
            var result2 = _keyGeneratorVisitor.ExpressionToString(expression2);


            var list3 = GetDefaultList();
            list3.Add(new TestClass { TestVal = 1234, Test = "different" });
            var listWithPrintableValues3 = list3.Select(x => x.TestVal).ToList();

            Expression<Func<TestClass, bool>> expression3 = x => listWithPrintableValues3.Contains(x.TestVal);
            var result3 = _keyGeneratorVisitor.ExpressionToString(expression3);

            Assert.That(result1, Is.EqualTo(result2));

            Assert.That(result1, Is.Not.EqualTo(result3));
        }

        [Test]
        public void Test_Enumerable_Printer_With_Non_Printable_Values()
        {
            var list1 = GetDefaultList();
            Expression<Func<TestClass, bool>> expression = x => list1.Contains(x);
            var result1 = _keyGeneratorVisitor.ExpressionToString(expression);

            var list2 = GetDefaultList();
            Expression<Func<TestClass, bool>> expression2 = x => list2.Contains(x);
            var result2 = _keyGeneratorVisitor.ExpressionToString(expression2);

            if (result1.IsPrintable || result2.IsPrintable)
            {
                // check in the visitor if the type is printable
                // if not, you probably broke something
                Assert.Fail("One of the results is printable");
            }
            else
            {
                Assert.That(result1.IsPrintable, Is.EqualTo(result2.IsPrintable));
            }
        }

        [Test]
        public void Test_Enumerable_Printer_With_Printable_Null_Values()
        {
            var list1 = GetDefaultList().Select(x => (TestClass)null!).ToList();
            Expression<Func<TestClass, bool>> expression = x => list1.Contains(x);
            var result1 = _keyGeneratorVisitor.ExpressionToString(expression);


            var list2 = GetDefaultList();
            Expression<Func<TestClass, bool>> expression2 = x => list2.Select(l => (TestClass)null!).Contains(x);
            var result2 = _keyGeneratorVisitor.ExpressionToString(expression2);


            var list3 = GetDefaultList().Select(x => (TestClass)null!).ToList();
            list3.Add(null!);
            list3.Add(null!);
            Expression<Func<TestClass, bool>> expression3 = x => list3.Contains(x);
            var result3 = _keyGeneratorVisitor.ExpressionToString(expression3);

            Assert.That(result1, Is.EqualTo(result2));

            Assert.That(result1, Is.Not.EqualTo(result3));
        }

        [Test]
        public void Test_KeyGenerator_Is_Thread_Safe()
        {
       

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

            var expressions = new List<(Expression, string?)>
            {
                (expression1, _keyGeneratorVisitor.ExpressionToStringIfPrintable(expression1)),
                (expression2, _keyGeneratorVisitor.ExpressionToStringIfPrintable(expression2)),
                (expression3, _keyGeneratorVisitor.ExpressionToStringIfPrintable(expression3)),
                (expression4, _keyGeneratorVisitor.ExpressionToStringIfPrintable(expression4)),
            };

            if (expressions.All(x => x.Item2 == null))
            {
                Assert.Fail("The test is not testing anything, all items are not printable");
            }
            else if (expressions.All(x => x.Item2 != null))
            {
                Assert.Fail("The test is not testing anything, all items are printable");
            }

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 10
            };

      
            Parallel.For(0, 5000, parallelOptions, i =>
            {
                Parallel.ForEach(expressions, parallelOptions, x =>
                {
                    var keyStr = _keyGeneratorVisitor.ExpressionToStringIfPrintable(x.Item1);

                    if (keyStr != x.Item2)
                    {
                        Assert.Fail("Key generation is not thread safe");
                    }
                });
            });
        }
    }
}