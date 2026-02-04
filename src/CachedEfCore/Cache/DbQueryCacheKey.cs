using System;

namespace CachedEfCore.Cache
{
    public readonly struct DbQueryCacheKey : IEquatable<DbQueryCacheKey>, IDbQueryCacheKey
    {
        public DbQueryCacheKey(Type entityType, string expression, string? additionalExpressionData, nint delegateFunctionPointer, Guid? dependentDbContext)
        {
            EntityType = entityType;
            Expression = expression;
            AdditionalExpressionData = additionalExpressionData;
            DelegateFunctionPointer = delegateFunctionPointer;
            DependentDbContext = dependentDbContext;
        }

        public readonly Type EntityType;
        public readonly string Expression;
        public readonly string? AdditionalExpressionData;
        public readonly nint DelegateFunctionPointer;
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
                   DelegateFunctionPointer == other.DelegateFunctionPointer &&
                   DependentDbContext == other.DependentDbContext;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(EntityType, Expression, AdditionalExpressionData, DelegateFunctionPointer, DependentDbContext);
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