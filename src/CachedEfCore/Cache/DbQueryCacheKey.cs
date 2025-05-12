using System;

namespace CachedEfCore.Cache
{
    public readonly struct DbQueryCacheKey : IEquatable<DbQueryCacheKey>
    {
        public DbQueryCacheKey(string entityFullName, string expression, string? additionalExpressionData, string queryStructure)
        {
            EntityFullName = entityFullName;
            Expression = expression;
            AdditionalExpressionData = additionalExpressionData;
            QueryStructure = queryStructure;
        }

        public readonly string EntityFullName;
        public readonly string Expression;
        public readonly string? AdditionalExpressionData;
        public readonly string QueryStructure;

        public override bool Equals(object? obj)
        {
            return obj is DbQueryCacheKey other && Equals(other);
        }

        public bool Equals(DbQueryCacheKey other)
        {
            return EntityFullName == other.EntityFullName &&
                   Expression == other.Expression &&
                   AdditionalExpressionData == other.AdditionalExpressionData &&
                   QueryStructure == other.QueryStructure;
        }

        public override int GetHashCode()
        {
            int hash = HashCode.Combine(EntityFullName, Expression, QueryStructure);
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