using CachedEfCore.Configuration;
using CachedEfCore.DbContextOptionExtensions;
using Microsoft.Extensions.DependencyInjection;

namespace CachedEfCore.Caching.FusionCache.Configuration
{
    public class CachedEfCoreCachingFusionCacheOptionsBuilder
    {
        private readonly CachedEfCoreDbContextOptionExtension _cachedEfCoreExtension;

        private bool _configureRegistrationCalled;

        public CachedEfCoreCachingFusionCacheOptionsBuilder(CachedEfCoreDbContextOptionExtension cachedEfCoreExtension)
        {
            _cachedEfCoreExtension = cachedEfCoreExtension;
        }

        public CachedEfCoreCachingFusionCacheOptionsBuilder ConfigureRegistration(Action<IServiceCollection> configure)
        {
            var services = new ServiceCollection();

            configure(services);

            var newServices = services.Select(x => new CachedEfCoreService
            {
                ServiceDescriptor = x,
                GetServiceProviderHashCode = null,
                ShouldUseSameServiceProvider = null,
                Options = null,
            });

            foreach (var item in newServices)
            {
                _cachedEfCoreExtension.AddOrReplaceService(item);
            }

            _configureRegistrationCalled = true;

            return this;
        }

        public void Validate()
        {
            if (!_configureRegistrationCalled)
            {
                throw new InvalidOperationException($"You must configure the FusionCache registration by calling {nameof(ConfigureRegistration)}");
            }
        }
    }
}
