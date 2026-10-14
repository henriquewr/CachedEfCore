using CachedEfCore.PostgreSQL.SqlAnalysis;
using CachedEfCore.Tests.Common.Fixtures;
using Xunit;

namespace CachedEfCore.PostgreSQL.Tests.SqlAnalisys.Parsing.CopyParsing
{
    public class CopyPostgreSqlParsingTests : PostgreSqlParsingTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly PostgreSqlParser _postgreSqlParser;

        public CopyPostgreSqlParsingTests(ServiceProviderFixture serviceProviderFixture) : base(serviceProviderFixture)
        {
            _postgreSqlParser = new PostgreSqlParser();
        }

        [Theory]
        [InlineData("""
        COPY Test TO '/tmp/test.csv';
        """)]
        [InlineData("""
        COPY Test (id, name) TO STDOUT WITH (FORMAT csv, HEADER);
        """)]
        [InlineData("""
        COPY (SELECT id, name FROM Test) TO '/tmp/test.csv' WITH (FORMAT csv, HEADER);
        """)]
        [InlineData("""
        COPY (SELECT id, name FROM Test WHERE id > 10)
        TO PROGRAM 'gzip > /tmp/test.csv.gz' WITH (FORMAT csv);
        """)]
        public void Copy_To_Without_State_Changing(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Empty(identifiers);
        }

        [Theory]
        [InlineData("""
        COPY Test FROM '/tmp/test.csv';
        """)]
        [InlineData("""
        COPY Test (id, name) FROM '/tmp/test.csv' WITH (FORMAT csv, HEADER true);
        """)]
        [InlineData("""
        COPY Test FROM STDIN WITH (FORMAT text);
        """)]
        public void Copy_From(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        COPY (INSERT INTO Test (name) VALUES ('Chicken') RETURNING id, name)
        TO STDOUT WITH (FORMAT csv);
        """)]
        [InlineData("""
        COPY (UPDATE Test SET name = 'Chicken' WHERE id = 1 RETURNING id, name)
        TO STDOUT WITH (FORMAT csv, HEADER);
        """)]
        [InlineData("""
        COPY (DELETE FROM Test WHERE id = 1 RETURNING id, name)
        TO STDOUT WITH (FORMAT csv);
        """)]
        public void Copy_To_Query(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }
    }
}
