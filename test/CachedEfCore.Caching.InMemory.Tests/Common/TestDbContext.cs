using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CachedEfCore.Caching.InMemory.Tests.Common
{
    public class TestDbContext : DbContext
    {
        public TestDbContext() : base()
        {
        }

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }

        public DbSet<LazyLoadEntity> LazyLoadEntity { get; set; }
        public DbSet<NonLazyLoadEntity> NonLazyLoadEntity { get; set; }
    }

    public class LazyLoadEntity
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(LazyLoadProp))]
        public int? LazyLoadPropId { get; set; }

        [ForeignKey(nameof(LazyLoadPropId))]
        public virtual NonLazyLoadEntity? LazyLoadProp { get; set; }
    }

    public class NonLazyLoadEntity
    {
        [Key]
        public int Id { get; set; }

        public string? StringData { get; set; }
    }
}
