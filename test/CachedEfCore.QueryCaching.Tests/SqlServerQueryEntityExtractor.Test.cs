using CachedEfCore.Cache;
using CachedEfCore.Context;
using CachedEfCore.EntityMapping;
using CachedEfCore.Interceptors;
using CachedEfCore.SqlAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Xunit;

namespace CachedEfCore.SqlQueryEntityExtractor.Tests
{
    public class QueryCaching
    {
        private static IEnumerable<ISqlQueryEntityExtractor> SqlQueryEntityExtractorImplementations =>
            new ISqlQueryEntityExtractor[]
            {
                new SqlServerQueryEntityExtractor(),
                new GenericSqlQueryEntityExtractor()
            };

        private static readonly CachedDbContext _cachedDbContext = new TestDbContext(new DbQueryCacheStore(new MemoryCache(new MemoryCacheOptions())));

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

        private static IEnumerable<Func<string, string>> GetSqlVariantsTransformFunc()
        {
            yield return identifier => identifier;
            yield return identifier => $"[{identifier}]";
            yield return identifier => $"\"{identifier}\"";
        }

        public static TheoryData<ISqlQueryEntityExtractor, TableEntityMapping, string, HashSet<IEntityType>> GetDeleteTestCases()
        {
            var theoryData = new TheoryData<ISqlQueryEntityExtractor, TableEntityMapping, string, HashSet<IEntityType>>();
            
            foreach (var implementation in SqlQueryEntityExtractorImplementations)
            {
                var variantFuncs = GetSqlVariantsTransformFunc().ToList();
                foreach (var applyVariantFunc in variantFuncs)
                {
                    theoryData.Add(
                        implementation,
                        _cachedDbContext.TableEntity,
                        $"""
                        DELETE FROM
                        {applyVariantFunc(GetTableName(typeof(LazyLoadEntity)))};
                        """,
                        new HashSet<IEntityType>
                        {
                            GetIEntityType(typeof(LazyLoadEntity))
                        }
                    );

                    theoryData.Add(
                        implementation,
                        _cachedDbContext.TableEntity,
                        $"""
                        DELETE FROM {applyVariantFunc(GetTableName(typeof(LazyLoadEntity)))};
                        """,
                        new HashSet<IEntityType>
                        {
                            GetIEntityType(typeof(LazyLoadEntity))
                        }
                    );

                    theoryData.Add(
                        implementation,
                        _cachedDbContext.TableEntity,
                        $"""
                        DELETE {applyVariantFunc("u")}
                        FROM {applyVariantFunc(GetTableName(typeof(LazyLoadEntity)))}
                        {applyVariantFunc("u")};
                        """,
                        new HashSet<IEntityType>
                        {
                            GetIEntityType(typeof(LazyLoadEntity))
                        }
                    );

                    theoryData.Add(
                        implementation,
                        _cachedDbContext.TableEntity,
                        $"""
                            DELETE {applyVariantFunc("u")} FROM {applyVariantFunc(GetTableName(typeof(LazyLoadEntity)))} {applyVariantFunc("u")};
                        """,
                        new HashSet<IEntityType>
                        {
                            GetIEntityType(typeof(LazyLoadEntity))
                        }
                    );
                }
            }

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetDeleteTestCases))]
        public void Extract_Entities_From_Delete_Query(ISqlQueryEntityExtractor sqlQueryEntityExtractor, TableEntityMapping tableEntities, string sql, HashSet<IEntityType> expected)
        {
            var entities = sqlQueryEntityExtractor.GetStateChangingEntityTypesFromSql(tableEntities, sql).ToHashSet();

            Assert.True(expected.SetEquals(entities));
        }

        public static TheoryData<ISqlQueryEntityExtractor, TableEntityMapping, string, HashSet<IEntityType>> GetUpdateTestCases()
        {
            var theoryData = new TheoryData<ISqlQueryEntityExtractor, TableEntityMapping, string, HashSet<IEntityType>>();

            foreach (var implementation in SqlQueryEntityExtractorImplementations)
            {
                var variantFuncs = GetSqlVariantsTransformFunc().ToList();
                foreach (var applyVariantFunc in variantFuncs)
                {
                    theoryData.Add(
                        implementation,
                        _cachedDbContext.TableEntity,
                        $"""
                        UPDATE {applyVariantFunc(GetTableName(typeof(LazyLoadEntity)))}
                        SET StringData = 'test'
                        """,
                        new HashSet<IEntityType>
                        {
                            GetIEntityType(typeof(LazyLoadEntity))
                        }
                    );

                    theoryData.Add(
                        implementation,
                        _cachedDbContext.TableEntity,
                        $"""
                        UPDATE {applyVariantFunc(GetTableName(typeof(LazyLoadEntity)))} SET StringData = 'test'
                        """,
                        new HashSet<IEntityType>
                        {
                            GetIEntityType(typeof(LazyLoadEntity))
                        }
                    );

                    theoryData.Add(
                        implementation,
                        _cachedDbContext.TableEntity,
                        $"""
                        UPDATE {applyVariantFunc("u")}
                        SET {applyVariantFunc("u")}.StringData = 'test'
                        FROM {applyVariantFunc(GetTableName(typeof(LazyLoadEntity)))} AS {applyVariantFunc("u")};
                        """,
                        new HashSet<IEntityType>
                        {
                            GetIEntityType(typeof(LazyLoadEntity))
                        }
                    );

                    theoryData.Add(
                        implementation,
                        _cachedDbContext.TableEntity,
                        $"""
                        UPDATE {applyVariantFunc("u")} SET {applyVariantFunc("u")}.StringData = 'test' FROM {applyVariantFunc(GetTableName(typeof(LazyLoadEntity)))} AS {applyVariantFunc("u")};
                        """,
                        new HashSet<IEntityType>
                        {
                            GetIEntityType(typeof(LazyLoadEntity))
                        }
                    );
                }
            }

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetUpdateTestCases))]
        public void Extract_Entities_From_Update_Query(ISqlQueryEntityExtractor sqlQueryEntityExtractor, TableEntityMapping tableEntities, string sql, HashSet<IEntityType> expected)
        {
            var entities = sqlQueryEntityExtractor.GetStateChangingEntityTypesFromSql(tableEntities, sql).ToHashSet();

            Assert.True(expected.SetEquals(entities));
        }

        public static TheoryData<ISqlQueryEntityExtractor, TableEntityMapping, string, HashSet<IEntityType>> GetInsertTestCases()
        {
            var theoryData = new TheoryData<ISqlQueryEntityExtractor, TableEntityMapping, string, HashSet<IEntityType>>();

            foreach (var implementation in SqlQueryEntityExtractorImplementations)
            {
                var variantFuncs = GetSqlVariantsTransformFunc().ToList();
                foreach (var applyVariantFunc in variantFuncs)
                {
                    theoryData.Add(
                        implementation,
                        _cachedDbContext.TableEntity,
                        $"""
                        INSERT INTO {applyVariantFunc(GetTableName(typeof(LazyLoadEntity)))} (StringData) VALUES ("test");
                        """,
                        new HashSet<IEntityType>
                        {
                            GetIEntityType(typeof(LazyLoadEntity))
                        }
                    );

                    theoryData.Add(
                        implementation,
                        _cachedDbContext.TableEntity,
                        $"""
                        INSERT {applyVariantFunc(GetTableName(typeof(LazyLoadEntity)))} (StringData) VALUES ("test");
                        """,
                        new HashSet<IEntityType>
                        {
                            GetIEntityType(typeof(LazyLoadEntity))
                        }
                    );

                    theoryData.Add(
                        implementation,
                        _cachedDbContext.TableEntity,
                        $"""
                        INSERT INTO 
                        {applyVariantFunc(GetTableName(typeof(LazyLoadEntity)))} (StringData) 
                        VALUES ("test");
                        """,
                        new HashSet<IEntityType>
                        {
                            GetIEntityType(typeof(LazyLoadEntity))
                        }
                    );

                    theoryData.Add(
                        implementation,
                        _cachedDbContext.TableEntity,
                        $"""
                        INSERT 
                        {applyVariantFunc(GetTableName(typeof(LazyLoadEntity)))} (StringData) 
                        VALUES ("test");
                        """,
                        new HashSet<IEntityType>
                        {
                            GetIEntityType(typeof(LazyLoadEntity))
                        }
                    );
                }
            }

            return theoryData;
        }
        
        [Theory]
        [MemberData(nameof(GetInsertTestCases))]
        public void Extract_Entities_From_Insert_Query(ISqlQueryEntityExtractor sqlQueryEntityExtractor, TableEntityMapping tableEntities, string sql, HashSet<IEntityType> expected)
        {
            var entities = sqlQueryEntityExtractor.GetStateChangingEntityTypesFromSql(tableEntities, sql).ToHashSet();

            Assert.True(expected.SetEquals(entities));
        }

        public class TestDbContext : CachedDbContext
        {
            public TestDbContext(DbQueryCacheStore dbQueryCacheStore) : base(dbQueryCacheStore)
            {
            }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseInMemoryDatabase("test").AddInterceptors(new DbStateInterceptor(new SqlServerQueryEntityExtractor()));
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