using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CachedEfCore.Extensions.DbSet
{
    public static class DbSetExtensions
    {
        internal static DbContext GetDbContext<TEntity>(this DbSet<TEntity> dbSet) where TEntity : class
        {
            var currentContext = dbSet.GetService<ICurrentDbContext>();
            return currentContext!.Context;
        }
    }
}