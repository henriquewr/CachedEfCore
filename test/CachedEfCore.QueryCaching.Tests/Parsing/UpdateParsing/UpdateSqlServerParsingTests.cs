using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlAnalysis.SqlServer;
using CachedEfCore.Tests.Common.Fixtures;
using System;
using Xunit;

namespace CachedEfCore.SqlAnalisys.Tests.Parsing.UpdateParsing
{
    public class UpdateSqlServerParsingTests : SqlServerParsingTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;
        private readonly SqlServerParser _sqlServerParser;

        public UpdateSqlServerParsingTests(ServiceProviderFixture serviceProviderFixture)
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
        [InlineData("UPDATE TOP (SELECT 1) Test SET Status = 1;")]
        [InlineData("UPDATE TOP(10) Test SET Status = 1;")]
        [InlineData("UPDATE TOP(((10))) Test SET Status = 1;")]
        [InlineData("UPDATE TOP (((10))) Test SET Status = 1;")]
        public void Top(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("UPDATE TOP (SELECT 1) PERCENT Test SET Status = 1;")]
        [InlineData("UPDATE TOP(10) PERCENT Test SET Status = 1;")]
        [InlineData("UPDATE TOP(((10))) PERCENT Test SET Status = 1;")]
        [InlineData("UPDATE TOP (((10))) PERCENT Test SET Status = 1;")]
        public void Top_Percent(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("UPDATE Test WITH (ROWLOCK) SET Status = 1;")]
        [InlineData("UPDATE Test WITH (ROWLOCK, UPDLOCK) SET Status = 1;")]
        public void With_Hint(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("UPDATE Test SET Status = 1, Value = 3;")]
        [InlineData("UPDATE Test SET Status = 1, Value = 3, Other = 56;")]
        public void Multiple_Set(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Set_String_Literal()
        {
            var identifiers = _sqlServerParser.Parse("UPDATE u SET Status = '1' FROM Test u");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Set_Func()
        {
            var identifiers = _sqlServerParser.Parse("UPDATE u SET Status = ABS(-1) FROM Test u");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Set_Subquery()
        {
            var identifiers = _sqlServerParser.Parse("UPDATE u SET Status = (SELECT 1) FROM Test u");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("UPDATE u SET Status += 1 FROM Test u;")]
        [InlineData("UPDATE u SET Status -= 1 FROM Test u;")]
        [InlineData("UPDATE u SET Status *= 1 FROM Test u;")]
        [InlineData("UPDATE u SET Status /= 1 FROM Test u;")]
        [InlineData("UPDATE u SET Status %= 1 FROM Test u;")]
        [InlineData("UPDATE u SET Status &= 1 FROM Test u;")]
        [InlineData("UPDATE u SET Status ^= 1 FROM Test u;")]
        [InlineData("UPDATE u SET Status |= 1 FROM Test u;")]
        public void Assignment_Operators(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Set_Write()
        {
            var identifiers = _sqlServerParser.Parse("UPDATE u SET Value.WRITE('SQL ', 6, 0) FROM Test u WHERE Id = 1;");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("UPDATE Test SET Value = Value WHERE Id = 1;")]
        [InlineData("UPDATE u SET Value = Value FROM Test u WHERE Id = 1;")]
        [InlineData("UPDATE u SET Value = Value OUTPUT inserted.Value NewValue FROM Test u;")]
        [InlineData("UPDATE Test SET Value = Value OPTION (RECOMPILE);")]
        public void Update_Value_Keyword(string sql)
        {
            // VALUE is a keyword
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("UPDATE u SET u.Value = 1 OUTPUT deleted.Value AS OldValue, inserted.Value NewValue FROM Test u;")]
        [InlineData("UPDATE u SET u.Value = 1 OUTPUT 1 OldValue, inserted.Value AS NewValue FROM Test u;")]
        [InlineData("UPDATE u SET u.Value = 1 OUTPUT 1 + 2 OldValue FROM Test u;")]
        [InlineData("UPDATE u SET u.Value = 1 OUTPUT deleted.Value + 2 OldValue FROM Test u;")]
        [InlineData("UPDATE u SET u.Value = 1 OUTPUT 'a' OldValue, inserted.Value AS NewValue FROM Test u;")]
        [InlineData("UPDATE u SET u.Value = 1 OUTPUT ABS(-1) OldValue, inserted.Value AS NewValue FROM Test u;")]
        [InlineData("UPDATE u SET u.Value = 1 OUTPUT ABS(-1) + 2 OldValue FROM Test u;")]
        [InlineData("UPDATE u SET u.Value = 1 OUTPUT ABS(-1) + 2 FROM Test u;")]
        [InlineData("UPDATE u SET Value = Value FROM Test u WHERE Id = 1;")]
        [InlineData("UPDATE u SET Value = Value OUTPUT inserted.Value NewValue FROM Test u;")]
        public void Output(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Option()
        {
            var identifiers = _sqlServerParser.Parse("UPDATE Test SET Value = Value OPTION (RECOMPILE);");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }
    }
}
