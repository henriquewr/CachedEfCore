using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlAnalysis.SqlServer;
using CachedEfCore.Tests.Common.Fixtures;
using System;
using System.Linq;
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
        [InlineData("INSERT TOP (SELECT 1) INTO Test VALUES (1), (')');")]
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
        [InlineData("INSERT INTO Test (Value), (Value2) VALUES (1, ')'), (2, ')');")]
        [InlineData("INSERT dbo.Test VALUES (1);")]
        [InlineData("INSERT [Test] VALUES (1);")]
        public void Insert(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Default_Values()
        {
            var identifiers = _sqlServerParser.Parse("INSERT Test DEFAULT VALUES;");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("INSERT INTO Test OUTPUT inserted.Value SELECT u.Value FROM OtherTable AS u")]
        [InlineData("INSERT INTO Test OUTPUT inserted.Value SELECT Value FROM OtherTable")]
        [InlineData("INSERT INTO Test OUTPUT inserted.Value DEFAULT VALUES")]
        [InlineData("INSERT INTO Test OUTPUT inserted.Value VALUES (1);")]
        [InlineData("INSERT Test OUTPUT inserted.Value AS NewValue VALUES (1);")]
        [InlineData("INSERT INTO Test OUTPUT ABS(-1) VALUES (1);")]
        [InlineData("INSERT INTO Test OUTPUT 1 + 2 VALUES (1);")]
        [InlineData("INSERT INTO Test OUTPUT inserted.Value + 2 VALUES (1);")]
        [InlineData("INSERT Test OUTPUT inserted.Value + 2 EXEC dbo.Something;")]
        public void Output(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("INSERT INTO Test OUTPUT inserted.Value INTO Test2 SELECT Value FROM u.OtherTable AS u")]
        [InlineData("INSERT INTO Test OUTPUT inserted.Value INTO Test2 SELECT Value FROM OtherTable")]
        [InlineData("INSERT INTO Test OUTPUT inserted.Value INTO Test2 DEFAULT VALUES")]
        [InlineData("INSERT INTO Test OUTPUT inserted.Value INTO Test2 VALUES (1);")]
        [InlineData("INSERT Test OUTPUT inserted.Value AS NewValue INTO Test2 VALUES (1);")]
        [InlineData("INSERT INTO Test OUTPUT ABS(-1) INTO Test2 VALUES (1);")]
        [InlineData("INSERT INTO Test OUTPUT 1 + 2 INTO Test2 VALUES (1);")]
        [InlineData("INSERT INTO Test OUTPUT inserted.Value + 2 INTO Test2 VALUES (1);")]
        [InlineData("INSERT Test OUTPUT inserted.Value + 2 INTO Test2 EXEC dbo.Something;")]
        public void Output_Into(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("INSERT INTO Test WITH (ROWLOCK) OUTPUT inserted.Value INTO Test2 SELECT u.Value FROM OtherTable AS u")]
        [InlineData("INSERT INTO Test WITH (ROWLOCK) OUTPUT inserted.Value INTO Test2 SELECT Value FROM OtherTable")]
        [InlineData("INSERT INTO Test WITH (ROWLOCK, HOLDLOCK) OUTPUT inserted.Value INTO Test2 DEFAULT VALUES")]
        [InlineData("INSERT INTO Test WITH (ROWLOCK) OUTPUT inserted.Value INTO Test2 VALUES (1);")]
        [InlineData("INSERT Test WITH (ROWLOCK) OUTPUT inserted.Value AS NewValue INTO Test2 VALUES (1);")]
        [InlineData("INSERT Test WITH (ROWLOCK, HOLDLOCK) OUTPUT ABS(-1) INTO Test2 VALUES (1);")]
        [InlineData("INSERT INTO Test WITH (ROWLOCK) OUTPUT 1 + 2 INTO Test2 VALUES (1);")]
        [InlineData("INSERT INTO Test WITH (ROWLOCK) OUTPUT inserted.Value + 2 INTO Test2 VALUES (1);")]
        [InlineData("INSERT Test WITH (ROWLOCK, HOLDLOCK) OUTPUT inserted.Value + 2 INTO Test2 EXEC dbo.Something;")]
        public void With(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Fact(Skip = "Currently unsupported")]
        public void Dml_Table_Source()
        {
            var sql = """
            INSERT INTO Test (Value)
            SELECT Value
            FROM
            (
                MERGE Test2 t
                USING Source s ON t.Id = s.Id
                WHEN MATCHED THEN
                    UPDATE SET Value = s.Value
                OUTPUT inserted.Value
            ) AS x(Value);
            """;

            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }
    }
}
