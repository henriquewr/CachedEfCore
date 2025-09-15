using CachedEfCore.KeyGeneration.EvalTypeChecker;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;

namespace CachedEfCore.KeyGeneration.Tests
{
    public class ExpressionEvalTypeCheckerTests
    {
        private static ExpressionEvalTypeCheckerVisitor CreateEvalTypeChecker(params IEnumerable<Type> types)
        {
            return new ExpressionEvalTypeCheckerVisitor(new TypeCompatibilityChecker(types));
        }

        public static TheoryData<BinaryExpression?, bool, ExpressionEvalTypeCheckerVisitor> GetBinaryExpressionTestCases()
        {
            return new()
            {
                {
                    Expression.Add
                    (
                        Expression.Constant(1),
                        Expression.Constant(2)
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.Add
                    (
                        Expression.Convert(Expression.Constant(1), typeof(double)),
                        Expression.Constant(2D)
                    ),
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    null,
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetBinaryExpressionTestCases))]
        public void BinaryExpression(BinaryExpression? binaryExpr, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var result = evalTypeChecker.WillEvalTypes(binaryExpr);

            Assert.Equal(expectedResult, result);
        }


        public static TheoryData<ConditionalExpression?, bool, ExpressionEvalTypeCheckerVisitor> GetConditionalExpressionTestCases()
        {
            return new()
            {
                { 
                    Expression.Condition
                    (
                        Expression.Constant(true),
                        Expression.Constant(1),
                        Expression.Constant(2)
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.Condition
                    (
                        Expression.Constant(true),
                        Expression.Constant(1D),
                        Expression.Convert(Expression.Constant(1), typeof(double))
                    ), 
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )                
                },
                {
                    Expression.Condition
                    (
                        Expression.Equal(Expression.Constant(new List<int>().AsQueryable()), Expression.Constant(null)),
                        Expression.Constant(1),
                        Expression.Constant(1)
                    ),
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(IQueryable<>)
                    )
                },
                { 
                    null, 
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetConditionalExpressionTestCases))]
        public void ConditionalExpression(ConditionalExpression? expr, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var result = evalTypeChecker.WillEvalTypes(expr);

            Assert.Equal(expectedResult, result);
        }


