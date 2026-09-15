using BenchmarkDotNet.Attributes;
using CachedEfCore.Caching.InMemory.Configuration;
using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlServer.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VSDiagnostics;
using System;
using System.Security.Cryptography;
using CachedEfCore.Caching.InMemory.Benchmarks.TestContainer;
using Iced.Intel;
using CachedEfCore.Cache.Helper;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Infrastructure;
using CachedEfCore.Cache.Store;
using System.Linq.Expressions;
using CachedEfCore.Cache.KeyGeneration.ExpressionKeyGen;

namespace CachedEfCore.Caching.InMemory.Benchmarks
{
    // For more information on the VS BenchmarkDotNet Diagnosers see https://learn.microsoft.com/visualstudio/profiling/profiling-with-benchmark-dotnet
    [CPUUsageDiagnoser]
    public class Benchmarks
    {
        protected IServiceProvider CreateProvider(string connectionString)
        {
            var services = new ServiceCollection();

            services.AddCachedEfCore();

            services.AddDbContext<TestDbContext>(options =>
            {
                options.UseSqlServer(connectionString);

                options.UseCachedEfCore(cachedEfCoreOptions =>
                {
                    cachedEfCoreOptions.UseSqlServer();
                    cachedEfCoreOptions.UseInMemoryCacheStore();
                });
            });
            var builtServiceProvider = services.BuildServiceProvider();
            return builtServiceProvider;
        }
        
        public class TestDbContext : DbContext
        {
            public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
            {

            }

            public DbSet<TestEntity> TestEntities => Set<TestEntity>();
        }

        public class TestEntity
        {
            public int Id { get; set; }
        }
        private SqlServerTestContainer _sqlServerTestContainer;
        private IServiceScope _scope;
        private TestDbContext _dbContext;
        [GlobalSetup]
        public void Setup()
        {
            _sqlServerTestContainer = new SqlServerTestContainer();
            _sqlServerTestContainer.InitializeAsync().AsTask().Wait();

            var serviceProvider = CreateProvider(_sqlServerTestContainer.ConnectionString);
            _scope = serviceProvider.CreateScope();
            _dbContext = _scope.ServiceProvider.GetService<TestDbContext>();
            _dbContext.Database.EnsureCreated();
        }

        [Benchmark]
        public async Task TestdbQueryCacheHelper1()
        {
            var dbQueryCacheHelper = _scope.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();

            var id = 2;
            var value = await dbQueryCacheHelper.GetOrAddAsync(typeof(TestEntity), _dbContext, async () =>
            {
               return await _dbContext.TestEntities.Where(x => x.Id == id).ToListAsync();
            }, [id]);

        }

        [Benchmark]
        public async Task TestdbQueryCacheHelper2()
        {
            var dbQueryCacheHelper = _scope.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();
            var serachId = 4;
            Expression<Func<TestEntity, bool>> id = x => x.Id == serachId;
            var value = await dbQueryCacheHelper.GetOrAddAsync(typeof(TestEntity), _dbContext, async () =>
            {
                return await _dbContext.TestEntities.Where(id).ToListAsync();
            }, [id]);

        }

        [Benchmark]
        public KeyGeneratorResult<String> TestkeyGeneratorVisitor1()
        {
            var serachId = 4;
            Expression<Func<TestEntity, bool>> id = x => EF.Functions.DateDiffYear(DateTime.Now, DateTime.Now) != 3 || x.Id == (serachId + 2) / 1;
            var keyGeneratorVisitor = _dbContext.GetService<KeyGeneratorVisitor>();
            return keyGeneratorVisitor.ExpressionToString(id);
        }
    }
}
