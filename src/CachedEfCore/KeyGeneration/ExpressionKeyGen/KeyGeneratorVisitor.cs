using CachedEfCore.KeyGeneration.ExpressionEvaluation;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace CachedEfCore.KeyGeneration.ExpressionKeyGen
{
    public readonly struct KeyGeneratorResult<T>
    {
        public KeyGeneratorResult(T expression, string? json)
        {
            Expression = expression;
            AdditionalJson = json;
        }

        public readonly T Expression { get; }
        public readonly string? AdditionalJson { get; }
    }

    public class KeyGeneratorVisitor : ExpressionVisitor, IDisposable, IAsyncDisposable
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        private readonly IPrintabilityChecker _printableHelper;
        private readonly ICachedEfCoreEvalutableExpressionChecker _cachedEfCoreEvalutableExpressionChecker;

        private class KeyGeneratorState : IDisposable, IAsyncDisposable
        {
            public required ValuePrinter ValuePrinter { get; set; }
            public required IModel? Model { get; set; }

            public static KeyGeneratorState CreateNew(KeyGeneratorVisitor instance)
            {
                return new KeyGeneratorState
                {
                    ValuePrinter = new(instance._jsonSerializerOptions),
                    Model = null,
                };
            }

            public void Dispose()
            {
                Model = null;
                ValuePrinter.Dispose();
            }

            public ValueTask DisposeAsync()
            {
                Model = null;
                return ValuePrinter.DisposeAsync();
            }

            public void ResetState()
            {
                Model = null;
                ValuePrinter.ResetState();
            }

            public bool ShouldCreateNew()
            {
                return ValuePrinter.IsDisposed;
            }
        }

        [ThreadStatic]
        private static KeyGeneratorState? _state;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetState(IModel? model)
        {
            if (_state is null)
            {
                _state = KeyGeneratorState.CreateNew(this);
            }
            else if (_state.ShouldCreateNew())
            {
                _state.Dispose();
                _state = KeyGeneratorState.CreateNew(this);
            }
            else
            {
                _state.ResetState();
            }

            _state.Model = model;
        }

        public KeyGeneratorVisitor(
            IPrintabilityChecker printableHelper,
            ICachedEfCoreEvalutableExpressionChecker cachedEfCoreEvalutableExpressionChecker,
            JsonSerializerOptions jsonSerializerOptions
        )
        {
            _printableHelper = printableHelper;
            _jsonSerializerOptions = jsonSerializerOptions;
            _cachedEfCoreEvalutableExpressionChecker = cachedEfCoreEvalutableExpressionChecker;
        }

        /// <summary>
        /// Replaces the variables in a expression with their "real values", this method never throws exceptions, if the expression cannot be evaluated it returns null
        /// </summary>
        /// <returns>
        /// A string representation of the expression with the "real values
        /// "</returns>
        /// <param name="node">The expression to convert</param>
        /// <param name="model">The EF Core model associated with the query, if the expression is part of an EF Core-generated query.</param>
        public KeyGeneratorResult<string>? SafeExpressionToString(Expression node, IModel? model)
        {
            try
            {
                return ExpressionToString(node, model);
            }
#pragma warning disable CS0168 // Variable is declared but never used (for debug view)
            catch (Exception ex)
#pragma warning restore CS0168 // Variable is declared but never used
            {
                return null;
            }
        }

        /// <summary>
        /// Replaces the variables in a expression with their "real values"
        /// </summary>
        /// <returns>
        /// A string representation of the expression with the "real values
        /// "</returns>
        /// <param name="node">The expression to convert</param>
        /// <param name="model">The EF Core model associated with the query, if the expression is part of an EF Core-generated query.</param>
        public KeyGeneratorResult<string> ExpressionToString(Expression node, IModel? model)
        {
            // The Visit method returns the expression with the 'real values'
            // ex: 
            // var variable = "exampleValue";
            // var result = Visit(x => x.Property == variable).ToString();
            // result is "x => x.Property == "exampleValue""

            // If a method call can be evaluated locally it will be evaluated and exchanged for the result
            // ex: 
            // var variable = "someValue";
            // var list = new List<string>
            // {
            //     "someValue"
            // };
            // var result = Visit(x => x.Property == variable || list.Contains(variable)).ToString();
            // result is "x => x.Property == "exampleValue" || true"
            // list.Contains(variable) is evaluated and returned true
            // and the method call is exchanged for the result

            var visited = VisitWithState(node, model);
            var result = new KeyGeneratorResult<string>
            (
                visited.Expression.ToString(),
                visited.AdditionalJson
            );

            return result;
        }

        public KeyGeneratorResult<T> VisitExpr<T>(T expression, IModel? model) where T : Expression
        {
            var visited = VisitWithState(expression, model);

            var result = new KeyGeneratorResult<T>
            (
                (T)visited.Expression,
                visited.AdditionalJson
            );

            return result;
        }

        private KeyGeneratorResult<Expression> VisitWithState(Expression node, IModel? model)
        {
            ResetState(model);
            var expression = base.Visit(node);

            var additionalJson = _state!.ValuePrinter.GetResult();

            var result = new KeyGeneratorResult<Expression>
            (
                expression,
                additionalJson
            );

            return result;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            try
            {
                var canEval = _cachedEfCoreEvalutableExpressionChecker.IsEvaluatableMethodCall(node, _state!.Model);

                if (!canEval)
                {
                    return base.VisitMethodCall(node);
                }

                var evaluated = EvalMethodCall(node);
                var constExprResult = Expression.Constant(evaluated.Result, evaluated.ResultType);
                return VisitConstant(constExprResult);
            }
#pragma warning disable CS0168 // Variable is declared but never used (for debug view)
            catch (Exception ex)
#pragma warning restore CS0168 // Variable is declared but never used
            {

#if TEST_BUILD
                throw;
#endif

#pragma warning disable CS0162 // Unreachable code detected
                return base.VisitMethodCall(node);
#pragma warning restore CS0162 // Unreachable code detected
            }
        }

        private static (object? Result, Type ResultType) EvalMethodCall(MethodCallExpression node)
        {
            //TODO Maybe use preferInterpretation true in the Compile method, it was way faster in some tests, but it needs more thinking about that
            /*
                //that code was faster but it needs more testing
                var instance = node.Object != null ? Evaluate(node.Object) : null;
                var arguments = node.Arguments.Select(x => x is LambdaExpression lambda ? lambda.Compile() : Evaluate(x)).ToArray();
                var result = node.Method.Invoke(instance, arguments);
            */

            var lambda = Expression.Lambda(node);
            var compiledLambda = lambda.Compile();
            var result = compiledLambda.DynamicInvoke();

            return (Result: result, ResultType: compiledLambda.Method.ReturnType);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (!_cachedEfCoreEvalutableExpressionChecker.IsEvaluatableConstant(node))
            {
                var isPrintable = _printableHelper.IsPrintable(node.Value, node.Type);
                if (!isPrintable)
                {
                    _state!.ValuePrinter.Print(node.Value);
                }
            }

            return base.VisitConstant(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (_cachedEfCoreEvalutableExpressionChecker.IsEvaluatableMemberExpression(node, _state!.Model))
            {
                var value = Evaluate(node);

                var constantValue = Expression.Constant(value, node.Type);
                return VisitConstant(constantValue);
            }
            else
            {
                return base.VisitMember(node);
            }
        }

        private static object? Evaluate(Expression exp)
        {
            if (exp.NodeType == ExpressionType.Constant)
            {
                return ((ConstantExpression)exp).Value;
            }
            else if (exp.NodeType == ExpressionType.Call)
            {
                return EvalMethodCall((MethodCallExpression)exp).Result;
            }

            MemberExpression mexp = (MemberExpression)exp;
            var value = Evaluate(mexp.Expression!);

            if (mexp.Member is FieldInfo field)
            {
                var fieldVal = field.GetValue(value);
                return fieldVal;
            }

            PropertyInfo property = (PropertyInfo)mexp.Member;
            var propVal = property.GetValue(value, null);
            return propVal;
        }

        public void Dispose()
        {
            _state?.Dispose();

            GC.SuppressFinalize(this);
        }

        public ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);

            if (_state is null)
            {
                return ValueTask.CompletedTask;
            }

            return _state.DisposeAsync();
        }
    }
}