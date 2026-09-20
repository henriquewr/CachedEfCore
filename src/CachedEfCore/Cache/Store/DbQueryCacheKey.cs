using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.ObjectPool;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace CachedEfCore.Cache.Store
{
    public readonly struct DbQueryCacheKey : IEquatable<DbQueryCacheKey>, IDbQueryCacheKey
    {
        public DbQueryCacheKey(Type entityType, 
            ExpressionKey expression,
            string? additionalExpressionData, 
            MethodInfo method,
            DbContextId? dependentDbContext)
        {
            EntityType = entityType;
            Expression = expression;
            AdditionalExpressionData = additionalExpressionData;
            Method = method;
            DependentDbContext = dependentDbContext;
        }

        public readonly Type EntityType { get; }
        public readonly ExpressionKey Expression { get; }
        public readonly string? AdditionalExpressionData { get; }
        public readonly MethodInfo Method { get; }
        public readonly DbContextId? DependentDbContext { get; }

        public string Stringify()
        {
            var keyBuilder = new SimpleKeyBuilder();

            keyBuilder.AddKey(EntityType.AssemblyQualifiedName);
            keyBuilder.AddKey(Expression.Stringify());
            keyBuilder.AddKey(AdditionalExpressionData);
            keyBuilder.AddKey($"{Method.DeclaringType?.AssemblyQualifiedName}:{Method}");
            keyBuilder.AddKey(DependentDbContext?.ToString());

            var key = keyBuilder.GetKey();

            return key;
        }

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

        private readonly struct SimpleKeyBuilder()
        {
            private static readonly ObjectPool<StringBuilder> _stringBuilderPool = new DefaultObjectPoolProvider().CreateStringBuilderPool();

            private readonly StringBuilder _stringBuilder = _stringBuilderPool.Get();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void AddKey(string? expression)
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
            public string GetKey()
            {
                var key = _stringBuilder.ToString();
                _stringBuilderPool.Return(_stringBuilder);

                return key;
            }
        }

        public readonly struct ExpressionKey : IEquatable<ExpressionKey>
        {
            public readonly struct Builder()
            {
                private readonly SimpleKeyBuilder _keyBuilder = new SimpleKeyBuilder();

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly void AddExpression(string? expression)
                {
                    _keyBuilder.AddKey(expression);
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public ExpressionKey GetKey()
                {
                    var expr = _keyBuilder.GetKey();
                    return new ExpressionKey(expr);
                }
            }

            public ExpressionKey(string expression)
            {
                Expression = expression;
            }

            public readonly string Expression { get; }

            public string Stringify()
            {
                return Expression;
            }

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
    }
}