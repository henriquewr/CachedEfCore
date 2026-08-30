using CachedEfCore.SqlAnalysis;
using CachedEfCore.SqlServer.Configuration;
using CachedEfCore.SqlServer.SqlAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace CachedEfCore.DependencyInjection.Tests.SqlServer
{
    public class DependencyInjectionSqlServerTests : DependencyInjectionTestBase
    {
        [Fact]
        public void SqlServer_DependencyInjection_Should_Register_CachedEfCore()
        {
            var services = new ServiceCollection();

            services.AddCachedEfCore();

            services.AddDbContext<TestDbContext>(options =>
            {
                options.UseSqlServer();

                options.UseCachedEfCore(cachedEfCoreOptions =>
                {
                    cachedEfCoreOptions.UseSqlServer();

                    cachedEfCoreOptions.ConfigureKeyGeneration(keyGen =>
                    {
                        keyGen.ConfigureNonEvaluableTypes(originals =>
                        {
                            var cloned = originals.ToList();
                            cloned.Add(typeof(object));

                            return cloned;
                        });

                        keyGen.ConfigureJsonSerializer(original =>
                        {
                            var newOptions = new JsonSerializerOptions();
                            return newOptions;
                        });
                    });
                });
            });

            var builtServiceProvider = services.BuildServiceProvider();

            using var scope = builtServiceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var sqlQueryEntityExtractor = dbContext.GetService<ISqlQueryEntityExtractor>();

            Assert.IsType<SqlServerQueryEntityExtractor>(sqlQueryEntityExtractor);

            AssertCachedEfCoreIsRegistred(scope);
        }
    }
}
