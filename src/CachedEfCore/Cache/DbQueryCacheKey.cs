using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CachedEfCore.Cache
{
    public readonly struct DbQueryCacheKey : IEquatable<DbQueryCacheKey>, IDbQueryCacheKey
    {
        public readonly struct ExpressionKey : IEquatable<ExpressionKey>
        {
            public struct Builder()
            {
                private readonly HashCode HashCode = new();
                private string Expression = "";

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void AddExpression(string? expression)
                {
                    HashCode.Add(expression);

                    Expression += expression;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public readonly ExpressionKey GetKey()
                {
                    return new ExpressionKey(HashCode.ToHashCode(), Expression);
                }
            }

            public ExpressionKey(int hash, string expression)
            {
                Hash = hash;
                Expression = expression;
            }

            public readonly int Hash;
            public readonly string Expression;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override bool Equals(object? obj)
            {
                return obj is ExpressionKey other && Equals(other);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(ExpressionKey other)
            {
                return Hash == other.Hash &&
                       Expression == other.Expression;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int GetHashCode()
            {
                return HashCode.Combine(Hash, Expression);
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