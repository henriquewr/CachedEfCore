using CachedEfCore.SqlServer.SqlAnalysis;
using CachedEfCore.Tests.Common.Fixtures;
using Xunit;

namespace CachedEfCore.SqlServer.SqlAnalisys.Tests.Parsing.TruncateTableParsing
{
    public class TruncateTableSqlServerParsingTests : SqlServerParsingTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly SqlServerParser _sqlServerParser;

        public TruncateTableSqlServerParsingTests(ServiceProviderFixture serviceProviderFixture) : base(serviceProviderFixture)
        {
            _sqlServerParser = new SqlServerParser();
        }

        [Fact]
        public void TruncateTable()
        {
            var identifiers = _sqlServerParser.Parse("TRUNCATE TABLE Test");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void TruncateTable_Database_Schema_Table()
        {
            var identifiers = _sqlServerParser.Parse("TRUNCATE TABLE CachedEfCoreDb.dbo.Test");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void TruncateTable_Schema_Table()
        {
            var identifiers = _sqlServerParser.Parse("TRUNCATE TABLE dbo.Test");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("TRUNCATE TABLE CachedEfCoreDb.dbo.Test WITH (PARTITIONS (1, 2, 3));")]
        [InlineData("TRUNCATE TABLE CachedEfCoreDb.dbo.Test WITH (PARTITIONS (1 TO 3));")]
        [InlineData("TRUNCATE TABLE CachedEfCoreDb.dbo.Test WITH (PARTITIONS (1, 2 TO 4, 5));")]
        public void TruncateTable_With(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }
    }
}
