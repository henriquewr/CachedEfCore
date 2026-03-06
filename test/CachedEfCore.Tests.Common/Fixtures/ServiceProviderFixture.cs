using Microsoft.Extensions.DependencyInjection;

namespace CachedEfCore.Tests.Common.Fixtures
{
    public class ServiceProviderFixture
    {
        public IServiceProvider CreateProvider(Action<IServiceCollection> configureProvider)
        {
            var services = new ServiceCollection();

            configureProvider(services);

            var builtServiceProvider = services.BuildServiceProvider();

            return builtServiceProvider;
        }
    }
}
