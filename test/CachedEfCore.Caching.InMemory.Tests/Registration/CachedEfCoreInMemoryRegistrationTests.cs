using CachedEfCore.Caching.InMemory.Configuration;
using CachedEfCore.Caching.InMemory.DbContextOptionExtensions;
using CachedEfCore.Caching.Specification.Tests.Common;
using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlServer.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;

namespace CachedEfCore.Caching.InMemory.Tests.Registration
{
    public class CachedEfCoreInMemoryRegistrationTests
    {
        private static IServiceProvider Register(IMemoryCache memoryCache)
        {
            var services = new ServiceCollection();

            services.AddCachedEfCore();

            services.AddDbContextPool<TestDbContext>(options =>
            {
                options.UseSqlServer();

                options.EnableServiceProviderCaching(true);

                options.UseCachedEfCore(cachedEfCoreOptions =>
                {
                    cachedEfCoreOptions.UseInMemoryCacheStore(inMemoryCacheOptions =>
                    {
                        inMemoryCacheOptions.UseInstance(memoryCache);
                    });
                    cachedEfCoreOptions.UseSqlServer();
                });
            });

            var builtServiceProvider = services.BuildServiceProvider();

            return builtServiceProvider;
        }

        [Fact]
        public void Should_Not_Be_Same_Internal_Service_Provider_When_Not_Same_Instance()
        {
            var firstCache = new MemoryCache(new MemoryCacheOptions());
            var secondCache = new MemoryCache(new MemoryCacheOptions());

            // warning 'Microsoft.EntityFrameworkCore.Infrastructure.ManyServiceProvidersCreatedWarning'
            for (int i = 0; i < 100; i++)
            {
                var firstProvider = Register(firstCache);
                var secondProvider = Register(secondCache);

                using var firstProviderScope = firstProvider.CreateScope();
                using var secondProviderScope = secondProvider.CreateScope();

                var firstDbContext = firstProviderScope.ServiceProvider.GetRequiredService<TestDbContext>();
                var secondDbContext = secondProviderScope.ServiceProvider.GetRequiredService<TestDbContext>();

                var firstDbContextInMemoryCacheExtension = firstDbContext.GetService<IDbContextOptions>()
                        .FindExtension<CachedEfCoreInMemoryCacheExtension>()!;

                var secondDbContextInMemoryCacheExtension = secondDbContext.GetService<IDbContextOptions>()
                        .FindExtension<CachedEfCoreInMemoryCacheExtension>()!;

                Assert.Same(firstCache, firstDbContextInMemoryCacheExtension.MemoryCache);
                Assert.Same(secondCache, secondDbContextInMemoryCacheExtension.MemoryCache);
            }
        }
    }
}
