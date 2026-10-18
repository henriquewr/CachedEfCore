using CachedEfCore.PostgreSQL.SqlAnalysis;
using CachedEfCore.Tests.Common.Fixtures;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CachedEfCore.PostgreSQL.Tests.SqlAnalisys.Parsing
{
    public class PostgreSqlParsingFunkyDataTests : PostgreSqlParsingTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly PostgreSqlParser _postgreSqlParser;
        public PostgreSqlParsingFunkyDataTests(ServiceProviderFixture serviceProviderFixture) : base(serviceProviderFixture)
        {
            _postgreSqlParser = new PostgreSqlParser();
        }

        public class TestCase
        {
            public required string Sql { get; set; }

            public required HashSet<string> Identifiers { get; set; }
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
                DELETE FROM """""""""Test""" where id = 1;
                """""""""",
                Identifiers = new HashSet<string> { "\"\"\"\"Test\"" },
            });
            theoryData.Add(new TestCase
            {
                Sql = $""""""
                DELETE FROM"""""Test""a""";
                """""",
                Identifiers = new HashSet<string> { "\"\"Test\"a\"" },
            });
            theoryData.Add(new TestCase
            {
                Sql = $"""
                DELETE FROM "Test""why";
                """,
                Identifiers = new HashSet<string> { "Test\"why" },
            });
            theoryData.Add(new TestCase
            {
                Sql = $"""
                DELETE FROM "really""bad""identifier";
                """,
                Identifiers = new HashSet<string> { "really\"bad\"identifier" },
            });
            theoryData.Add(new TestCase
            {
                Sql = $"""
                DELETE FROM"Test";
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

            var identifiers = _postgreSqlParser.Parse(sql);

            var hashSet = identifiers.Select(x => x.ToString()).ToHashSet();

            Assert.True(testCase.Identifiers.SetEquals(hashSet));
        }

        [Theory]
        [InlineData("INSERT INTO Test (Value, Value2) VALUES (1, '))))'), (2, ')))');")]
        [InlineData("""
            UPDATE Test
            SET Value = Func(
                -- ))))))
                '))',
                "abc(def)",
                /* )))) /*commented*/commented/*commented*/*/
                "(("
            )
            FROM OtherTable;
            """)]
        public void Parenthesis_Inside(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Unicode_Identifiers()
        {
            var sql = """
                INSERT INTO U&"\0441\043B\043E\043D" ("Value", "Value2") VALUES (1, '))))'), (2, ')))');
                """;

            var identifiers = _postgreSqlParser.Parse(sql);

            // I dont really know how to test this
            Assert.Equal("\\0441\\043B\\043E\\043D", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Unicode_Identifiers_With_Uescape()
        {
            var sql = """
                INSERT INTO U&"d!0061t!+000061" UESCAPE '!' ("Value", "Value2") VALUES (1, '))))'), (2, ')))');
                """;

            var identifiers = _postgreSqlParser.Parse(sql);

            // Honestly I have no idea what the expected value should be 
            Assert.Equal("d!0061t!+000061", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Unicode_String_Inside_Parenthesis()
        {
            var sql = """
                INSERT INTO "Test" ("Value", "Value2") VALUES (1, U&'d\0061t\+000061'), (2, U&'\0441\043B\043E\043D');
                """;

            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Unicode_String()
        {
            var sql = """
                UPDATE "Test"
                SET
                    "Value" = 1,
                    "Value2" = U&'d\0061t\+000061'
                WHERE "Id" = 1;
                """;

            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Unicode_String_With_Uescape_Inside_Parenthesis()
        {
            var sql = """
                INSERT INTO "Test" ("Value", "Value2") VALUES (1, U&'d!0061t!+000061' UESCAPE '!'), (2, U&'d!0061t!+000061'UESCAPE '!');
                """;

            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Unicode_String_With_Uescape()
        {
            var sql = """
                UPDATE "Test"
                SET
                    "Value" = 1,
                    "Value2" = U&'d!0061t!+000061' UESCAPE '!'
                WHERE "Id" = 1;
                """;

            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("$$Dianne's horse$$")]
        [InlineData("$SomeTag$Dianne's horse$SomeTag$")]
        public void Dollar_String(string dollarString)
        {
            var sql = $"""
                UPDATE "Test"
                SET
                    "Value" = 1,
                    "Value2" = {dollarString}
                WHERE "Id" = 1;
                """;

            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Dollar_String_Inside_Parenthesis()
        {
            var sql = """
                INSERT INTO "Test" ("Value", "Value2") VALUES (1, $$Dianne's horse$$), (2, $SomeTag$Dianne's horse$SomeTag$);
                """;

            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }
    }
}
