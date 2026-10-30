using CachedEfCore.Cache.EventData;
using CachedEfCore.Cache.Metrics;
using CachedEfCore.Cache.Store;
using CachedEfCore.DependencyManager;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using ZiggyCreatures.Caching.Fusion;

namespace CachedEfCore.Caching.FusionCache.Store
{
    public class DbQueryCacheFusionCacheStore : IDbQueryCacheStore
    {
        private readonly DbContext _dbContext;
        private readonly IFusionCache _fusionCache;
        private readonly IDbQueryCacheMetrics _metrics;

        private readonly string _dbContextCacheTag;

        public event Action<IOnInvalidatingRootEntities>? OnInvalidatingRootEntities;
        public event Action<IOnInvalidatingDependentEntities>? OnInvalidatingDependentEntities;

        private static string GetTransactionCacheTag(in Guid transactionId)
            => $"Tran:{transactionId}";
        private static string GetDbContextCacheTag(in DbContextId dbContextId)
            => $"DbCtx:{dbContextId}";

        private static string GetEntityCacheTag(IEntityType entity)
            => entity.ClrType is null ? $"Ent:{entity.Name}" : GetTypeCacheTag(entity.ClrType);

        private static string GetTypeCacheTag(Type type)
            => $"T:{type.AssemblyQualifiedName}";

        public DbQueryCacheFusionCacheStore(DbContext dbContext, 
            IFusionCache fusionCache,
            IDbQueryCacheMetrics metrics)
        {
            _fusionCache = fusionCache;
            _dbContext = dbContext;
            _dbContextCacheTag = GetDbContextCacheTag(dbContext.ContextId);
            _metrics = metrics;
        }

        private sealed record class FusionCacheKey(IDbQueryCacheKey Key, Guid? TransactionId)
        {
            public override string ToString()
            {
                var keyString = Key.Stringify();

                var keyStringLength = keyString.Length;
                var lengthDigits = keyStringLength == 0 ? 1 : (int)Math.Log10(keyStringLength) + 1;

                var createdKey = string.Create(
                    (TransactionId.HasValue ? 36 : 1) + 1 + lengthDigits + 1 + keyString.Length,
                    (TransactionId, KeyString: keyString),
                    static (span, state) =>
                    {
                        var written = 0;
                        if (state.TransactionId.HasValue)
                        {
                            state.TransactionId.Value.TryFormat(span, out written);
                        }
                        else
                        {
                            span[written++] = '_';
                        }
                        span[written++] = '|';
                        state.KeyString.Length.TryFormat(span.Slice(written), out var keyStringLengthWritten, provider: CultureInfo.InvariantCulture);
                        written += keyStringLengthWritten;
                        span[written++] = '|';
                        state.KeyString.AsSpan().CopyTo(span.Slice(written));
                    });

                Debug.Assert(createdKey == $"{(TransactionId.HasValue ? TransactionId.Value.ToString() : "_")}|{keyString.Length}|{keyString}");

                return createdKey;
            }
        }

        // we skip distributed cache and notifications, as no value dependending on the DbContext reaches L2
        private static readonly FusionCacheEntryOptions _resetOptions = new FusionCacheEntryOptions().SetSkipDistributedCache(true, true);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Reset()
            => _fusionCache.RemoveByTag(_dbContextCacheTag, _resetOptions);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueTask ResetAsync(CancellationToken cancellationToken = default)
            => _fusionCache.RemoveByTagAsync(_dbContextCacheTag, _resetOptions, token: cancellationToken);

        public void Dispose()
        {
            Reset();

            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await ResetAsync().ConfigureAwait(false);

            GC.SuppressFinalize(this);
        }

