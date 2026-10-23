using CachedEfCore.PostgreSQL.SqlAnalysis;
using CachedEfCore.Tests.Common.Fixtures;
using System.Linq;
using Xunit;

namespace CachedEfCore.PostgreSQL.Tests.SqlAnalisys.Parsing.TruncateTableParsing
{
    public class TruncateTablePostgreSqlParsingTests : PostgreSqlParsingTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly PostgreSqlParser _postgreSqlParser;

        public TruncateTablePostgreSqlParsingTests(ServiceProviderFixture serviceProviderFixture) : base(serviceProviderFixture)
        {
            _postgreSqlParser = new PostgreSqlParser();
        }

        [Fact]
        public void Truncate()
        {
            var identifiers = _postgreSqlParser.Parse("TRUNCATE Test");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Truncate_Table()
        {
            var identifiers = _postgreSqlParser.Parse("TRUNCATE TABLE Test");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Truncate_Only()
        {
            var identifiers = _postgreSqlParser.Parse("TRUNCATE ONLY Test");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Truncate_Star()
        {
            var identifiers = _postgreSqlParser.Parse("TRUNCATE Test *");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Truncate_Restart_Identity()
        {
            var identifiers = _postgreSqlParser.Parse("TRUNCATE Test RESTART IDENTITY");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Truncate_Continue_Identity()
        {
            var identifiers = _postgreSqlParser.Parse("TRUNCATE Test CONTINUE IDENTITY");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Truncate_Cascade()
        {
            var identifiers = _postgreSqlParser.Parse("TRUNCATE Test CASCADE");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Truncate_Restrict()
        {
            var identifiers = _postgreSqlParser.Parse("TRUNCATE Test RESTRICT");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("TRUNCATE TABLE ONLY Test, Test2 *, Test3 CASCADE", "Test,Test2,Test3")]
        [InlineData("TRUNCATE TABLE ONLY Test, Test2 * CASCADE", "Test,Test2")]
        public void Truncate_Multiple_Tables(string sql, string tables)
        {
            var identifiers = _postgreSqlParser.Parse(sql).Select(x => x.ToString()).Order().ToArray();

            var tablesArr = tables.Split(',').Order().ToArray();

            Assert.Equal(tablesArr, identifiers);
        }
    }
}
