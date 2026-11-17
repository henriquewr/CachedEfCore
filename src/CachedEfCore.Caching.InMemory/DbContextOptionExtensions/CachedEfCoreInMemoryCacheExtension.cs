using CachedEfCore.Caching.InMemory.Configuration;
using CachedEfCore.Caching.InMemory.Store;
using CachedEfCore.Caching.Store;
using CachedEfCore.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CachedEfCore.Caching.InMemory.DbContextOptionExtensions
{
    public class CachedEfCoreInMemoryCacheExtension : IDbContextOptionsExtension
    {
        public DbQueryCacheInMemoryInternalStore DbQueryCacheInternalStore { get; private set; } = null!;

        private void CreateDbQueryCacheInternalStore()
        {
            DbQueryCacheInternalStore = new DbQueryCacheInMemoryInternalStore(MemoryCache, new InMemoryPendingEntitiesImplementation(MemoryCache, new object()), MemoryCacheEntryOptions);
        }

        public MemoryCacheEntryOptions MemoryCacheEntryOptions { get; private set; } = CachedEfCoreCachingInMemoryOptionsDefaults.DefaultMemoryCacheEntryOptions;
        public void WithMemoryCacheEntryOptions(MemoryCacheEntryOptions memoryCacheEntryOptions)
        {
            MemoryCacheEntryOptions = memoryCacheEntryOptions;
            CreateDbQueryCacheInternalStore();
        }

        public IMemoryCache MemoryCache { get; private set; } = new MemoryCache(new MemoryCacheOptions());
        public void WithMemoryCacheInstance(IMemoryCache memoryCache)
        {
            MemoryCache = memoryCache;
            CreateDbQueryCacheInternalStore();
        }

        private readonly Type _contextType;
        private readonly DbContextOptionsBuilder _dbContextOptionsBuilder;
        private readonly List<CachedEfCoreService> _services = CachedEfCoreCachingInMemoryCoreServices.GetCoreServices().ToList();

        private IEnumerable<CachedEfCoreService> _orderedServices => _services.OrderBy(x => x.ServiceDescriptor.ServiceKey);

        public DbContextOptionsExtensionInfo Info { get; }

        public CachedEfCoreInMemoryCacheExtension(DbContextOptionsBuilder dbContextOptionsBuilder)
        {
            _dbContextOptionsBuilder = dbContextOptionsBuilder;
            _contextType = dbContextOptionsBuilder.Options.ContextType;
            Info = new ExtensionInfo(this);

            CreateDbQueryCacheInternalStore();
        }

        public void ApplyServices(IServiceCollection services)
        {
            foreach (var item in _services)
            {
                services.TryAdd(item.ServiceDescriptor);
            }
        }

        public IDbContextOptionsExtension ApplyDefaults(IDbContextOptions options)
        {
            return this;
        }

        public void Validate(IDbContextOptions options)
        {
        }

        private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
        {
            public ExtensionInfo(CachedEfCoreInMemoryCacheExtension extension) : base(extension)
            {
            }

            public override bool IsDatabaseProvider => false;

            public override string LogFragment => " CachedEfCore InMemory ";

            private int? HashCodeServiceProvider;

            public override int GetServiceProviderHashCode()
            {
                if (HashCodeServiceProvider.HasValue)
                {
                    return HashCodeServiceProvider.Value;
                }

                var extension = (CachedEfCoreInMemoryCacheExtension)this.Extension;

                HashCode hashCode = new HashCode();

                foreach (var item in extension._orderedServices)
                {
                    if (item.GetServiceProviderHashCode is null)
                    {
                        continue;
                    }

                    hashCode.Add(item.GetServiceProviderHashCode(item));
                }

                HashCodeServiceProvider = hashCode.ToHashCode();

                return HashCodeServiceProvider.Value;
            }

            public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            {
                debugInfo["CachedEfCore_InMemory:Context"] = ((CachedEfCoreInMemoryCacheExtension)Extension)._contextType.FullName!;
            }

            public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            {
                if (other.Extension is not CachedEfCoreInMemoryCacheExtension cachedEfCoreInMemoryCacheExtension)
                {
                    return false;
                }

                var extension = (CachedEfCoreInMemoryCacheExtension)this.Extension;

                foreach (var service in extension._services)
                {
                    if (service.ShouldUseSameServiceProvider is null)
                    {
                        continue;
                    }

                    var otherServices = cachedEfCoreInMemoryCacheExtension._services.Where(x => x.ServiceDescriptor.ServiceType == service.ServiceDescriptor.ServiceType);

                    if (!otherServices.Any())
                    {
                        continue;
                    }

                    var arg = new ShouldUseSameServiceProviderArgs
                    {
                        ThisService = service,
                        OtherServices = otherServices
                    };

                    if (!service.ShouldUseSameServiceProvider(arg))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
