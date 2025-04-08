using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using CachedEfCore.Context;
using CachedEfCore.Cache;
using CachedEfCore.Interceptors;
using CachedEfCore.SqlAnalysis;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Linq;
using CachedEfCore.EntityMapping;

namespace CachedEfCore.SqlQueryEntityExtractor.Tests
{
    public class QueryCaching
    {
        private static readonly ISqlQueryEntityExtractor _sqlQueryEntityExtractor = new SqlAnalysis.SqlQueryEntityExtractor();
        private static readonly CachedDbContext _cachedDbContext = new TestDbContext(new DbQueryCacheStore(new MemoryCache(new MemoryCacheOptions())));

        public QueryCaching()
        {
        }

        private static IEntityType GetIEntityType(Type entityType)
        {
            return _cachedDbContext.Model.GetEntityTypes().First(x => x.ClrType == entityType);
        }

        private static string GetTableName(Type entityType)
        {
            var entity = GetIEntityType(entityType);
            var tableName = entity.GetTableName() ?? entity.GetViewName()!;
            return tableName;
        }


        private static IEnumerable<TestCaseData> GetDeleteTestCases()
        {
            yield return new TestCaseData(_cachedDbContext.TableEntity,
               $"""
                DELETE FROM
                {GetTableName(typeof(LazyLoadEntity))};
                """,
               new HashSet<IEntityType>
               {
                    GetIEntityType(typeof(LazyLoadEntity))
               });

            yield return new TestCaseData(_cachedDbContext.TableEntity,
                $"""
                DELETE FROM
                "{GetTableName(typeof(LazyLoadEntity))}";
                """,
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(LazyLoadEntity))
                }
            );
            
