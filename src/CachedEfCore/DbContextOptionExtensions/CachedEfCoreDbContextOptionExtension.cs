using CachedEfCore.Cache.Store;
using CachedEfCore.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CachedEfCore.DbContextOptionExtensions
{
    public class CachedEfCoreDbContextOptionExtension : IDbContextOptionsExtension
    {
        private readonly Type _contextType;
        private readonly List<CachedEfCoreService> _services = CachedEfCoreCoreServices.GetCoreServices().ToList();

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

        public CachedEfCoreDbContextOptionExtension(Type contextType)
        {
            _contextType = contextType;
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
            if (_services.Any(x => x.ServiceDescriptor.ServiceType == typeof(IDbQueryCacheStore)) == false)
            {
                throw new InvalidOperationException($"The service {nameof(IDbQueryCacheStore)} is required. A common cause is that you did not call any cache provider, e.g., {nameof(CachedEfCoreOptionsBuilder)}.UseInMemoryCacheStore()");
            }
        }

        private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
        {
            public ExtensionInfo(CachedEfCoreDbContextOptionExtension extension) : base(extension)
            {
            }

            public override bool IsDatabaseProvider => false;

            public override string LogFragment => " CachedEfCore ";

            private int? HashCodetServiceProvider;

            public override int GetServiceProviderHashCode()
            {
                if (HashCodetServiceProvider.HasValue)
                {
                    return HashCodetServiceProvider.Value;
                }

                var extension = (CachedEfCoreDbContextOptionExtension)this.Extension;

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
                debugInfo["CachedEfCore:Context"] = ((CachedEfCoreDbContextOptionExtension)Extension)._contextType.FullName!;
            }

            public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            {
                if (other.Extension is not CachedEfCoreDbContextOptionExtension cachedEfCoreDbContextOptionExtension)
                {
                    return false;
                }

                var extension = (CachedEfCoreDbContextOptionExtension)this.Extension;

                foreach (var service in extension._services)
                {
                    if (service.ShouldUseSameServiceProvider is null)
                    {
                        continue;
                    }

                    var otherServices = cachedEfCoreDbContextOptionExtension._services.Where(x => x.ServiceDescriptor.ServiceType == service.ServiceDescriptor.ServiceType);

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
