using CachedEfCore.Cache.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CachedEfCore.Cache.Query
{
    internal readonly struct CompiledQueryCacheKey<TParameter, TCachePartition> : IEquatable<CompiledQueryCacheKey<TParameter, TCachePartition>>, IDbQueryCacheKey
    {
        private static readonly EqualityComparer<TParameter> _parameterComparer = EqualityComparer<TParameter>.Default;
        private static readonly EqualityComparer<TCachePartition> _cachePartitionComparer = EqualityComparer<TCachePartition>.Default;

        private readonly long _queryId;
        private readonly IModel _model;
        private readonly TParameter _parameter;
        private readonly TCachePartition _cachePartition;
        private readonly DbContextId? _dependentDbContextId;
        private readonly int _hashCode;

        public CompiledQueryCacheKey(long queryId, IModel model, TParameter parameter, TCachePartition cachePartition, DbContextId? dependentDbContextId)
        {
            _queryId = queryId;
            _model = model;
            _parameter = parameter;
            _cachePartition = cachePartition;
            _dependentDbContextId = dependentDbContextId;

            var parameterHashCode = parameter is null ? 0 : _parameterComparer.GetHashCode(parameter);
            var cachePartitionHashCode = cachePartition is null ? 0 : _cachePartitionComparer.GetHashCode(cachePartition);
            _hashCode = HashCode.Combine(queryId, RuntimeHelpers.GetHashCode(model), parameterHashCode, cachePartitionHashCode, dependentDbContextId);
        }

        public Guid? DependentDbContext => _dependentDbContextId?.InstanceId;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object? obj)
        {
            return obj is CompiledQueryCacheKey<TParameter, TCachePartition> other && Equals(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CompiledQueryCacheKey<TParameter, TCachePartition> other)
        {
            return _queryId == other._queryId &&
                   ReferenceEquals(_model, other._model) &&
                   _parameterComparer.Equals(_parameter, other._parameter) &&
                   _cachePartitionComparer.Equals(_cachePartition, other._cachePartition) &&
                   _dependentDbContextId == other._dependentDbContextId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return _hashCode;
        }

        public static bool operator ==(CompiledQueryCacheKey<TParameter, TCachePartition> left, CompiledQueryCacheKey<TParameter, TCachePartition> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CompiledQueryCacheKey<TParameter, TCachePartition> left, CompiledQueryCacheKey<TParameter, TCachePartition> right)
        {
            return !(left == right);
        }
    }

    internal readonly struct SharedCachePartition
    {
    }
}
