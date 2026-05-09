using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlAnalysis.SqlServer;
using CachedEfCore.Tests.Common.Fixtures;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CachedEfCore.SqlAnalisys.Tests.Parsing
{
    public class EnclosedNonEnclosedSqlServerParsingTests : SqlServerParsingTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;
        private readonly SqlServerParser _sqlServerParser;
        public EnclosedNonEnclosedSqlServerParsingTests(ServiceProviderFixture serviceProviderFixture)
        {
            _serviceProviderFixture = serviceProviderFixture;
            _sqlServerParser = new SqlServerParser();
        }

        protected virtual IServiceProvider CreateProvider()
           => _serviceProviderFixture.CreateProvider(services =>
           {
               services.AddCachedEfCore<SqlServerQueryEntityExtractor>();
           });

        protected static IEnumerable<Func<string, string>> GetSqlVariantsTransformFunc()
        {
            yield return identifier => identifier;
            yield return identifier => $"[{identifier}]";
            yield return identifier => $"\"{identifier}\"";
        }

        public class TestCase
        {
            public required Func<Func<string, string>, string> GetSql { get; set; }

            public required HashSet<string> Identifiers { get; set; }
        }

        public static TheoryData<TestCase> GetDeleteTestCasesData()
        {
            var theoryData = new TheoryData<TestCase>();

            theoryData.Add(new TestCase
            {
                GetSql = (applyVariantFunc) => $"""
                DELETE FROM
                {applyVariantFunc("Test")};
                """,
                Identifiers = new HashSet<string> { "Test" },
            });

            theoryData.Add(new TestCase
            {
                GetSql = (applyVariantFunc) => $"""
                DELETE FROM {applyVariantFunc("Test")};
                """,
                Identifiers = new HashSet<string> { "Test" },
            });
            theoryData.Add(new TestCase
            {
                GetSql = (applyVariantFunc) => $"""
                DELETE {applyVariantFunc("u")}
                FROM {applyVariantFunc("Test")}
                {applyVariantFunc("u")};
                """,
                Identifiers = new HashSet<string> { "Test" },
            });
            theoryData.Add(new TestCase
            {
                GetSql = (applyVariantFunc) => $"""
                    DELETE {applyVariantFunc("u")} FROM {applyVariantFunc("Test")} {applyVariantFunc("u")};
                """,
                Identifiers = new HashSet<string> { "Test" },
            });

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetDeleteTestCasesData))]
        public void Extract_Identifiers_From_Delete_Query(TestCase testCase)
        {
            Test(testCase);
        }

        public static TheoryData<TestCase> GetUpdateTestCasesData()
        {
            var theoryData = new TheoryData<TestCase>();

            theoryData.Add(new TestCase
            {
                GetSql = (applyVariantFunc) => $"""
                UPDATE {applyVariantFunc("Test")}
                SET StringData = 'test'
                """,
                Identifiers = new HashSet<string> { "Test" },
            });

            theoryData.Add(new TestCase
            {
                GetSql = (applyVariantFunc) => $"""
                UPDATE {applyVariantFunc("Test")} SET StringData = 'test'
                """,
                Identifiers = new HashSet<string> { "Test" },
            });

            theoryData.Add(new TestCase
            {
                GetSql = (applyVariantFunc) => $"""
                UPDATE {applyVariantFunc("Test")} SET StringData = 'test''a'
                """,
                Identifiers = new HashSet<string> { "Test" },
            });

            theoryData.Add(new TestCase
            {
                GetSql = (applyVariantFunc) => $"""
                UPDATE {applyVariantFunc("u")}
                SET {applyVariantFunc("u")}.StringData = 'test'
                FROM {applyVariantFunc("Test")} AS {applyVariantFunc("u")};
                """,
                Identifiers = new HashSet<string> { "Test" },
            });

            theoryData.Add(new TestCase
            {
                GetSql = (applyVariantFunc) => $"""
                UPDATE {applyVariantFunc("u")} SET {applyVariantFunc("u")}.StringData = 'test' FROM {applyVariantFunc("Test")} AS {applyVariantFunc("u")};
                """,
                Identifiers = new HashSet<string> { "Test" },
            });

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetUpdateTestCasesData))]
        public void Extract_Identifiers_From_Update_Query(TestCase testCase)
        {
            Test(testCase);
        }


        public static TheoryData<TestCase> GetInsertTestCasesData()
        {
            var theoryData = new TheoryData<TestCase>();

            theoryData.Add(new TestCase
            {
                GetSql = (applyVariantFunc) => $"INSERT INTO {applyVariantFunc("Test")} (StringData) VALUES (\"test\");",
                Identifiers = new HashSet<string> { "Test" },
            });
            theoryData.Add(new TestCase
            {
                GetSql = (applyVariantFunc) => $"INSERT {applyVariantFunc("Test")} (StringData) VALUES (\"test\");",
                Identifiers = new HashSet<string> { "Test" },
            });
            theoryData.Add(new TestCase
            {
                GetSql = (applyVariantFunc) => $"""
                    INSERT INTO 
                    {applyVariantFunc("Test")} (StringData) 
                    VALUES ("test");
                    """,
                Identifiers = new HashSet<string> { "Test" },
            });
            theoryData.Add(new TestCase
            {
                GetSql = (applyVariantFunc) => $"""
                    INSERT 
                    {applyVariantFunc("Test")} (StringData) 
                    VALUES ("test");
                    """,
                Identifiers = new HashSet<string> { "Test" },
            });

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetInsertTestCasesData))]
        public void Extract_Identifiers_From_Insert_Query(TestCase testCase)
        {
            Test(testCase);
        }

        private void Test(TestCase testCase)
        {
            foreach (var transformFunc in GetSqlVariantsTransformFunc())
            {
                var sql = testCase.GetSql(transformFunc);

                var identifiers = _sqlServerParser.Parse(sql);

                var hashSet = identifiers.Select(x => x.ToString()).ToHashSet();

                Assert.True(testCase.Identifiers.SetEquals(hashSet));
            }
        }
    }
}
