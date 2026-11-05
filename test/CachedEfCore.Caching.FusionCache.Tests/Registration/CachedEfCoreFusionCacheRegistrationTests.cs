using CachedEfCore.Caching.FusionCache.Configuration;
using CachedEfCore.Caching.Specification.Tests.Common;
using CachedEfCore.DependencyInjection;
using CachedEfCore.PostgreSQL.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace CachedEfCore.Caching.FusionCache.Tests.Registration
{
    public class CachedEfCoreFusionCacheRegistrationTests
    {

        private static IServiceProvider Register(IFusionCache fusionCache)
        {
            var services = new ServiceCollection();

            services.AddCachedEfCore();

            services.AddDbContextPool<TestDbContext>(options =>
            {
                options.UseNpgsql();

                options.EnableServiceProviderCaching(true);

                options.UseCachedEfCore(cachedEfCoreOptions =>
                {
                    cachedEfCoreOptions.UseFusionCacheStore(fusionCacheOptions =>
                    {
                        fusionCacheOptions.UseInstance(fusionCache);
                    });
                    cachedEfCoreOptions.UsePostgreSql();
                });
            });

            var builtServiceProvider = services.BuildServiceProvider();

            return builtServiceProvider;
        }

        [Fact]
        public void Should_Not_Be_Same_Internal_Service_Provider_When_Not_Same_Instance()
        {
            var firstCache = new ZiggyCreatures.Caching.Fusion.FusionCache(new FusionCacheOptions { CacheName = "First" });
            var secondCache = new ZiggyCreatures.Caching.Fusion.FusionCache(new FusionCacheOptions { CacheName = "Second" });

            // warning 'Microsoft.EntityFrameworkCore.Infrastructure.ManyServiceProvidersCreatedWarning'
            for (int i = 0; i < 100; i++)
            {
                var firstProvider = Register(firstCache);
                var secondProvider = Register(secondCache);

                using var firstProviderScope = firstProvider.CreateScope();
                using var secondProviderScope = secondProvider.CreateScope();

                var firstDbContext = firstProviderScope.ServiceProvider.GetRequiredService<TestDbContext>();
                var secondDbContext = secondProviderScope.ServiceProvider.GetRequiredService<TestDbContext>();

                var firstDbContextCache = firstDbContext.GetService<IFusionCache>();
                var secondDbContextCache = secondDbContext.GetService<IFusionCache>();

                Assert.Same(firstCache, firstDbContextCache);
                Assert.Same(secondCache, secondDbContextCache);
            }
        }
    }
}