        ~DbQueryCacheFusionCacheStore()
        {
            try
            {
                Reset();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public void ResetState() => Reset();

        public async Task ResetStateAsync(CancellationToken cancellationToken = default)
        {
            await ResetAsync(cancellationToken).ConfigureAwait(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAllByTransactionId(Guid transactionId)
        {
            _fusionCache.RemoveByTag(GetTransactionCacheTag(transactionId));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAllDbContextDependent(DbContextId dbContextId)
        {
            _fusionCache.RemoveByTag(GetDbContextCacheTag(dbContextId));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAll()
        {
            _fusionCache.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveRootEntities(HashSet<IEntityType> entitiesToRemove, bool fireEvent = true)
        {
            if (fireEvent && OnInvalidatingRootEntities is not null)
            {
                OnInvalidatingRootEntities.Invoke(new OnInvalidatingRootEntities
                {
                    Entities = entitiesToRemove,
                    DbContext = _dbContext
                });
            }

            var typesToRemove = new HashSet<IEntityType>();

            var dependencyManager = _dbContext.GetService<EntityDependency>();

            foreach (var typeToRemove in entitiesToRemove)
            {
                typesToRemove.UnionWith(dependencyManager.GetUpperRelatedEntities(typeToRemove));
            }

            RemoveDependentEntities(typesToRemove, fireEvent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveDependentEntities(HashSet<IEntityType> entitiesToRemove, bool fireEvent = true)
        {
            if (fireEvent && OnInvalidatingDependentEntities is not null)
            {
                OnInvalidatingDependentEntities.Invoke(new OnInvalidatingDependentEntities
                {
                    Entities = entitiesToRemove,
                    DbContext = _dbContext
                });
            }

            var tags = entitiesToRemove.Select(GetEntityCacheTag);

            _fusionCache.RemoveByTag(tags);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (bool SkipDistributedAndNotifications, string[] Tags) GetMetadata(Type rootEntityType, FusionCacheKey cacheKey)
        {
            if (ShoudSkipDistributed(cacheKey))
            {
                // we don't bother to check if dataToCache is null to prevent calling l2 cache on every dispose/reset

                if (cacheKey.TransactionId.HasValue)
                {
                    return (true, [_dbContextCacheTag, GetTransactionCacheTag(cacheKey.TransactionId.Value), GetTypeCacheTag(rootEntityType)]);
                }

                return (true, [_dbContextCacheTag, GetTypeCacheTag(rootEntityType)]);
            }
            else
            {
                return (false, [GetTypeCacheTag(rootEntityType)]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShoudSkipDistributed(FusionCacheKey cacheKey)
            => cacheKey.TransactionId.HasValue || cacheKey.Key.DependentDbContext.HasValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddToCache<T>(Type rootEntityType, IDbQueryCacheKey key, T? dataToCache)
        {
            var transactionId = _dbContext.Database.CurrentTransaction?.TransactionId;

            var fusionCacheKey = new FusionCacheKey(key, transactionId);

            var stringKey = fusionCacheKey.ToString();

            var metadata = GetMetadata(rootEntityType, fusionCacheKey);

            if (metadata.SkipDistributedAndNotifications)
            {
                _fusionCache.Set<T?>(stringKey, dataToCache, options =>
                {
                    options.SetSkipDistributedCache(true, true);
                }, tags: metadata.Tags);
            }
            else
            {
                _fusionCache.Set<T?>(stringKey, dataToCache, tags: metadata.Tags);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? GetCached<T>(IDbQueryCacheKey key)
        {
            var transactionId = _dbContext.Database.CurrentTransaction?.TransactionId;

            var fusionCacheKey = new FusionCacheKey(key, transactionId);

            var stringKey = fusionCacheKey.ToString();

            var maybeFromCache = ShoudSkipDistributed(fusionCacheKey)
                ? _fusionCache.TryGet<T>(stringKey, options => options.SetSkipDistributedCache(true, true))
                : _fusionCache.TryGet<T>(stringKey);

            if (maybeFromCache.HasValue)
            {
                _metrics.ReportCacheHit();
                return maybeFromCache.Value;
            }

            _metrics.ReportCacheMiss();
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetOrAdd<T>(Type rootEntityType, IDbQueryCacheKey key, Func<T> create)
        {
            var transactionId = _dbContext.Database.CurrentTransaction?.TransactionId;

            var fusionCacheKey = new FusionCacheKey(key, transactionId);

            var stringKey = fusionCacheKey.ToString();

            var isCacheHit = true;

            var metadata = GetMetadata(rootEntityType, fusionCacheKey);

            var result = metadata.SkipDistributedAndNotifications
                ? _fusionCache.GetOrSet<T>(stringKey, CreateFunc, options => options.SetSkipDistributedCache(true, true), tags: metadata.Tags)
                : _fusionCache.GetOrSet<T>(stringKey, CreateFunc, tags: metadata.Tags);

            if (isCacheHit)
            {
                _metrics.ReportCacheHit();
            }

            return result;

            T CreateFunc(CancellationToken ct)
            {
                var createdValue = create();
                _metrics.ReportCacheMiss();

                isCacheHit = false;

                return createdValue;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<T> GetOrAddAsync<T>(Type rootEntityType, IDbQueryCacheKey key, Func<Task<T>> create)
        {
            var transactionId = _dbContext.Database.CurrentTransaction?.TransactionId;

            var fusionCacheKey = new FusionCacheKey(key, transactionId);

            var stringKey = fusionCacheKey.ToString();

            var isCacheHit = true;

            var metadata = GetMetadata(rootEntityType, fusionCacheKey);

            var result = metadata.SkipDistributedAndNotifications
                ? await _fusionCache.GetOrSetAsync<T>(stringKey, CreateFunc, options => options.SetSkipDistributedCache(true, true), tags: metadata.Tags).ConfigureAwait(false)
                : await _fusionCache.GetOrSetAsync<T>(stringKey, CreateFunc, tags: metadata.Tags).ConfigureAwait(false);

            if (isCacheHit)
            {
                _metrics.ReportCacheHit();
            }

            return result;

            async Task<T> CreateFunc(CancellationToken ct)
            {
                var createdValue = await create().ConfigureAwait(false);
                _metrics.ReportCacheMiss();

                isCacheHit = false;

                return createdValue;
            }
        }
    }
}
