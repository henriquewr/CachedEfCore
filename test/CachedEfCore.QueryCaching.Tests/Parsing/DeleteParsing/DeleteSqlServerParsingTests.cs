using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlAnalysis.SqlServer;
using CachedEfCore.Tests.Common.Fixtures;
using System;
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
        [InlineData("DELETE TOP (SELECT 1) FROM Test;")]
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
        [InlineData("DELETE u FROM Test u WHERE Id = 1;")]
        [InlineData("DELETE u OUTPUT deleted.Value OldValue FROM Test u;")]
        [InlineData("DELETE Test OUTPUT deleted.Value OldValue;")]
        public void Output(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }
    }
}