        public static TheoryData<ConstantExpression?, bool, ExpressionEvalTypeCheckerVisitor> GetConstantExpressionTestCases()
        {
            return new()
            {
                { 
                    Expression.Constant(3), 
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                { 
                    Expression.Constant(3.12D), 
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                { 
                    null, 
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetConstantExpressionTestCases))]
        public void ConstantExpression(ConstantExpression? expr, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var result = evalTypeChecker.WillEvalTypes(expr);

            Assert.Equal(expectedResult, result);
        }


        public static TheoryData<BlockExpression?, bool, ExpressionEvalTypeCheckerVisitor> GetBlockExpressionTestCases()
        {
            return new()
            {
                { 
                    Expression.Block
                    (
                        Expression.Constant(1),
                        Expression.Constant(2),
                        Expression.Constant(3)
                    ), 
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                { 
                    Expression.Block
                    (
                        Expression.Constant(1), 
                        Expression.Constant(2), 
                        Expression.Constant(3.12D)
                    ), 
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.Block
                    (
                        Expression.Constant(3.1762D)
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.Block
                    (
                        new[] { Expression.Variable(typeof(double), "nonUsed") },
                        Expression.Constant(1),
                        Expression.Constant(2)
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.Block
                    (
                        new[] { Expression.Variable(typeof(double), "usedVariable") },
                        Expression.Assign
                        (
                            Expression.Variable(typeof(double), "usedVariable"),
                            Expression.Constant(14.354D)
                        ),
                        Expression.Constant(2)
                    ),
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                { 
                    null, 
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double) 
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetBlockExpressionTestCases))]
        public void BlockExpression(BlockExpression? blockExpr, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var result = evalTypeChecker.WillEvalTypes(blockExpr);

            Assert.Equal(expectedResult, result);
        }


        public static TheoryData<LambdaExpression?, bool, ExpressionEvalTypeCheckerVisitor> GetLambdaExpressionTestCases()
        {
            Expression<Func<int, int, int>> lambdaExpr = (x, y) => x + y;

            Expression<Func<int, int, double>> lambdaExpr2 = (x, y) => x + y + 3.34D;

            Expression<Func<int, int, double>> lambdaExpr3 = (x, y) => 3.123D;

            return new()
            {
                { 
                    lambdaExpr, 
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                { 
                    lambdaExpr2, 
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    lambdaExpr3,
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double),
                        typeof(int)
                    )
                },
                {
                    null,
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetLambdaExpressionTestCases))]
        public void LambdaExpression(LambdaExpression? lambdaExpr, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var result = evalTypeChecker.WillEvalTypes(lambdaExpr);

            Assert.Equal(expectedResult, result);
        }

        
        public static TheoryData<Expression?, bool, ExpressionEvalTypeCheckerVisitor> GetGotoExpressionTestCases()
        {
            return new()
            {
                { 
                    Expression.Constant(0), 
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                { 
                    Expression.Constant(1.12D),
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                { 
                    null, 
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetGotoExpressionTestCases))]
        public void GotoExpression(Expression? value, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var label = Expression.Label("test");
            var expr = Expression.Goto(label, value);

            var result = evalTypeChecker.WillEvalTypes(expr);

            Assert.Equal(expectedResult, result);
        }


        public static TheoryData<MemberExpression?, bool, ExpressionEvalTypeCheckerVisitor> GetMemberExpressionTestCases()
        {
            return new()
            {
                { 
                    // "s".Length
                    Expression.Property
                    (
                        Expression.Parameter(typeof(string), "s"), nameof(string.Length)
                    ), 
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double),

                        // int is the result type, it is not really evaluated
                        typeof(int)
                    )
                },
                {
                    // |-----------|
                    // ((TestClass)s).Value
                    Expression.Property
                    (
                        Expression.Parameter(typeof(TestClass), "s"), nameof(TestClass.Value)
                    ),
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double),
                        typeof(TestClass)
                    )
                }, 
                { 
                    null, 
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetMemberExpressionTestCases))]
        public void MemberExpression(MemberExpression? expr, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var result = evalTypeChecker.WillEvalTypes(expr);

            Assert.Equal(expectedResult, result);
        }


        public static TheoryData<MemberInitExpression?, bool, ExpressionEvalTypeCheckerVisitor> GetMemberInitExpressionTestCases()
        {
            return new()
            {
                { 
                    Expression.MemberInit
                    (
                        Expression.New(typeof(TestClass).GetConstructor(Type.EmptyTypes)!), 
                        Expression.Bind
                        (
                            typeof(TestClass).GetProperty(nameof(TestClass.Value))!, 
                            Expression.Constant(5)
                        )
                    ), 
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double),
                        typeof(TestClass)
                    )
                },
                {
                    Expression.MemberInit
                    (
                        Expression.New(typeof(TestClass).GetConstructor(Type.EmptyTypes)!),
                        Expression.Bind
                        (
                            typeof(TestClass).GetProperty(nameof(TestClass.DoubleValue))!,
                            Expression.Constant(5.123D)
                        )
                    ), 
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double),
                        typeof(TestClass)
                    )
                },
                { 
                    null, 
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double),
                        typeof(TestClass)
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetMemberInitExpressionTestCases))]
        public void MemberInitExpression(MemberInitExpression? expr, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var result = evalTypeChecker.WillEvalTypes(expr);

            Assert.Equal(expectedResult, result);
        }


        public static TheoryData<MethodCallExpression?, bool, ExpressionEvalTypeCheckerVisitor> GetMethodCallExpressionTestCases()
        {
            return new()
            {
                {
                    // ((TestClass)instance).MethodIntConvertsToDouble(5)
                    Expression.Call
                    (
                        Expression.Constant(new TestClass()),
                        typeof(TestClass).GetMethod(nameof(TestClass.MethodIntConvertsToDouble))!,
                        Expression.Constant(5)
                    ), 
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    //                                   |------|
                    // ((TestClass)instance).MethodDouble(5.123D)
                    Expression.Call
                    (
                        Expression.Constant(new TestClass()),
                        typeof(TestClass).GetMethod(nameof(TestClass.MethodDouble))!,
                        Expression.Constant(5.123D)
                    ), 
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    // |-------------------|
                    // ((TestClass)instance).MethodIntConvertsToDouble(5)
                    Expression.Call
                    (
                        Expression.Constant(new TestClass()),
                        typeof(TestClass).GetMethod(nameof(TestClass.MethodIntConvertsToDouble))!,
                        Expression.Constant(5)
                    ),
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double),
                        typeof(TestClass)
                    )
                },
                { 
                    null, 
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetMethodCallExpressionTestCases))]
        public void MethodCallExpression(MethodCallExpression? expr, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var result = evalTypeChecker.WillEvalTypes(expr);

            Assert.Equal(expectedResult, result);
        }


        public static TheoryData<NewExpression?, bool, ExpressionEvalTypeCheckerVisitor> GetNewExpressionTestCases()
        {
            return new()
            {
                {
                    // new TestClass(1)
                    Expression.New
                    (
                        typeof(TestClass).GetConstructor(new[]{ typeof(int) })!,
                        Expression.Constant(1)
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    // new TestClass()
                    Expression.New
                    (
                        typeof(TestClass).GetConstructor(Type.EmptyTypes)!
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double),
                        typeof(TestClass)
                    )
                },
                {
                    //              |-|
                    // new TestClass(1)
                    Expression.New
                    (
                        typeof(TestClass).GetConstructor(new[]{ typeof(int) })!,
                        Expression.Constant(1)
                    ),
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(int)
                    )
                },
                {
                    
                    // new Tuple<int, int>(1, 2)
                    Expression.New
                    (
                        typeof(Tuple<int, int>).GetConstructor(new[] { typeof(int), typeof(int) })!,
                        Expression.Constant(1),
                        Expression.Constant(2)
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double),
                        typeof(TestClass)
                    )
                },
                {
                    null,
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetNewExpressionTestCases))]
        public void NewExpression(NewExpression? expr, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var result = evalTypeChecker.WillEvalTypes(expr);

            Assert.Equal(expectedResult, result);
        }


        public static TheoryData<NewArrayExpression?, bool, ExpressionEvalTypeCheckerVisitor> GetNewArrayExpressionTestCases()
        {
            return new()
            {
                {
                    // new int[] { 1 }
                    Expression.NewArrayInit
                    (
                        typeof(int), 
                        Expression.Constant(1)
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    // new int[] { 1 }
                    Expression.NewArrayInit
                    (
                        typeof(int),
                        Expression.Constant(1)
                    ),
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(int)
                    )
                },
                {
                    null,
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetNewArrayExpressionTestCases))]
        public void NewArrayExpression(NewArrayExpression? expr, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var result = evalTypeChecker.WillEvalTypes(expr);

            Assert.Equal(expectedResult, result);
        }


        public static TheoryData<ParameterExpression?, bool, ExpressionEvalTypeCheckerVisitor> GetParameterExpressionTestCases()
        {
            // The parameters has an interesting logic:
            // Creating the ParameterExpression by Expression.Parameter means that is a "Value" parameter, because it has no parent
            // For example:
            // "x => x.Something", both "x" are parameters, but the "x" parameter before "=>" has no value, and after "=>" the "x" has a value
            // the first "x" is a placeholder (the definition of a param), and the second "x" is the identifier refering to the parameter

            return new()
            {
                {
                    Expression.Parameter(typeof(int)),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.Parameter(typeof(int)),
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double),
                        typeof(int)
                    )
                },
                {
                    null,
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetParameterExpressionTestCases))]
        public void ParameterExpression(ParameterExpression? expr, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var result = evalTypeChecker.WillEvalTypes(expr);

            Assert.Equal(expectedResult, result);
        }


        public static TheoryData<UnaryExpression?, bool, ExpressionEvalTypeCheckerVisitor> GetUnaryExpressionTestCases()
        {
            return new()
            {
                {
                    Expression.Convert(Expression.Constant(1), typeof(double)),
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.Convert(Expression.Constant(1.123D), typeof(int)),
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.Convert(Expression.Constant(1), typeof(double)),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(long)
                    )
                },
                {
                    Expression.Convert(Expression.Constant(1.123D), typeof(int)),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(long)
                    )
                },
                {
                    null,
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetUnaryExpressionTestCases))]
        public void UnaryExpression(UnaryExpression? expr, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var result = evalTypeChecker.WillEvalTypes(expr);

            Assert.Equal(expectedResult, result);
        }


        public static TheoryData<DefaultExpression?, bool, ExpressionEvalTypeCheckerVisitor> GetDefaultExpressionTestCases()
        {
            return new()
            {
                {
                    Expression.Default(typeof(double)),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    null,
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetDefaultExpressionTestCases))]
        public void DefaultExpression(DefaultExpression? expr, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var result = evalTypeChecker.WillEvalTypes(expr);

            Assert.Equal(expectedResult, result);
        }


        public static TheoryData<TryExpression?, bool, ExpressionEvalTypeCheckerVisitor> GetTryExpressionTestCases()
        {
            return new()
            {
                {
                    Expression.TryCatch
                    (
                        Expression.Constant(1),
                        Expression.Catch
                        (
                            typeof(Exception), 
                            Expression.Constant(2)
                        )
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double),
                        typeof(Exception)
                    )
                },
                {
                    Expression.TryCatch
                    (
                        Expression.Constant(1.123D),
                        Expression.Catch
                        (
                            typeof(Exception),
                            Expression.Constant(2.123D)
                        )
                    ),
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    null,
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetTryExpressionTestCases))]
        public void TryExpression(TryExpression? expr, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var result = evalTypeChecker.WillEvalTypes(expr);

            Assert.Equal(expectedResult, result);
        }

        public static TheoryData<IndexExpression?, bool, ExpressionEvalTypeCheckerVisitor> GetIndexExpressionTestCases()
        {
            return new()
            {
                {
                    Expression.ArrayAccess
                    (
                        Expression.Constant(new int[3]),
                        Expression.Constant(0)
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.ArrayAccess
                    (
                        Expression.Constant(new double[3]),
                        Expression.Constant(0)
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.ArrayAccess
                    (
                        Expression.Constant(new double[3]),
                        Expression.Constant(0)
                    ),
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(int)
                    )
                },
                {
                    null,
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetIndexExpressionTestCases))]
        public void IndexExpression(IndexExpression? expr, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var result = evalTypeChecker.WillEvalTypes(expr);

            Assert.Equal(expectedResult, result);
        }


        public static TheoryData<TypeBinaryExpression?, bool, ExpressionEvalTypeCheckerVisitor> GetTypeBinaryExpressionTestCases()
        {
            return new()
            {
                {
                    Expression.TypeIs
                    (
                        Expression.Constant("abc"), 
                        typeof(string)
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.TypeIs
                    (
                        Expression.Constant(1.1234D),
                        typeof(string)
                    ),
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.TypeIs
                    (
                        Expression.Constant(1.1234D),
                        typeof(string)
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(string)
                    )
                },
                {
                    null,
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetTypeBinaryExpressionTestCases))]
        public void TypeBinaryExpression(TypeBinaryExpression? expr, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var result = evalTypeChecker.WillEvalTypes(expr);

            Assert.Equal(expectedResult, result);
        }


        public static TheoryData<SwitchExpression?, bool, ExpressionEvalTypeCheckerVisitor> GetSwitchExpressionTestCases()
        {
            return new()
            {
                {
                    Expression.Switch
                    (
                        Expression.Constant(1),
                        Expression.Constant(12),
                        Expression.SwitchCase
                        (
                            Expression.Constant(2),
                            Expression.Constant(123)
                        )
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.Switch
                    (
                        Expression.Constant(1D),
                        Expression.Constant(12),
                        Expression.SwitchCase
                        (
                            Expression.Constant(2),
                            Expression.Constant(123D)
                        )
                    ),
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    null,
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetSwitchExpressionTestCases))]
        public void SwitchExpression(SwitchExpression? expr, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var result = evalTypeChecker.WillEvalTypes(expr);

            Assert.Equal(expectedResult, result);
        }


        public static TheoryData<InvocationExpression?, bool, ExpressionEvalTypeCheckerVisitor> GetInvocationExpressionTestCases()
        {
            Expression<Func<int, int, int>> lambdaExpr = (x, y) => x + y;

            Expression<Func<int, int, double>> lambdaExpr2 = (x, y) => x + y + 3.34D;

            Expression<Func<double, int>> lambdaExpr3 = x => 1;

            Expression<Func<int, double>> lambdaExpr4 = x => 1.12D;

            Func<int, double> func = x => 3.456D;
            Expression<Func<int, double>> lambdaExpr5 = x => func(x);

            Expression<Func<int, double>> lambdaExpr6 = x => 1.12D;

            var x = Expression.Parameter(typeof(int), "x");
            var y = Expression.Variable(typeof(int), "y");
            var lambdaWithBlock = Expression.Lambda<Func<int, int>>
            (
                Expression.Block
                (
                    new[] { y },
                    Expression.Assign
                    (
                        y,
                        Expression.Add
                        (
                            x,
                            Expression.Constant(2)
                        )
                    ),
                    Expression.Add
                    (
                        y,
                        Expression.Constant(10)
                    )
                ), 
                x
            );


            var x2 = Expression.Parameter(typeof(int), "x");
            var y2 = Expression.Variable(typeof(double), "y");
            var lambdaWithBlock2 = Expression.Lambda<Func<int, double>>
            (
                Expression.Block
                (
                    new[] { y2 },
                    Expression.Assign
                    (
                        y2,
                        Expression.Constant(1.34D)
                    ),
                    Expression.Constant(1.3456D)
                ), 
                x2
            );


            var x3 = Expression.Parameter(typeof(int), "x");
            var y3 = Expression.Variable(typeof(int), "y");
            var lambdaWithBlock3 = Expression.Lambda<Func<int, double>>
            (
                Expression.Block
                (
                    new[] { y3 },
                    Expression.Assign
                    (
                        y3,
                        Expression.Constant(1)
                    ),
                    Expression.Constant(1.34556D)
                ),
                x3
            );

            return new()
            {
                {
                    Expression.Invoke
                    (
                        lambdaExpr,
                        Expression.Constant(1),
                        Expression.Constant(5)
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.Invoke
                    (
                        lambdaExpr2,
                        Expression.Constant(1),
                        Expression.Constant(5)
                    ),
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.Invoke
                    (
                        lambdaExpr3,
                        Expression.Constant(1.334D)
                    ),
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.Invoke
                    (
                        lambdaExpr4,
                        Expression.Constant(1)
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.Invoke
                    (
                        lambdaExpr5,
                        Expression.Constant(1)
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.Invoke
                    (
                        lambdaWithBlock,
                        Expression.Constant(1)
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.Invoke
                    (
                        lambdaWithBlock2,
                        Expression.Constant(1)
                    ),
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.Invoke
                    (
                        lambdaWithBlock3,
                        Expression.Constant(1)
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    null,
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetInvocationExpressionTestCases))]
        public void InvocationExpression(InvocationExpression? expr, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var result = evalTypeChecker.WillEvalTypes(expr);
            
            Assert.Equal(expectedResult, result);
        }


        public static TheoryData<LoopExpression?, bool, ExpressionEvalTypeCheckerVisitor> GetLoopExpressionTestCases()
        {
            var loopBreak = Expression.Label("loopBreak");

            return new()
            {
                {
                    Expression.Loop
                    (
                        Expression.Break(loopBreak),
                        loopBreak
                    ),
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    Expression.Loop
                    (
                        Expression.Block
                        (
                            Expression.Break
                            (
                                loopBreak, 
                                Expression.Constant(1.345D)
                            )
                        ),
                        loopBreak
                    ),
                    true,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
                {
                    null,
                    false,
                    CreateEvalTypeChecker
                    (
                        typeof(double)
                    )
                },
            };
        }
        [Theory]
        [MemberData(nameof(GetLoopExpressionTestCases))]
        public void LoopExpression(LoopExpression? expr, bool expectedResult, ExpressionEvalTypeCheckerVisitor evalTypeChecker)
        {
            var result = evalTypeChecker.WillEvalTypes(expr);

            Assert.Equal(expectedResult, result);
        }


        [Fact]
        public void ExpressionEvalTypeCheckerVisitor_Is_Thread_Safe()
        {
            var evalTypeChecker = CreateEvalTypeChecker(typeof(double));

            var x = Expression.Parameter(typeof(int), "x");
            var y = Expression.Variable(typeof(int), "y");
            var lambdaWithBlock = Expression.Lambda<Func<int, int>>
            (
                Expression.Block
                (
                    new[] { y },
                    Expression.Assign
                    (
                        y,
                        Expression.Add
                        (
                            x,
                            Expression.Constant(2)
                        )
                    ),
                    Expression.Add
                    (
                        y,
                        Expression.Constant(10)
                    )
                ),
                x
            );


            var x2 = Expression.Parameter(typeof(int), "x");
            var y2 = Expression.Variable(typeof(double), "y");
            var lambdaWithBlock2 = Expression.Lambda<Func<int, double>>
            (
                Expression.Block
                (
                    new[] { y2 },
                    Expression.Assign
                    (
                        y2,
                        Expression.Constant(23.45D)
                    ),
                    Expression.Add
                    (
                        y2,
                        Expression.Constant(13.40D)
                    )
                ),
                x2
            );

            var expression3 = Expression.Add(Expression.Constant(1.3455D), Expression.Convert(Expression.Constant(1), typeof(double)));

            var expression4 = Expression.Add(Expression.Constant(44), Expression.Constant(1));

            Assert.NotEqual(evalTypeChecker.WillEvalTypes(expression3), evalTypeChecker.WillEvalTypes(expression4));
            Assert.NotEqual(evalTypeChecker.WillEvalTypes(lambdaWithBlock), evalTypeChecker.WillEvalTypes(lambdaWithBlock2));

            var expressions = new List<(Expression, bool)>
            {
                (lambdaWithBlock, evalTypeChecker.WillEvalTypes(lambdaWithBlock)),
                (lambdaWithBlock2, evalTypeChecker.WillEvalTypes(lambdaWithBlock2)),

                (expression3, evalTypeChecker.WillEvalTypes(expression3)),
                (expression4, evalTypeChecker.WillEvalTypes(expression4)),
            };

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 10
            };

            Parallel.For(0, 5000, parallelOptions, i =>
            {
                Parallel.ForEach(expressions, parallelOptions, x =>
                {
                    var keyStr = evalTypeChecker.WillEvalTypes(x.Item1);

                    Assert.Equal(x.Item2, keyStr);
                });
            });
        }

        private class TestClass
        {
            public TestClass()
            {
                
            }

            public TestClass(int value)
            {

            }

            public TestClass(int value, double doubleValue)
            {

            }

            public int Value { get; set; }
            public double DoubleValue { get; set; }

            public double MethodDouble(double value)
            {
                return value;
            }

            public double MethodIntConvertsToDouble(int value)
            {
                return value;
            }
        }
    }
}