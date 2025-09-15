using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace CachedEfCore.KeyGeneration.EvalTypeChecker
{
    public class ExpressionEvalTypeCheckerVisitor : ExpressionVisitor, IExpressionEvalTypeChecker
    {
        [ThreadStatic]
        private static bool _willEvalAnyType;

        private readonly ITypeCompatibilityChecker _typeCompatibilityChecker;

        public ExpressionEvalTypeCheckerVisitor(
            ITypeCompatibilityChecker typeCompatibilityChecker
        )
        {
            _typeCompatibilityChecker = typeCompatibilityChecker;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ResetState()
        {
            _willEvalAnyType = false;
        }

        public bool WillEvalTypes(Expression? expression)
        {
            ResetState();

            Visit(expression);

            return _willEvalAnyType;
        }
        
        private bool WillEvalType(Type type)
        {
            var canEval = _typeCompatibilityChecker.IsCompatible(type);
            return canEval;
        }

        private bool WillEvalType(object? value, Type type)
        {
            return value != null && WillEvalType(type);
        }

        [return: NotNullIfNotNull(nameof(node))]
        public override Expression? Visit(Expression? node)
        {
            if (_willEvalAnyType == true)
            {
                return node;
            }

            return base.Visit(node);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            _willEvalAnyType |= WillEvalType(node.Value, node.Type);

            return node;
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            //All parameters are evaluable, they have no value

            if (node.Body.NodeType != ExpressionType.Constant)
            {
                // x => constantExpressionBody
                // the returned constantExpressionBody is not evalued

                Visit(node.Body);
            }

            return node;
        }

        protected override Expression VisitBlock(BlockExpression node)
        {
            //All variables are evaluable, they have no value
            var iterations = node.Expressions.Count - 1;
            for (int i = 0; i < iterations; i++)
            {
                var expr = node.Expressions[i];

                Visit(expr);
            }
            // node.Result is the last one
            if (node.Result.NodeType != ExpressionType.Constant)
            {
                // { return constantExpression }
                // the constant expression will not be evaluated, its just gonna return it
                Visit(node.Result);
            }

            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (_willEvalAnyType != true)
            {
                if (node.Expression is not null)
                {
                    _willEvalAnyType |= WillEvalType(node.Expression.Type);
                    Visit(node.Expression);
                }
            }

            return node;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            _willEvalAnyType |= WillEvalType(node.Type);

            return node;
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            _willEvalAnyType |= WillEvalType(node.Type);

            Visit(node.Operand);

            return node;
        }

        protected override CatchBlock VisitCatchBlock(CatchBlock node)
        {
            //node.Variable is evaluable, it has no value

            Visit(node.Filter);
            Visit(node.Body);

            return node;
        }
    }
}