            yield return new TestCaseData(_cachedDbContext.TableEntity,
                $"""
                DELETE FROM
                [{GetTableName(typeof(LazyLoadEntity))}];
                """,
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(LazyLoadEntity))
                }
            );

            yield return new TestCaseData(_cachedDbContext.TableEntity,
               $"""
                DELETE FROM {GetTableName(typeof(LazyLoadEntity))};
                """,
               new HashSet<IEntityType>
               {
                    GetIEntityType(typeof(LazyLoadEntity))
               });  
            
            yield return new TestCaseData(_cachedDbContext.TableEntity,
               $"""
                DELETE FROM "{GetTableName(typeof(LazyLoadEntity))}";
                """,
               new HashSet<IEntityType>
               {
                    GetIEntityType(typeof(LazyLoadEntity))
               }); 
            
            yield return new TestCaseData(_cachedDbContext.TableEntity,
               $"""
                DELETE FROM [{GetTableName(typeof(LazyLoadEntity))}];
                """,
               new HashSet<IEntityType>
               {
                    GetIEntityType(typeof(LazyLoadEntity))
               });

            yield return new TestCaseData(_cachedDbContext.TableEntity,
               $"""
                DELETE u
                FROM {GetTableName(typeof(LazyLoadEntity))}
                u;
                """,
               new HashSet<IEntityType>
               {
                    GetIEntityType(typeof(LazyLoadEntity))
               });   
            
            yield return new TestCaseData(_cachedDbContext.TableEntity,
               $"""
                DELETE "u"
                FROM "{GetTableName(typeof(LazyLoadEntity))}"
                "u";
                """,
               new HashSet<IEntityType>
               {
                    GetIEntityType(typeof(LazyLoadEntity))
               });

            yield return new TestCaseData(_cachedDbContext.TableEntity,
               $"""
                DELETE [u]
                FROM [{GetTableName(typeof(LazyLoadEntity))}]
                [u];
                """,
               new HashSet<IEntityType>
               {
                    GetIEntityType(typeof(LazyLoadEntity))
               });

            yield return new TestCaseData(_cachedDbContext.TableEntity,
                $"""
                    DELETE u FROM {GetTableName(typeof(LazyLoadEntity))} u;
                """,
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(LazyLoadEntity))
                });  
            
            yield return new TestCaseData(_cachedDbContext.TableEntity,
                $"""
                    DELETE "u" FROM "{GetTableName(typeof(LazyLoadEntity))}" "u";
                """,
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(LazyLoadEntity))
                });  
            
            yield return new TestCaseData(_cachedDbContext.TableEntity,
                $"""
                    DELETE [u] FROM [{GetTableName(typeof(LazyLoadEntity))}] [u];
                """,
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(LazyLoadEntity))
                });
        }

        [TestCaseSource(nameof(GetDeleteTestCases))]
        public void Extract_Entities_From_Delete_Query(TableEntityMapping tableEntities, string sql, HashSet<IEntityType> expected)
        {
            var entities = _sqlQueryEntityExtractor.GetStateChangingEntityTypesFromSql(tableEntities, sql).ToHashSet();

            Assert.That(entities, Is.EquivalentTo(expected));
        }

        private static IEnumerable<TestCaseData> GetUpdateTestCases()
        {
            yield return new TestCaseData(_cachedDbContext.TableEntity,
               $"""
                UPDATE u
                SET u.StringData = 'test'
                FROM {GetTableName(typeof(LazyLoadEntity))} AS u;
                """,
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(LazyLoadEntity))
                }
            );

            yield return new TestCaseData(_cachedDbContext.TableEntity,
               $"""
                UPDATE "u"
                SET u.StringData = 'test'
                FROM {GetTableName(typeof(LazyLoadEntity))} AS "u";
                """,
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(LazyLoadEntity))
                }
            );

            yield return new TestCaseData(_cachedDbContext.TableEntity,
                $"""
                UPDATE [u]
                SET [u].[StringData] = 'test'
                FROM [{GetTableName(typeof(LazyLoadEntity))}] AS [u];
                """,
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(LazyLoadEntity))
                }
            );

            yield return new TestCaseData(_cachedDbContext.TableEntity,
                $"""
                UPDATE {GetTableName(typeof(LazyLoadEntity))}
                SET StringData = 'test'
                """,
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(LazyLoadEntity))
                }
            );

            yield return new TestCaseData(_cachedDbContext.TableEntity,
                $"""
                UPDATE "{GetTableName(typeof(LazyLoadEntity))}"
                SET StringData = 'test'
                """,
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(LazyLoadEntity))
                }
            );

            yield return new TestCaseData(_cachedDbContext.TableEntity,
                $"""
                UPDATE [{GetTableName(typeof(LazyLoadEntity))}]
                SET StringData = 'test'
                """,
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(LazyLoadEntity))
                }
            );
        }

        [TestCaseSource(nameof(GetUpdateTestCases))]
        public void Extract_Entities_From_Update_Query(TableEntityMapping tableEntities, string sql, HashSet<IEntityType> expected)
        {
            var entities = _sqlQueryEntityExtractor.GetStateChangingEntityTypesFromSql(tableEntities, sql).ToHashSet();

            Assert.That(entities, Is.EquivalentTo(expected));
        }

        private static IEnumerable<TestCaseData> GetInsertTestCases()
        {
            yield return new TestCaseData(_cachedDbContext.TableEntity,
               $"""
                INSERT INTO {GetTableName(typeof(LazyLoadEntity))} (StringData) VALUES ("test");
                """,
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(LazyLoadEntity))
                }
            );

            yield return new TestCaseData(_cachedDbContext.TableEntity,
               $"""
                INSERT INTO "{GetTableName(typeof(LazyLoadEntity))}" (StringData) VALUES ("test");
                """,
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(LazyLoadEntity))
                }
            );

            yield return new TestCaseData(_cachedDbContext.TableEntity,
               $"""
                INSERT INTO [{GetTableName(typeof(LazyLoadEntity))}] (StringData) VALUES ("test");
                """,
                new HashSet<IEntityType>
                {
                    GetIEntityType(typeof(LazyLoadEntity))
                }
            );
        }

        [TestCaseSource(nameof(GetInsertTestCases))]
        public void Extract_Entities_From_Insert_Query(TableEntityMapping tableEntities, string sql, HashSet<IEntityType> expected)
        {
            var entities = _sqlQueryEntityExtractor.GetStateChangingEntityTypesFromSql(tableEntities, sql).ToHashSet();

            Assert.That(entities, Is.EquivalentTo(expected));
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            _cachedDbContext.Dispose();
        }

        public class TestDbContext : CachedDbContext
        {
            public TestDbContext(DbQueryCacheStore dbQueryCacheStore) : base(dbQueryCacheStore)
            {
            }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseInMemoryDatabase("test").AddInterceptors(new DbStateInterceptor(new SqlAnalysis.SqlQueryEntityExtractor()));
                base.OnConfiguring(optionsBuilder);
            }

            public DbSet<LazyLoadEntity> LazyLoadEntity { get; set; }
            public DbSet<NonLazyLoadEntity> NonLazyLoadEntity { get; set; }
            public DbSet<AnotherLazyLoadEntity> AnotherLazyLoadEntity { get; set; }
            public DbSet<LazyLoadWithGenericEntity> LazyLoadWithGenericEntity { get; set; }
        }

        public class AnotherLazyLoadEntity
        {
            [Key]
            public int Id { get; set; }
            public string? StringData { get; set; }

            [ForeignKey(nameof(LazyLoadProp))]
            public int? LazyLoadPropId { get; set; }

            [ForeignKey(nameof(LazyLoadPropId))]
            public virtual LazyLoadEntity? LazyLoadProp { get; set; }
        }

        public class LazyLoadWithGenericEntity
        {
            [Key]
            public int Id { get; set; }
            public string? StringData { get; set; }

            public virtual ICollection<NonLazyLoadEntity>? LazyLoadGenericProp { get; set; }
        }

        public class LazyLoadEntity
        {
            [Key]
            public int Id { get; set; }
            public string? StringData { get; set; }

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
}
