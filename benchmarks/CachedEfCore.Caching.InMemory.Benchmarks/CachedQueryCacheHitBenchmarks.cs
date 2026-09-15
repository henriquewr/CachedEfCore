using BenchmarkDotNet.Attributes;
using CachedEfCore.Cache.Helper;
using CachedEfCore.Cache.Query;
using CachedEfCore.Caching.InMemory.Configuration;
using CachedEfCore.Configuration;
using CachedEfCore.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CachedEfCore.Caching.InMemory.Benchmarks
{
    public abstract class CachedQueryCacheHitBenchmarkBase
    {
        private const int ProductId = 18_372;

        private IServiceProvider _serviceProvider = null!;
        private readonly List<IServiceScope> _serviceScopes = new();
        private BenchmarkDbContext _dbContext = null!;
        private IDbQueryCacheHelper _cacheHelper = null!;
        private Func<ProductDto?> _queryDatabase = null!;
        private Expression<Func<Product, bool>> _queryExpression = null!;
        private CachedQuery<BenchmarkDbContext, int, ProductDto?> _compiledQuery = null!;

        protected void SetupCache()
        {
            var services = new ServiceCollection();

            services.AddCachedEfCore();
            services.AddDbContextPool<BenchmarkDbContext>(options =>
            {
                options.UseInMemoryDatabase(nameof(CachedQueryCacheHitBenchmarks));
                options.UseCachedEfCore(cachedEfCoreOptions =>
                {
                    cachedEfCoreOptions.UseInMemoryCacheStore();
                    cachedEfCoreOptions.UseGenericProvider();
                });
            });

            _serviceProvider = services.BuildServiceProvider();
            _dbContext = CreateDbContext();
            _cacheHelper = _serviceProvider.GetRequiredService<IDbQueryCacheHelper>();

            _dbContext.Products.Add(new Product
            {
                Id = ProductId,
                Name = "Cached product",
                Price = 123.45m
            });
            _dbContext.SaveChanges();

            _queryExpression = product => product.Id == ProductId;
            _queryDatabase = QueryDatabase;
            _compiledQuery = CachedQuery.For<Product>().Compile(
                (BenchmarkDbContext context, int id) => context.Products
                    .AsNoTracking()
                    .Where(product => product.Id == id)
                    .Select(product => new ProductDto(product.Id, product.Name, product.Price))
                    .SingleOrDefault()
            );

            ExpressionKeyCacheHit();
            CompiledQueryCacheHit();
        }

        protected void CleanupCache()
        {
            foreach (var serviceScope in _serviceScopes)
            {
                serviceScope.Dispose();
            }

            ((IDisposable)_serviceProvider).Dispose();
        }

        protected BenchmarkDbContext CreateDbContext()
        {
            var serviceScope = _serviceProvider.CreateScope();
            _serviceScopes.Add(serviceScope);

            return serviceScope.ServiceProvider.GetRequiredService<BenchmarkDbContext>();
        }

        protected ProductDto? ExpressionKeyCacheHit()
        {
            return ExpressionKeyCacheHit(_dbContext);
        }

        protected ProductDto? ExpressionKeyCacheHit(BenchmarkDbContext dbContext)
        {
            return _cacheHelper.GetOrAdd<ProductDto?, Product>(dbContext, _queryDatabase, _queryExpression);
        }

        protected ProductDto? CompiledQueryCacheHit()
        {
            return CompiledQueryCacheHit(_dbContext);
        }

        protected ProductDto? CompiledQueryCacheHit(BenchmarkDbContext dbContext)
        {
            return _compiledQuery.GetOrAdd(dbContext, ProductId);
        }

        private ProductDto? QueryDatabase()
        {
            return _dbContext.Products
                .AsNoTracking()
                .Where(_queryExpression)
                .Select(product => new ProductDto(product.Id, product.Name, product.Price))
                .SingleOrDefault();
        }
    }

    [MemoryDiagnoser]
    [ShortRunJob]
    public class CachedQueryCacheHitBenchmarks : CachedQueryCacheHitBenchmarkBase
    {
        [GlobalSetup]
        public void Setup()
        {
            SetupCache();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            CleanupCache();
        }

        [Benchmark(Baseline = true)]
        public ProductDto? ExpressionKey()
        {
            return ExpressionKeyCacheHit();
        }

        [Benchmark]
        public ProductDto? CompiledQuery()
        {
            return CompiledQueryCacheHit();
        }
    }

    [MemoryDiagnoser]
    [ShortRunJob]
    public class CachedQueryConcurrentCacheHitBenchmarks : CachedQueryCacheHitBenchmarkBase
    {
        private const int Operations = 4_096;

        private ParallelOptions _parallelOptions = null!;
        private BenchmarkDbContext[] _dbContexts = null!;
        private Action<int> _expressionKeyOperation = null!;
        private Action<int> _compiledQueryOperation = null!;

        [Params(1, 4, 16, 32)]
        public int Threads { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            SetupCache();

            _parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Threads
            };
            _dbContexts = Enumerable.Range(0, Threads).Select(_ => CreateDbContext()).ToArray();
            _expressionKeyOperation = ExecuteExpressionKeyOperations;
            _compiledQueryOperation = ExecuteCompiledQueryOperations;
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            CleanupCache();
        }

        [Benchmark(Baseline = true, OperationsPerInvoke = Operations)]
        public void ExpressionKey()
        {
            Parallel.For(0, Threads, _parallelOptions, _expressionKeyOperation);
        }

        [Benchmark(OperationsPerInvoke = Operations)]
        public void CompiledQuery()
        {
            Parallel.For(0, Threads, _parallelOptions, _compiledQueryOperation);
        }

        private void ExecuteExpressionKeyOperations(int worker)
        {
            var dbContext = _dbContexts[worker];

            for (var i = worker; i < Operations; i += Threads)
            {
                ExpressionKeyCacheHit(dbContext);
            }
        }

        private void ExecuteCompiledQueryOperations(int worker)
        {
            var dbContext = _dbContexts[worker];

            for (var i = worker; i < Operations; i += Threads)
            {
                CompiledQueryCacheHit(dbContext);
            }
        }
    }

    public sealed record ProductDto(int Id, string Name, decimal Price);

    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public decimal Price { get; set; }
    }

    public class BenchmarkDbContext : DbContext
    {
        public BenchmarkDbContext(DbContextOptions<BenchmarkDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; } = null!;
    }
}
