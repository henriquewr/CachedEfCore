using CachedEfCore.KeyGeneration.ExpressionEvaluation.EvalTypeChecker;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace CachedEfCore.KeyGeneration.ExpressionEvaluation
{
    public class CachedEfCoreEvalutableExpressionChecker : ICachedEfCoreEvalutableExpressionChecker
    {
        private readonly IExpressionEvalTypeChecker _expressionEvalTypeChecker;
        private readonly IEvaluatableExpressionFilter? _evaluatableExpressionFilter;
        private readonly GetParametersVisitor _getParametersVisitor;

        public CachedEfCoreEvalutableExpressionChecker(
            IExpressionEvalTypeChecker expressionEvalTypeChecker,
            IEvaluatableExpressionFilter? evaluatableExpressionFilter = null)
        {
            _expressionEvalTypeChecker = expressionEvalTypeChecker;
            _evaluatableExpressionFilter = evaluatableExpressionFilter;
            _getParametersVisitor = new();
        }

        public virtual bool IsEvaluatableExpression(Expression expression, IModel? model)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Constant:
                    return IsEvaluatableConstant((ConstantExpression)expression);

                case ExpressionType.MemberAccess:
                    return IsEvaluatableMemberExpression((MemberExpression)expression, model);

                case ExpressionType.Call:
                    return IsEvaluatableMethodCall((MethodCallExpression)expression, model);
            }

            return false;
        }

        public virtual bool IsEvaluatableConstant(ConstantExpression expression)
        {
            return _expressionEvalTypeChecker.WillEvalTypes(expression);
        }
        
        public virtual bool IsEvaluatableMethodCall(MethodCallExpression expression, IModel? model)
        {
            if (_expressionEvalTypeChecker.WillEvalTypes(expression))
            {
                return false;
            }

            var hasAllScopes = _getParametersVisitor.HasAllParamsScopes(expression);

            if (!hasAllScopes || TryGetIsEvaluatableExpression(expression, model, out var isEvaluatable) && isEvaluatable == false)
            {
                return false;
            }

            return true;
        }

        public virtual bool IsEvaluatableMemberExpression(MemberExpression expression, IModel? model)
        {
            if (_expressionEvalTypeChecker.WillEvalTypes(expression))
            {
                return false;
            }

            if (TryGetIsEvaluatableExpression(expression, model, out var isEvaluatable) && isEvaluatable == false)
            {
                return false;
            }

            while (expression.Expression != null && expression.Expression.NodeType == ExpressionType.MemberAccess)
            {
                expression = (MemberExpression)expression.Expression;
            }

            if (expression.Expression is null)
            {
                return false;
            }

            switch (expression.Expression.NodeType)
            {
                case ExpressionType.Constant:
                    return true;

                case ExpressionType.Call:
                    var canEval = IsEvaluatableMethodCall((MethodCallExpression)expression.Expression, model);
                    return canEval;

                default:
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetIsEvaluatableExpression(Expression expression, IModel? model, out bool isEvaluatable)
        {
            isEvaluatable = false;

            if (_evaluatableExpressionFilter is not null && model is not null)
            {
                isEvaluatable = _evaluatableExpressionFilter.IsEvaluatableExpression(expression, model);
                return true;
            }

            return false;
        }

        private sealed class GetParametersVisitor : ExpressionVisitor
        {
            [ThreadStatic]
            private static Dictionary<ParameterExpression, bool>? _parameterExpressions;

            private static void ResetState()
            {
                if (_parameterExpressions is null)
                {
                    _parameterExpressions = new Dictionary<ParameterExpression, bool>();
                }
                else
                {
                    _parameterExpressions.Clear();
                }
            }

            public bool HasAllParamsScopes(Expression expression)
            {
                var allParams = GetParameters(expression);
                var hasAllScopes = allParams.Values.All(x => x);
                return hasAllScopes;
            }

            private Dictionary<ParameterExpression, bool> GetParameters(Expression expression)
            {
                ResetState();

                Visit(expression);
                return _parameterExpressions!;
            }

            protected override Expression VisitLambda<T>(Expression<T> node)
            {
                var parameterExpressions = _parameterExpressions!;

                foreach (var param in node.Parameters)
                {
                    parameterExpressions[param] = true;
                }

                return base.VisitLambda(node);
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                _parameterExpressions!.TryAdd(node, false);

                return base.VisitParameter(node);
            }
        }
    }
}
