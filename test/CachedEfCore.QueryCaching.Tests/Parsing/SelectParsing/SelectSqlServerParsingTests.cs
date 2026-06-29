using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlAnalysis.SqlServer;
using CachedEfCore.Tests.Common.Fixtures;
using System;
using System.Diagnostics.Metrics;
using System.Linq;
using Xunit;

namespace CachedEfCore.SqlAnalisys.Tests.Parsing.SelectParsing
{
    public class SelectSqlServerParsingTests : SqlServerParsingTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;
        private readonly SqlServerParser _sqlServerParser;

        public SelectSqlServerParsingTests(ServiceProviderFixture serviceProviderFixture)
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
        [InlineData("SELECT TOP (4) id, value FROM Test;")]
        [InlineData("SELECT TOP (3) * FROM Test;")]
        [InlineData("SELECT TOP (3) * FROM Test WHERE id = 1;")]
        [InlineData("SELECT TOP (3) id, value FROM Test WHERE id = 1;")]
        [InlineData("SELECT TOP (1) id, value FROM Test WHERE id = 1 AND value = 1;")]
        [InlineData("SELECT TOP( CASE WHEN 1 = 1 THEN 10 ELSE 5 END) * FROM Test;")]
        public void Top_Without_Percent(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Empty(identifiers);
        }

        [Theory]
        [InlineData("SELECT TOP (4) PERCENT id, value FROM Test;")]
        [InlineData("SELECT TOP (3) PERCENT * FROM Test;")]
        [InlineData("SELECT TOP (3) PERCENT * FROM Test WHERE id = 1;")]
        [InlineData("SELECT TOP (3) PERCENT id, value FROM Test WHERE id = 1;")]
        [InlineData("SELECT TOP (12) PERCENT id, value FROM Test WHERE id = 1 AND value = 1;")]
        [InlineData("SELECT TOP( CASE WHEN 1 = 1 THEN 10 ELSE 5 END) PERCENT * FROM Test;")]
        public void Top_With_Percent(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Empty(identifiers);
        }

        [Theory]
        [InlineData("SELECT id, value FROM Test;")]
        [InlineData("SELECT * FROM Test;")]
        [InlineData("SELECT * FROM Test WHERE id = 1;")]
        [InlineData("SELECT id, value FROM Test WHERE id = 1;")]
        [InlineData("SELECT id, value FROM Test WHERE id = 1 AND value = 1;")]
        [InlineData("SELECT (CASE WHEN 1 = 1 THEN 10 ELSE 5 END);")]
        [InlineData("SELECT MAX(CASE WHEN 1 = 1 THEN 10 ELSE 5 END);")]
        public void Regular_Select(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Empty(identifiers);
        }

        [Theory]
        [InlineData("SELECT DISTINCT Country FROM Test;")]
        public void Select_Distinct(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Empty(identifiers);
        }
    }
}
