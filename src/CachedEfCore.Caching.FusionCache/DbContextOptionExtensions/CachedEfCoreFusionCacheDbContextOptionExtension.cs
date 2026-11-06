using CachedEfCore.Caching.FusionCache.Configuration;
using CachedEfCore.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace CachedEfCore.Caching.FusionCache.DbContextOptionExtensions
{
    public class CachedEfCoreFusionCacheExtension : IDbContextOptionsExtension
    {
        public IFusionCache FusionCache { get; private set; } = null!;
        public void WithFusionCacheInstance(IFusionCache fusionCache)
        {
            FusionCache = fusionCache;
        }

        private readonly Type _contextType;
        private readonly DbContextOptionsBuilder _dbContextOptionsBuilder;
        private readonly List<CachedEfCoreService> _services = CachedEfCoreCachingFusionCacheCoreServices.GetCoreServices().ToList();

        private IEnumerable<CachedEfCoreService> _orderedServices => _services.OrderBy(x => x.ServiceDescriptor.ServiceKey);

        public void AddService(CachedEfCoreService cachedEfCoreService)
        {
            _services.Add(cachedEfCoreService);
        }

        public void ReplaceService(CachedEfCoreService cachedEfCoreService)
        {
            var index = _services.FindIndex(x => x.ServiceDescriptor.ServiceType == cachedEfCoreService.ServiceDescriptor.ServiceType);
            if (index != -1)
            {
                _services[index] = cachedEfCoreService;
            }
            else
            {
                throw new InvalidOperationException($"Service {cachedEfCoreService.ServiceDescriptor.ServiceType} not found to replace.");
            }
        }

        public void AddOrReplaceService(CachedEfCoreService cachedEfCoreService)
        {
            var index = _services.FindIndex(x => x.ServiceDescriptor.ServiceType == cachedEfCoreService.ServiceDescriptor.ServiceType);
            if (index != -1)
            {
                _services[index] = cachedEfCoreService;
            }
            else
            {
                _services.Add(cachedEfCoreService);
            }
        }

        public DbContextOptionsExtensionInfo Info { get; }

        public CachedEfCoreFusionCacheExtension(DbContextOptionsBuilder dbContextOptionsBuilder)
        {
            _dbContextOptionsBuilder = dbContextOptionsBuilder;
            _contextType = dbContextOptionsBuilder.Options.ContextType;
            Info = new ExtensionInfo(this);
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
            if (FusionCache is null)
            {
                throw new InvalidOperationException($"You must configure the FusionCache registration by calling {nameof(CachedEfCoreCachingFusionCacheOptionsBuilder)}.{nameof(CachedEfCoreCachingFusionCacheOptionsBuilder.UseInstance)}" );
            }
        }

        private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
        {
            public ExtensionInfo(CachedEfCoreFusionCacheExtension extension) : base(extension)
            {
            }

            public override bool IsDatabaseProvider => false;

            public override string LogFragment => " CachedEfCore FusionCache ";

            private int? HashCodetServiceProvider;

            public override int GetServiceProviderHashCode()
            {
                if (HashCodetServiceProvider.HasValue)
                {
                    return HashCodetServiceProvider.Value;
                }

                var extension = (CachedEfCoreFusionCacheExtension)this.Extension;

                HashCode hashCode = new HashCode();

                foreach (var item in extension._orderedServices)
                {
                    if (item.GetServiceProviderHashCode is null)
                    {
                        continue;
                    }

                    hashCode.Add(item.GetServiceProviderHashCode(item));
                }

                HashCodetServiceProvider = hashCode.ToHashCode();

                return HashCodetServiceProvider.Value;
            }

            public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            {
                debugInfo["CachedEfCore_FusionCache:Context"] = ((CachedEfCoreFusionCacheExtension)Extension)._contextType.FullName!;
            }

            public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            {
                if (other.Extension is not CachedEfCoreFusionCacheExtension cachedEfCoreFusionCacheExtension)
                {
                    return false;
                }

                var extension = (CachedEfCoreFusionCacheExtension)this.Extension;

                foreach (var service in extension._services)
                {
                    if (service.ShouldUseSameServiceProvider is null)
                    {
                        continue;
                    }

                    var otherServices = cachedEfCoreFusionCacheExtension._services.Where(x => x.ServiceDescriptor.ServiceType == service.ServiceDescriptor.ServiceType);

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
