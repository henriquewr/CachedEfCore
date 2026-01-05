using System;

namespace CachedEfCore.Cache
{
    public readonly struct DbQueryCacheKey : IEquatable<DbQueryCacheKey>
    {
        public DbQueryCacheKey(Type entityType, string expression, string? additionalExpressionData, nint delegateFunctionPointer)
        {
            EntityType = entityType;
            Expression = expression;
            AdditionalExpressionData = additionalExpressionData;
            DelegateFunctionPointer = delegateFunctionPointer;
        }

        public readonly Type EntityType;
        public readonly string Expression;
        public readonly string? AdditionalExpressionData;
        public readonly nint DelegateFunctionPointer;

        public override bool Equals(object? obj)
        {
            return obj is DbQueryCacheKey other && Equals(other);
        }

        public bool Equals(DbQueryCacheKey other)
        {
            return EntityType == other.EntityType &&
                   Expression == other.Expression &&
                   AdditionalExpressionData == other.AdditionalExpressionData &&
                   DelegateFunctionPointer == other.DelegateFunctionPointer;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(EntityType, Expression, AdditionalExpressionData, DelegateFunctionPointer);
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