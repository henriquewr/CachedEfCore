using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlAnalysis.SqlServer;
using CachedEfCore.Tests.Common.Fixtures;
using System;
using Xunit;

namespace CachedEfCore.SqlAnalisys.Tests.Parsing.InsertParsing
{
    public class InsertSqlServerParsingTests : SqlServerParsingTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;
        private readonly SqlServerParser _sqlServerParser;

        public InsertSqlServerParsingTests(ServiceProviderFixture serviceProviderFixture)
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
        [InlineData("INSERT TOP (SELECT 1) INTO Test VALUES (1), ('2');")]
        [InlineData("INSERT TOP(10) INTO Test (A, B) VALUES (1, '2');")]
        [InlineData("INSERT TOP(((10))) Test (A, B) VALUES (1, 'a');")]
        [InlineData("INSERT TOP (((10))) Test VALUES (1);")]
        public void Top_Without_Percent(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("INSERT TOP (SELECT 1) PERCENT INTO Test VALUES (1);")]
        [InlineData("INSERT TOP(10) PERCENT INTO Test VALUES (1);")]
        [InlineData("INSERT TOP(((10))) PERCENT Test VALUES (1);")]
        [InlineData("INSERT TOP (((10))) PERCENT Test VALUES (1);")]
        public void Top_Percent(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("INSERT INTO Test VALUES (1);")]
        [InlineData("INSERT INTO Test VALUES (1), (2);")]
        [InlineData("INSERT INTO Test (Value) VALUES (1);")]
        [InlineData("INSERT INTO Test (Value) VALUES (1), (2);")]
        [InlineData("INSERT INTO dbo.Test VALUES (1);")]
        [InlineData("INSERT INTO [Test] VALUES (1);")]
        [InlineData("INSERT INTO Test DEFAULT VALUES;")]
        [InlineData("INSERT INTO Test OUTPUT inserted.Value VALUES (1);")]
        [InlineData("INSERT INTO Test OUTPUT inserted.Value AS NewValue VALUES (1);")]
        [InlineData("INSERT INTO Test OUTPUT ABS(-1) VALUES (1);")]
        [InlineData("INSERT INTO Test OUTPUT 1 + 2 VALUES (1);")]
        [InlineData("INSERT INTO Test OUTPUT inserted.Value + 2 VALUES (1);")]
        public void Insert(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }
    }
}
