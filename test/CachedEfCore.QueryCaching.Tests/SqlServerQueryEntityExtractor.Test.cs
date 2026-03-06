using CachedEfCore.Context;
using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlAnalysis;
using CachedEfCore.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CachedEfCore.SqlQueryEntityExtractor.Tests
{
    public class QueryCaching : SqlQueryEntityExtractorTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;
        private readonly CachedDbContext _dbContext;
        public QueryCaching(ServiceProviderFixture serviceProviderFixture)
        {
            _serviceProviderFixture = serviceProviderFixture;
            _dbContext = CreateProvider().CreateScope().ServiceProvider.GetRequiredService<TestDbContext>();
        }

        protected virtual IServiceProvider CreateProvider()
           => _serviceProviderFixture.CreateProvider(services =>
           {
               services.AddCachedEfCore<SqlServerQueryEntityExtractor>();

               services.AddDbContext<TestDbContext>();
           });

        private static IEnumerable<ISqlQueryEntityExtractor> SqlQueryEntityExtractorImplementations =>
           new ISqlQueryEntityExtractor[]
           {
                new SqlServerQueryEntityExtractor(),
                new GenericSqlQueryEntityExtractor()
           };

        public class TestCase
        {
            public required Func<string, Func<string, string>, string> GetSql { get; set; }

            public required Type EntityTable { get; set; }
            public required HashSet<Type> StateChangingEntities { get; set; }
        }

        private IEntityType GetIEntityType(Type entityType)
        {
            return _dbContext.Model.GetEntityTypes().First(x => x.ClrType == entityType);
        }

        private string GetTableName(Type entityType)
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

        public static TheoryData<TestCase> GetDeleteTestCasesData()
        {
            var theoryData = new TheoryData<TestCase>();

            theoryData.Add(new TestCase
            {
                GetSql = (table, applyVariantFunc) => $"""
                DELETE FROM
                {applyVariantFunc(table)};
                """,
                EntityTable = typeof(LazyLoadEntity),
                StateChangingEntities = new HashSet<Type>
                {
                    typeof(LazyLoadEntity)
                }
            });

            theoryData.Add(new TestCase
            {
                GetSql = (table, applyVariantFunc) => $"""
                DELETE FROM {applyVariantFunc(table)};
                """,
                EntityTable = typeof(LazyLoadEntity),
                StateChangingEntities = new HashSet<Type>
                {
                    typeof(LazyLoadEntity)
                }
            });
            theoryData.Add(new TestCase
            {
                GetSql = (table, applyVariantFunc) => $"""
                DELETE {applyVariantFunc("u")}
                FROM {applyVariantFunc(table)}
                {applyVariantFunc("u")};
                """,
                EntityTable = typeof(LazyLoadEntity),
                StateChangingEntities = new HashSet<Type>
                {
                    typeof(LazyLoadEntity)
                }
            });
            theoryData.Add(new TestCase
            {
                GetSql = (table, applyVariantFunc) => $"""
                    DELETE {applyVariantFunc("u")} FROM {applyVariantFunc(table)} {applyVariantFunc("u")};
                """,
                EntityTable = typeof(LazyLoadEntity),
                StateChangingEntities = new HashSet<Type>
                {
                    typeof(LazyLoadEntity)
                }
            });

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetDeleteTestCasesData))]
        public void Extract_Entities_From_Delete_Query(TestCase testCase)
        {
            Test(testCase);
        }

        public static TheoryData<TestCase> GetUpdateTestCasesData()
        {
            var theoryData = new TheoryData<TestCase>();

            theoryData.Add(new TestCase
            {
                GetSql = (table, applyVariantFunc) => $"""
                UPDATE {applyVariantFunc(table)}
                SET StringData = 'test'
                """,
                EntityTable = typeof(LazyLoadEntity),
                StateChangingEntities = new HashSet<Type>
                {
                    typeof(LazyLoadEntity)
                }
            });

            theoryData.Add(new TestCase
            {
                GetSql = (table, applyVariantFunc) => $"""
                UPDATE {applyVariantFunc(table)} SET StringData = 'test'
                """,
                EntityTable = typeof(LazyLoadEntity),
                StateChangingEntities = new HashSet<Type>
                {
                    typeof(LazyLoadEntity)
                }
            });

            theoryData.Add(new TestCase
            {
                GetSql = (table, applyVariantFunc) => $"""
                UPDATE {applyVariantFunc("u")}
                SET {applyVariantFunc("u")}.StringData = 'test'
                FROM {applyVariantFunc(table)} AS {applyVariantFunc("u")};
                """,
                EntityTable = typeof(LazyLoadEntity),
                StateChangingEntities = new HashSet<Type>
                {
                    typeof(LazyLoadEntity)
                }
            });

            theoryData.Add(new TestCase
            {
                GetSql = (table, applyVariantFunc) => $"""
                UPDATE {applyVariantFunc("u")} SET {applyVariantFunc("u")}.StringData = 'test' FROM {applyVariantFunc(table)} AS {applyVariantFunc("u")};
                """,
                EntityTable = typeof(LazyLoadEntity),
                StateChangingEntities = new HashSet<Type>
                {
                    typeof(LazyLoadEntity)
                }
            });

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetUpdateTestCasesData))]
        public void Extract_Entities_From_Update_Query(TestCase testCase)
        {
            Test(testCase);
        }


        public static TheoryData<TestCase> GetInsertTestCasesData()
        {
            var theoryData = new TheoryData<TestCase>();

            theoryData.Add(new TestCase
            {
                GetSql = (table, applyVariantFunc) => $"INSERT INTO {applyVariantFunc(table)} (StringData) VALUES (\"test\");",
                EntityTable = typeof(LazyLoadEntity),
                StateChangingEntities = new HashSet<Type>
                {
                    typeof(LazyLoadEntity)
                },
            });
            theoryData.Add(new TestCase
            {
                GetSql = (table, applyVariantFunc) => $"INSERT {applyVariantFunc(table)} (StringData) VALUES (\"test\");",
                EntityTable = typeof(LazyLoadEntity),
                StateChangingEntities = new HashSet<Type>
                {
                    typeof(LazyLoadEntity)
                },
            });
            theoryData.Add(new TestCase
            {
                GetSql = (table, applyVariantFunc) => $"""
                    INSERT INTO 
                    {applyVariantFunc(table)} (StringData) 
                    VALUES ("test");
                    """,
                EntityTable = typeof(LazyLoadEntity),
                StateChangingEntities = new HashSet<Type>
                {
                    typeof(LazyLoadEntity)
                },
            });
            theoryData.Add(new TestCase
            {
                GetSql = (table, applyVariantFunc) => $"""
                    INSERT 
                    {applyVariantFunc(table)} (StringData) 
                    VALUES ("test");
                    """,
                EntityTable = typeof(LazyLoadEntity),
                StateChangingEntities = new HashSet<Type>
                {
                    typeof(LazyLoadEntity)
                },
            });

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetInsertTestCasesData))]
        public void Extract_Entities_From_Insert_Query(TestCase testCase)
        {
            Test(testCase);
        }

        private void Test(TestCase testCase)
        {
            var stateChangingEntities = testCase.StateChangingEntities.Select(GetIEntityType).ToHashSet();

            foreach (var sqlQueryEntityExtractor in SqlQueryEntityExtractorImplementations)
            {
                foreach (var transformFunc in GetSqlVariantsTransformFunc())
                {
                    var sql = testCase.GetSql(GetTableName(testCase.EntityTable), transformFunc);

                    var entities = sqlQueryEntityExtractor.GetStateChangingEntityTypesFromSql(_dbContext.TableEntity, sql).ToHashSet();

                    Assert.True(stateChangingEntities.SetEquals(entities));
                }
            }
        }
    }
}