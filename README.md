# CachedEfCore

CachedEfCore is a caching library for entity framework core

The cache of CachedEfCore is always the lastest version of the object cached, the library auto invalidates the cache when some entity related to the cached entity changes state, so is impossible to get an old cache

## **Configuration**
```
public void ConfigureServices(IServiceCollection services)
{
    services.AddMemoryCache();
    services.AddSingleton<KeyGeneratorVisitor>();
    services.AddSingleton<IDbQueryCacheHelper, DbQueryCacheHelper>();
    services.AddSingleton<IDbQueryCacheStore, DbQueryCacheStore>();
    services.AddSingleton<ISqlQueryEntityExtractor, SqlServerQueryEntityExtractor>();  // currently only SQL Server has a dedicated implementation, you can use GenericSqlQueryEntityExtractor for other database providers
    services.AddSingleton<DbStateInterceptor>();

    // AddDbContextPool or AddDbContext
    services.AddDbContextPool<AppDbContext>((serviceProvider, options) =>
    {
        options.UseLazyLoadingProxies();
        options.UseSqlServer(connectionString).AddInterceptors(serviceProvider.GetRequiredService<DbStateInterceptor>());
    });
}
```

## **DbContext**
```
public class YourDbContext : CachedDbContext
{
    public YourDbContext(DbContextOptions options, IDbQueryCacheStore dbQueryCacheStore) : base(options, dbQueryCacheStore)
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

### **Performance impact**
On my tests in a url shortner api:
Without CachedEfCore I was getting 8k requests per second, and then when enabling the CachedEfCore i'm getting 107k requests per seconds improving over 13x the performance with less resource usage (yes LESS resources)


### **Important**
Don't save the instances returned by the cache
