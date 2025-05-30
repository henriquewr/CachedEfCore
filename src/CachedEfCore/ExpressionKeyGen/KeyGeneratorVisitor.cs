﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CachedEfCore.ExpressionKeyGen
{
    public readonly struct KeyGeneratorResult<T>
    {
        public KeyGeneratorResult(T expression, string? json)
        {
            Expression = expression;
            AdditionalJson = json;
        }

        public readonly T Expression;
        public readonly string? AdditionalJson;
    }

    public class KeyGeneratorVisitor : ExpressionVisitor, IDisposable, IAsyncDisposable
    {
        private readonly GetParametersVisitor _getParametersVisitor;
        private readonly JsonSerializerOptions _jsonSerializerOptions;

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

        [ThreadStatic]
        private static MemoryStream? _memoryStream;

        public KeyGeneratorVisitor(JsonSerializerOptions jsonSerializerOptions)
        {
            _jsonSerializerOptions = jsonSerializerOptions;
            _getParametersVisitor = new GetParametersVisitor();
        }

        public KeyGeneratorVisitor(IEnumerable<Type> printableTypes) : this()
        {
            _printableTypes.UnionWith(printableTypes);
        }

        public KeyGeneratorVisitor() : this(new JsonSerializerOptions { IncludeFields = true })
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ResetState()
        {
            if (_memoryStream is null)
            {
                _memoryStream = new MemoryStream();
            }
            else
            {
                _memoryStream.Position = 0;
                _memoryStream.SetLength(0);
            }
        }

        public KeyGeneratorResult<string>? SafeExpressionToString(Expression node)
        {
            try
            {
                return ExpressionToString(node);
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
        /// <returns>A string representation of the expression with the "real values"</returns>
        public KeyGeneratorResult<string> ExpressionToString(Expression node)
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

            var visited = VisitWithState(node);
            var result = new KeyGeneratorResult<string>
            (
                visited.Expression.ToString(),
                visited.AdditionalJson
            );

            return result;
        }

        public KeyGeneratorResult<T> VisitExpr<T>(T expression) where T : Expression
        {
            var visited = VisitWithState(expression);

            var result = new KeyGeneratorResult<T>
            (
                (T)visited.Expression,
                visited.AdditionalJson
            );

            return result;
        }

        private KeyGeneratorResult<Expression> VisitWithState(Expression node)
        {
            ResetState();
            var expression = base.Visit(node);

            var l_memoryStream = _memoryStream!;
            l_memoryStream.Flush();
            var additionalJson = l_memoryStream.Length == 0 ? null : Encoding.UTF8.GetString(l_memoryStream.GetBuffer(), 0, (int)l_memoryStream.Length);

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
                var canEval = CanEvalMethodCall(node);

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
                return base.VisitMethodCall(node);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CanEvalMethodCall(MethodCallExpression node)
        {
            var hasAllScopes = _getParametersVisitor.HasAllParamsScopes(node);

            return hasAllScopes;
        }

        private static (object? Result, Type ResultType) EvalMethodCall(MethodCallExpression node)
        {
            var lambda = Expression.Lambda(node);
            var compiledLambda = lambda.Compile();
            var result = compiledLambda.DynamicInvoke();

            return (Result: result, ResultType: compiledLambda.Method.ReturnType);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            var isPrintable = IsPrintable(node.Value, node.Type);
            if (!isPrintable)
            {
                JsonSerializer.Serialize(_memoryStream!, node.Value, _jsonSerializerOptions);
            }

            return base.VisitConstant(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (CanBeEvaluated(node))
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

        private bool IsPrintable(IEnumerable values, Type[] types)
        {
            if (types.All(IsTypePrintable))
            {
                return true;
            }

            foreach (var item in values)
            {
                if (item is not null)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsPrintable(object? value, Type type)
        {
            if (value == null)
            {
                // null is always printable regardless of type
                return true;
            }

            return IsTypePrintable(type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsTypePrintable(Type type)
        {
            //Enums are always printable
            return type.IsEnum || _printableTypes.Contains(type);
        }

        private bool CanBeEvaluated(MemberExpression exp)
        {
            while (exp.Expression != null && exp.Expression.NodeType == ExpressionType.MemberAccess)
            {
                exp = (MemberExpression)exp.Expression;
            }

            if (exp.Expression is null)
            {
                return false;
            }

            switch (exp.Expression.NodeType)
            {
                case ExpressionType.Constant:
                    return true;

                case ExpressionType.Call:
                    var canEval = CanEvalMethodCall((MethodCallExpression)exp.Expression);
                    return canEval;

                default:
                    return false;
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
            _memoryStream?.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            if (_memoryStream is null)
            {
                return ValueTask.CompletedTask;
            }

            return _memoryStream.DisposeAsync();
        }

        private sealed class GetParametersVisitor : ExpressionVisitor
        {
            private readonly Dictionary<ParameterExpression, int> _parameterExpressions;

            private readonly Lock _dictionaryLock = new Lock();

            public GetParametersVisitor()
            {
                _parameterExpressions = new Dictionary<ParameterExpression, int>();
            }

            public bool HasAllParamsScopes(Expression expression)
            {
                lock (_dictionaryLock)
                {
                    var allParams = GetParametersNoLock(expression);
                    var hasAllScopes = !allParams.Values.Any(x => x == 1);
                    return hasAllScopes;
                }
            }

            public Dictionary<ParameterExpression, int> GetParameters(Expression expression)
            {
                lock (_dictionaryLock)
                {
                    return GetParametersNoLock(expression);
                }
            }

            private Dictionary<ParameterExpression, int> GetParametersNoLock(Expression expression)
            {
                _parameterExpressions.Clear();
                Visit(expression);
                return _parameterExpressions;
            }

            private void ParamFound(ParameterExpression param)
            {
                ref var value = ref CollectionsMarshal.GetValueRefOrAddDefault(_parameterExpressions, param, out _);
                value++;
            }

            protected override Expression VisitLambda<T>(Expression<T> node)
            {
                foreach (var param in node.Parameters)
                {
                    ParamFound(param);
                }

                return base.VisitLambda(node);
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                ParamFound(node);

                return base.VisitParameter(node);
            }
        }
    }
}