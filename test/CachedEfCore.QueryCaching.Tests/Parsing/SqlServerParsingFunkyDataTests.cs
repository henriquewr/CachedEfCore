using CachedEfCore.SqlAnalysis.SqlServer;
using CachedEfCore.Tests.Common.Fixtures;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CachedEfCore.SqlAnalisys.Tests.Parsing
{
    public class SqlServerParsingFunkyDataTests : SqlServerParsingTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;
        private readonly SqlServerParser _sqlServerParser;
        public SqlServerParsingFunkyDataTests(ServiceProviderFixture serviceProviderFixture)
        {
            _serviceProviderFixture = serviceProviderFixture;
            _sqlServerParser = new SqlServerParser();
        }

        public class TestCase
        {
            public required string Sql { get; set; }

            public required HashSet<string> Identifiers { get; set; }
        }

        public static TheoryData<TestCase> GetFunkyBracketsCasesData()
        {
            var theoryData = new TheoryData<TestCase>();

            theoryData.Add(new TestCase
            {
                Sql = $"""
                UPDATE [[[[[[[[[Test] SET StringData = 'test';
                """,
                Identifiers = new HashSet<string> { "[[[[[[[[Test" },
            });
            theoryData.Add(new TestCase
            {
                Sql = $"""
                DELETE [[[[[[[[[Test]]] where id = 1;
                """,
                Identifiers = new HashSet<string> { "[[[[[[[[Test]" },
            });
            theoryData.Add(new TestCase
            {
                Sql = $"""
                DELETE [[Test[a]]];
                """,
                Identifiers = new HashSet<string> { "[Test[a]" },
            });
            theoryData.Add(new TestCase
            {
                Sql = $"""
                DELETE [Test]]why];
                """,
                Identifiers = new HashSet<string> { "Test]why" },
            });
            theoryData.Add(new TestCase
            {
                Sql = $"""
                DELETE [Test]]]]why];
                """,
                Identifiers = new HashSet<string> { "Test]]why" },
            });
            theoryData.Add(new TestCase
            {
                Sql = $"""
                DELETE [really]]bad]]identifier];
                """,
                Identifiers = new HashSet<string> { "really]bad]identifier" },
            });
            theoryData.Add(new TestCase
            {
                Sql = $"""
                DELETE[Test];
                """,
                Identifiers = new HashSet<string> { "Test" },
            });

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetFunkyBracketsCasesData))]
        public void Funky_Brackets_Query(TestCase testCase)
        {
            var sql = testCase.Sql;

            var identifiers = _sqlServerParser.Parse(sql);

            var hashSet = identifiers.Select(x => x.ToString()).ToHashSet();
            
            Assert.True(testCase.Identifiers.SetEquals(hashSet));
        }

        public static TheoryData<TestCase> GetFunkyDoubleQuotesCasesData()
        {
            var theoryData = new TheoryData<TestCase>();

            theoryData.Add(new TestCase
            {
                Sql = $""""""""""
                UPDATE """Test" SET StringData = 'test';
                """""""""",
                Identifiers = new HashSet<string> { "\"Test" },
            });
            theoryData.Add(new TestCase
            {
                Sql = $""""""""""
                DELETE """""""""Test""" where id = 1;
                """""""""",
                Identifiers = new HashSet<string> { "\"\"\"\"Test\"" },
            });
            theoryData.Add(new TestCase
            {
                Sql = $""""""
                DELETE """""Test""a""";
                """""",
                Identifiers = new HashSet<string> { "\"\"Test\"a\"" },
            });
            theoryData.Add(new TestCase
            {
                Sql = $"""
                DELETE "Test""why";
                """,
                Identifiers = new HashSet<string> { "Test\"why" },
            });
            theoryData.Add(new TestCase
            {
                Sql = $"""
                DELETE "really""bad""identifier";
                """,
                Identifiers = new HashSet<string> { "really\"bad\"identifier" },
            });
            theoryData.Add(new TestCase
            {
                Sql = $"""
                DELETE"Test";
                """,
                Identifiers = new HashSet<string> { "Test" },
            });
           
            return theoryData;
        }

        [Theory]
        [MemberData(nameof(GetFunkyDoubleQuotesCasesData))]
        public void Funky_Double_Quotes_Query(TestCase testCase)
        {
            var sql = testCase.Sql;

            var identifiers = _sqlServerParser.Parse(sql);

            var hashSet = identifiers.Select(x => x.ToString()).ToHashSet();

            Assert.True(testCase.Identifiers.SetEquals(hashSet));
        }

        [Theory]
        [InlineData("INSERT INTO Test (Value), (Value2) VALUES (1, '))))'), (2, ')))');")]
        [InlineData("""
            UPDATE Test
            SET Value = Func(
                -- ))))))
                '))',
                [abc(def)],
                /* )))) */
                "(("
            )
            FROM OtherTable;
            """)]
        public void Parenthesis_Inside(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }
    }
}
