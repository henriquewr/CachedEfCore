# CachedEfCore

CachedEfCore is a caching library for entity framework core

The cache of CachedEfCore is always the lastest version of the object cached, the library auto invalidates the cache when some entity related to the cached entity changes state, so is impossible to get an old cache

## **Usage**
```
public void ConfigureServices(IServiceCollection services)
{
    services.AddMemoryCache();
    services.AddScoped<KeyGeneratorVisitor>();
    services.AddScoped<IDbQueryCacheHelper, DbQueryCacheHelper>();
    services.AddSingleton<IDbQueryCacheStore, DbQueryCacheStore>();


    // AddDbContextPool or AddDbContext
    services.AddDbContextPool<AppDbContext>(options =>
    {
        options.UseLazyLoadingProxies();
        options.UseSqlServer(connectionString).AddInterceptors(new DbStateInterceptor(new SqlQueryEntityExtractor()));
    });
}
```
> You can use singletons on services.AddScoped<KeyGeneratorVisitor>();
> and services.AddScoped<IDbQueryCacheHelper, DbQueryCacheHelper>();
> basically everything is thread safe, the library is built with multithreading in mind
> but in my tests Scoped in these services are a bit faster than Singletons, but it uses a little bit more memory, but i think its worth it


## **DbContext**
```
public class YourDbContext : CachedDbContext
{
    public YourDbContext(DbContextOptions options, IDbQueryCacheStore dbQueryCacheStore) : base(options, dbQueryCacheStore)
    {
    }
}
```

### **Performance impact**
On my tests in a url shortner api:
Without CachedEfCore I was getting 8k requests per second, and then when enabling the CachedEfCore i'm getting 105k requests per seconds improving over 13x the performance with less resource usage
