using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlAnalysis.SqlServer;
using CachedEfCore.Tests.Common.Fixtures;
using System;
using System.Linq;
using Xunit;

namespace CachedEfCore.SqlAnalisys.Tests.Parsing.DeleteParsing
{
    public class DeleteSqlServerParsingTests : SqlServerParsingTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;
        private readonly SqlServerParser _sqlServerParser;

        public DeleteSqlServerParsingTests(ServiceProviderFixture serviceProviderFixture)
        {
            _serviceProviderFixture = serviceProviderFixture;
            _sqlServerParser = new SqlServerParser();
        }

        protected virtual IServiceProvider CreateProvider()
           => _serviceProviderFixture.CreateProvider(services =>
           {
               services.AddCachedEfCore<SqlServerQueryEntityExtractor>();
           });

        [Theory]
        [InlineData("DELETE TOP (SELECT 1) FROM dbo.Test;")]
        [InlineData("DELETE TOP(10) FROM Test;")]
        [InlineData("DELETE TOP(((10))) FROM Test;")]
        [InlineData("DELETE TOP (((10))) FROM Test;")]
        public void Top_Without_Percent(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("DELETE TOP (SELECT 1) PERCENT FROM Test;")]
        [InlineData("DELETE TOP(10) PERCENT FROM Test;")]
        [InlineData("DELETE TOP(((10))) PERCENT FROM Test;")]
        [InlineData("DELETE TOP (((10))) PERCENT FROM Test;")]
        public void Top_Percent(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("DELETE u FROM Test u;")]
        [InlineData("DELETE FROM u FROM Test u;")]
        [InlineData("DELETE TOP (1) u FROM Test u;")]
        public void Delete_Alias(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Option()
        {
            var identifiers = _sqlServerParser.Parse("DELETE u FROM Test AS u OPTION (RECOMPILE);");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("DELETE u OUTPUT deleted.Value AS OldValue FROM Test u;")]
        [InlineData("DELETE u OUTPUT 1 OldValue FROM Test u;")]
        [InlineData("DELETE u OUTPUT 1 + 2 OldValue FROM Test u;")]
        [InlineData("DELETE TOP (1) u OUTPUT 1 + 2 OldValue FROM Test u;")]
        [InlineData("DELETE u OUTPUT deleted.Value + 2 OldValue FROM Test u;")]
        [InlineData("DELETE u OUTPUT 'a' OldValue FROM Test u;")]
        [InlineData("DELETE u OUTPUT ABS(-1) OldValue FROM Test u;")]
        [InlineData("DELETE u OUTPUT ABS(-1) + 2 OldValue FROM Test u;")]
        [InlineData("DELETE u OUTPUT ABS(-1) + 2 FROM Test u;")]
        [InlineData("DELETE u OUTPUT deleted.Value OldValue FROM Test u;")]
        [InlineData("DELETE Test OUTPUT deleted.Value OldValue;")]
        public void Output(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("DELETE u OUTPUT deleted.Value AS OldValue, deleted.Value2 AS Value2 INTO Test2 FROM Test u;")]
        [InlineData("DELETE u OUTPUT 1 OldValue INTO Test2 FROM Test u;")]
        [InlineData("DELETE u OUTPUT 1 + 2 OldValue INTO Test2 FROM Test u;")]
        [InlineData("DELETE TOP (1) u OUTPUT 1 + 2 OldValue INTO Test2 FROM Test u;")]
        [InlineData("DELETE u OUTPUT deleted.Value + 2 OldValue INTO Test2 FROM Test u;")]
        [InlineData("DELETE u OUTPUT 'a' OldValue INTO Test2 FROM Test u;")]
        [InlineData("DELETE u OUTPUT ABS(-1) OldValue INTO Test2 FROM Test u;")]
        [InlineData("DELETE u OUTPUT ABS(-1) + 2 OldValue INTO Test2 FROM Test u;")]
        [InlineData("DELETE u OUTPUT ABS(-1) + 2 INTO Test2 FROM Test u;")]
        [InlineData("DELETE u OUTPUT deleted.Value OldValue INTO Test2 FROM Test u;")]
        [InlineData("DELETE u OUTPUT deleted.Value OldValue INTO dbo.Test2 FROM Test u;")]
        [InlineData("DELETE Test OUTPUT deleted.Value OldValue INTO Test2;")]
        [InlineData("DELETE Test OUTPUT deleted.Value OldValue INTO dbo.Test2;")]
        public void Output_Into(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
        WITH Cte AS
        (
            SELECT *
            FROM Test
        )
        DELETE u
        OUTPUT deleted.Value AS OldValue,
                deleted.Value2 AS Value2
        INTO Test2
        FROM Cte u;
        """)]
        [InlineData("""
        WITH Cte AS
        (
            SELECT *
            FROM Test
        )
        DELETE Cte
        OUTPUT deleted.Value AS OldValue,
                deleted.Value2 AS Value2
        INTO Test2
        FROM Test3 u;
        """)]
        public void Cte(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Fact]
        public void Unused_Cte()
        {
            var identifiers = _sqlServerParser.Parse("""
            WITH Cte AS
            (
                SELECT *
                FROM Test
            )
            DELETE u
            OUTPUT deleted.Value AS OldValue,
                    deleted.Value2 AS Value2
            INTO Test2
            FROM Test3 u;
            """);

            Assert.Equal(["Test2", "Test3"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
        WITH Filtered AS
        (
            SELECT *
            FROM Test
            WHERE IsActive = 0
        ),
        Target AS
        (
            SELECT *
            FROM Filtered
            WHERE Value2 IS NOT NULL
        )
        DELETE Target
        OUTPUT deleted.Id
        INTO Test2;
        """)]

        [InlineData("""
        WITH Filtered AS
        (
            SELECT *
            FROM Test
            WHERE IsActive = 0
        ),
        Target AS
        (
            SELECT *
            FROM Filtered
            WHERE Value2 IS NOT NULL
        )
        DELETE u
        OUTPUT deleted.Id
        INTO Test2
        FROM Target u;
        """)]
        public void Multiple_Ctes(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }
    }
}
