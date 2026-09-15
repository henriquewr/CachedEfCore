# CachedEfCore
[![Main Build](https://github.com/henriquewr/CachedEfCore/actions/workflows/main.yml/badge.svg)](https://github.com/henriquewr/CachedEfCore/actions/workflows/main.yml)

CachedEfCore is a caching library for entity framework core

The cache of CachedEfCore is always the lastest version of the object cached, the library auto invalidates the cache when some entity related to the cached entity changes state, so is impossible to get an old cache

## **Configuration**

```csharp
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddCachedEfCore();

        // AddDbContextPool or AddDbContext
        services.AddDbContextPool<AppDbContext>(options =>
        {
            options.UseSqlServer();

            options.UseCachedEfCore(cachedEfCoreOptions =>
            {
                cachedEfCoreOptions.UseInMemoryCacheStore();

                // currently only SQL Server has a dedicated implementation, you can use UseGenericProvider for other database providers
                cachedEfCoreOptions.UseSqlServer();
            });
        });
    }
```

## **Full Configuration**
```csharp
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddCachedEfCore();

        // AddDbContextPool or AddDbContext
        services.AddDbContextPool<AppDbContext>(options =>
        {
            options.UseSqlServer();

            options.UseCachedEfCore(cachedEfCoreOptions =>
            {
                cachedEfCoreOptions.UseInMemoryCacheStore();

                // currently only SQL Server has a dedicated implementation, you can use UseGenericProvider for other database providers
                cachedEfCoreOptions.UseSqlServer();

                cachedEfCoreOptions.ConfigureKeyGeneration(keyGen =>
                {
                    keyGen.ConfigureNonEvaluableTypes(originals =>
                    {
                        originals.Add(typeof(SomeType));

                        return originals;
                    });

                    keyGen.ConfigureJsonSerializer(original =>
                    {
                        var newOptions = new JsonSerializerOptions();
                        return newOptions;
                    });
                });
            });
        });
    }
```

## **DbContext**
```
public class YourDbContext : CachedDbContext
{
    public YourDbContext() : base()
    {
    }

    public YourDbContext(DbContextOptions options) : base(options)
    {
    }
}
```

## **Usage**
```
public IEnumerable<TResult> SelectMany<TResult>(Expression<Func<T, bool>> where, Expression<Func<T, TResult>> selector)
{
    var result = _dbQueryCacheHelper.GetOrAdd<IEnumerable<TResult>, T>(_dbContext, () => _dbContext.Entity.Where(where).Select(selector).ToList(), [where, selector]);
    return result;
}

public async Task<IEnumerable<TResult>> SelectManyAsync<TResult>(Expression<Func<T, bool>> where, Expression<Func<T, TResult>> selector)
{
    var result = await _dbQueryCacheHelper.GetOrAddAsync<IEnumerable<TResult>, T>(_dbContext, async () => await _dbContext.Entity.Where(where).Select(selector).ToListAsync(), [where, selector]);
    return result!;
}
```

## **Compiled cached queries**

For hot queries, compile the cache identity and the Entity Framework query once. Cache hits use the typed parameter directly and do not visit an expression tree, serialize values, or create a string key.

```csharp
private static readonly CachedQuery<AppDbContext, int, ProductDto?> ProductById =
    CachedQuery.For<Product>().Compile(
        (AppDbContext context, int id) => context.Products
            .Where(product => product.Id == id)
            .Select(product => new ProductDto(product.Id, product.Name, product.Price))
            .SingleOrDefault()
    );

public ProductDto? GetProduct(int id)
{
    return ProductById.GetOrAdd(_dbContext, id);
}
```

Use `CompileAsync` and `GetOrAddAsync` for an EF compiled asynchronous query. The async expression receives a `CancellationToken` as its last parameter.

All values that can change the query result must be part of the typed cache identity, either as the query parameter or the cache partition. Use immutable values with stable equality semantics, such as primitives, `Guid`, enums, strings, or immutable composite keys.

When tenant or shard state comes from the `DbContext`, pass it as the typed cache partition:

```csharp
return ProductById.GetOrAdd(_dbContext, id, tenantId);
```

`CachedQuery.For<TEntity>()` defines the invalidation root. It must represent every entity read by the query through the configured entity dependency graph. Compiled cache keys are process-local and intended for in-process object-key cache stores such as `CachedEfCore.Caching.InMemory`.

## **Benchmarks**

Run the cache-hit comparison, including the 1, 4, 16, and 32-thread scenarios, with:

```bash
dotnet run -c Release --project benchmarks/CachedEfCore.Caching.InMemory.Benchmarks -- --filter '*CachedQuery*'
```

### **Performance impact**
On my tests in a url shortner api:
Without CachedEfCore I was getting 8k requests per second, and then when enabling the CachedEfCore i'm getting 107k requests per seconds improving over 13x the performance with less resource usage (yes LESS resources)


### **Important**
Don't save the instances returned by the cache
