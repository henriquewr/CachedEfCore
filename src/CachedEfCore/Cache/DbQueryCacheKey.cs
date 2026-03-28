using Microsoft.Extensions.ObjectPool;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace CachedEfCore.Cache
{
    public readonly struct DbQueryCacheKey : IEquatable<DbQueryCacheKey>, IDbQueryCacheKey
    {
        public readonly struct ExpressionKey : IEquatable<ExpressionKey>
        {
            public struct Builder()
            {
                private static readonly ObjectPool<StringBuilder> _stringBuilderPool = new DefaultObjectPoolProvider().CreateStringBuilderPool();

                private StringBuilder _stringBuilder = _stringBuilderPool.Get();

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly void AddExpression(string? expression)
                {
                    if (expression is null)
                    {
                        _stringBuilder.Append("-1:");
                    }
                    else
                    {
                        _stringBuilder.Append(expression.Length)
                          .Append(':')
                          .Append(expression);
                    }
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public ExpressionKey GetKey()
                {
                    var expr = _stringBuilder.ToString();

                    _stringBuilderPool.Return(_stringBuilder);
                    _stringBuilder = null!;

                    return new ExpressionKey(expr);
                }
            }

            public ExpressionKey(string expression)
            {
                Expression = expression;
            }

            public readonly string Expression;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override bool Equals(object? obj)
            {
                return obj is ExpressionKey other && Equals(other);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(ExpressionKey other)
            {
                return Expression == other.Expression;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int GetHashCode()
            {
                return Expression.GetHashCode();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator ==(ExpressionKey left, ExpressionKey right)
            {
                return left.Equals(right);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator !=(ExpressionKey left, ExpressionKey right)
            {
                return !(left == right);
            }
        }

        public DbQueryCacheKey(Type entityType, 
            ExpressionKey expression,
            string? additionalExpressionData, 
            MethodInfo method, 
            Guid? dependentDbContext)
        {
            EntityType = entityType;
            Expression = expression;
            AdditionalExpressionData = additionalExpressionData;
            Method = method;
            DependentDbContext = dependentDbContext;
        }

        public readonly Type EntityType;
        public readonly ExpressionKey Expression;
        public readonly string? AdditionalExpressionData;
        public readonly MethodInfo Method;
        public readonly Guid? DependentDbContext { get; }

        public override bool Equals(object? obj)
        {
            return obj is DbQueryCacheKey other && Equals(other);
        }

        public bool Equals(DbQueryCacheKey other)
        {
            return EntityType == other.EntityType &&
                   Expression == other.Expression &&
                   AdditionalExpressionData == other.AdditionalExpressionData &&
                   Method == other.Method &&
                   DependentDbContext == other.DependentDbContext;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(EntityType, Expression, AdditionalExpressionData, Method, DependentDbContext);
        }

        public static bool operator ==(DbQueryCacheKey left, DbQueryCacheKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DbQueryCacheKey left, DbQueryCacheKey right)
        {
            return !(left == right);
        }
    }
}