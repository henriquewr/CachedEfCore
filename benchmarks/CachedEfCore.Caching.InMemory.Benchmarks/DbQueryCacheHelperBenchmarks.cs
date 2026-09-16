using BenchmarkDotNet.Attributes;
using CachedEfCore.Cache.Helper;
using CachedEfCore.Caching.InMemory.Benchmarks.TestContainer;
using CachedEfCore.Caching.InMemory.Configuration;
using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlServer.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VSDiagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CachedEfCore.Caching.InMemory.Benchmarks
{
    [CPUUsageDiagnoser]
    [MemoryDiagnoser(true)]
    [ShortRunJob]
    public class DbQueryCacheHelperBenchmarks
    {
        protected IServiceProvider CreateProvider(string connectionString)
        {
            var services = new ServiceCollection();

            services.AddCachedEfCore();

            services.AddDbContext<TestDbContext>(options =>
            {
                //options.UseInMemoryDatabase("Test");

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
        private IDbQueryCacheHelper _dbQueryCacheHelper;

        [GlobalSetup]
        public void Setup()
        {
            _sqlServerTestContainer = new SqlServerTestContainer();
            _sqlServerTestContainer.InitializeAsync().AsTask().Wait();

            var serviceProvider = CreateProvider(_sqlServerTestContainer.ConnectionString);
            _scope = serviceProvider.CreateScope();
            _dbContext = _scope.ServiceProvider.GetService<TestDbContext>();
            _dbContext.Database.EnsureCreated();

            _dbQueryCacheHelper = _scope.ServiceProvider.GetRequiredService<IDbQueryCacheHelper>();
        }

        [Benchmark]
        public async Task<List<TestEntity>> Without_Cache_Simple_Int_Id()
        {
            return await _dbContext.TestEntities.Where(x => x.Id == 2).ToListAsync();
        }

        [Benchmark]
        public async Task<List<TestEntity>> GetOrAddAsync_With_Simple_Int_Id()
        {
            var id = 2;
            var value = await _dbQueryCacheHelper.GetOrAddAsync(typeof(TestEntity), _dbContext, async () =>
            {
               return await _dbContext.TestEntities.Where(x => x.Id == id).ToListAsync();
            }, [id]);

            return value;
        }

        [Benchmark]
        public async Task<List<TestEntity>> GetOrAddAsync_With_Expression()
        {
            var id = 4;
            Expression<Func<TestEntity, bool>> where = x => x.Id == id;
            var value = await _dbQueryCacheHelper.GetOrAddAsync(typeof(TestEntity), _dbContext, async () =>
            {
                return await _dbContext.TestEntities.Where(where).ToListAsync();
            }, [where]);

            return value;
        }

        [Benchmark]
        public async Task<List<TestEntity>> GetOrAddAsync_With_Complex_Expression()
        {
            var id = 4;
            Expression<Func<TestEntity, bool>> where = x => EF.Functions.DateDiffYear(DateTime.Now, DateTime.Now) != 3 || x.Id == (id + 2) / 1;

            var value = await _dbQueryCacheHelper.GetOrAddAsync(typeof(TestEntity), _dbContext, async () =>
            {
                return await _dbContext.TestEntities.Where(where).ToListAsync();
            }, [where]);

            return value;
        }


        [Benchmark]
        public List<TestEntity> GetOrAdd_With_Simple_Int_Id()
        {
            var id = 2;
            var value = _dbQueryCacheHelper.GetOrAdd(typeof(TestEntity), _dbContext, () =>
            {
                return _dbContext.TestEntities.Where(x => x.Id == id).ToList();
            }, [id]);

            return value;
        }

        [Benchmark]
        public List<TestEntity> GetOrAdd_With_Expression()
        {
            var id = 4;
            Expression<Func<TestEntity, bool>> where = x => x.Id == id;
            var value = _dbQueryCacheHelper.GetOrAdd(typeof(TestEntity), _dbContext, () =>
            {
                return _dbContext.TestEntities.Where(where).ToList();
            }, [where]);

            return value;
        }

        [Benchmark]
        public List<TestEntity> GetOrAdd_With_Complex_Expression()
        {
            var id = 4;
            Expression<Func<TestEntity, bool>> where = x => EF.Functions.DateDiffYear(DateTime.Now, DateTime.Now) != 3 || x.Id == (id + 2) / 1;

            var value = _dbQueryCacheHelper.GetOrAdd(typeof(TestEntity), _dbContext, () =>
            {
                return _dbContext.TestEntities.Where(where).ToList();
            }, [where]);

            return value;
        }
    }
}
