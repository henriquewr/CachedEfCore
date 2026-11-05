using CachedEfCore.Configuration;
using CachedEfCore.DbContextOptionExtensions;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using ZiggyCreatures.Caching.Fusion;

namespace CachedEfCore.Caching.FusionCache.Configuration
{
    public class CachedEfCoreCachingFusionCacheOptionsBuilder
    {
        private readonly CachedEfCoreDbContextOptionExtension _cachedEfCoreExtension;

        private bool _useInstanceCalled;

        public CachedEfCoreCachingFusionCacheOptionsBuilder(CachedEfCoreDbContextOptionExtension cachedEfCoreExtension)
        {
            _cachedEfCoreExtension = cachedEfCoreExtension;
        }

        public CachedEfCoreCachingFusionCacheOptionsBuilder UseInstance(IFusionCache fusionCacheInstance)
        {
            _cachedEfCoreExtension.AddOrReplaceService(new CachedEfCoreService
            {
                ServiceDescriptor = ServiceDescriptor.Singleton<IFusionCache>(fusionCacheInstance),
                GetServiceProviderHashCode = static thisService => ((IFusionCache)thisService.Options!).GetHashCode(),
                ShouldUseSameServiceProvider = static args => ReferenceEquals((IFusionCache)args.ThisService.Options!, (IFusionCache)args.OtherServices.Single().Options!),
                Options = fusionCacheInstance,
            });

            _useInstanceCalled = true;

            return this;
        }

        private static int DescriptorIdentityHash(ServiceDescriptor serviceDescriptor)
        {
            return HashCode.Combine(
                serviceDescriptor.ServiceType,
                serviceDescriptor.Lifetime,
                serviceDescriptor.ImplementationInstance is null ? 0 : RuntimeHelpers.GetHashCode(serviceDescriptor.ImplementationInstance),
                serviceDescriptor.ImplementationFactory is null ? 0 : HashCode.Combine(serviceDescriptor.ImplementationFactory.Method, serviceDescriptor.ImplementationFactory.Target is null ? 0 : RuntimeHelpers.GetHashCode(serviceDescriptor.ImplementationFactory.Target)),
                serviceDescriptor.ImplementationType);
        }

        private static bool SameRegistration(ServiceDescriptor a, ServiceDescriptor b)
        {
            return a.ServiceType == b.ServiceType
                && a.Lifetime == b.Lifetime
                && ReferenceEquals(a.ImplementationInstance, b.ImplementationInstance)
                && a.ImplementationType == b.ImplementationType
                && (a.ImplementationFactory == b.ImplementationFactory
                    || (a.ImplementationFactory?.Method == b.ImplementationFactory?.Method
                        && ReferenceEquals(a.ImplementationFactory?.Target, b.ImplementationFactory?.Target)));
        }

        public void Validate()
        {
            if (!_useInstanceCalled)
            {
                throw new InvalidOperationException($"You must configure the FusionCache registration by calling {nameof(UseInstance)}");
            }
        }
    }
}
