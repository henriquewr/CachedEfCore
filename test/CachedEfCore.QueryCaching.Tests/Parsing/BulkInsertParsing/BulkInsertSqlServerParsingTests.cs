using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlAnalysis.SqlServer;
using CachedEfCore.Tests.Common.Fixtures;
using System;
using Xunit;

namespace CachedEfCore.SqlAnalisys.Tests.Parsing.BulkInsertParsing
{
    public class BulkInsertSqlServerParsingTests : SqlServerParsingTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;
        private readonly SqlServerParser _sqlServerParser;

        public BulkInsertSqlServerParsingTests(ServiceProviderFixture serviceProviderFixture)
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
        [InlineData("BULK INSERT Test FROM 'file.csv'")]
        [InlineData("BULK INSERT dbo.Test FROM 'file.csv'")]
        [InlineData("BULK INSERT CachedEfCoreDb.dbo.Test FROM 'file.csv'")]
        public void Bulk_Insert_Should_Get_Table_Name(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
            BULK INSERT Test 
            FROM 'file.csv' 
            WITH ( 
                DATAFILETYPE = 'char', 
                KEEPNULLS, 
                TABLOCK, 
                MAXERRORS = 10, 
                FIRSTROW = 2
            )
            """)]
        public void Bulk_Insert_With_Clause(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }
    }
}
