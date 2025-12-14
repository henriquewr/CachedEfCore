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
            int hash = HashCode.Combine(EntityType, Expression, DelegateFunctionPointer);
            if (AdditionalExpressionData != null)
            {
                hash = HashCode.Combine(hash, AdditionalExpressionData);
            }
            return hash;
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