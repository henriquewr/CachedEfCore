using CachedEfCore.PostgreSQL.SqlAnalysis;
using CachedEfCore.Tests.Common.Fixtures;
using System.Linq;
using Xunit;

namespace CachedEfCore.PostgreSQL.Tests.SqlAnalisys.Parsing.DoParsing
{
    public class DoPostgreSqlParsingTests : PostgreSqlParsingTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly PostgreSqlParser _postgreSqlParser;

        public DoPostgreSqlParsingTests(ServiceProviderFixture serviceProviderFixture) : base(serviceProviderFixture)
        {
            _postgreSqlParser = new PostgreSqlParser();
        }

        [Theory]
        [InlineData("""
        DO LANGUAGE plpgsql $$
        BEGIN
            UPDATE "Test"
            SET "Value" = 1;
        END;
        $$;
        """)]
        public void Do_Language(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        DO $outer$
        BEGIN
            UPDATE "Test"
            SET "Value" = $inner$nested string$inner$;

            UPDATE "Test2"
            SET "Value" = $inner$nested string$inner$;
        END;
        $outer$;
        """)]
        [InlineData("""
        DO $outer$
        BEGIN
            UPDATE "Test"
            SET "Value" = 'It''s a nested ''single-quoted'' string';

            UPDATE "Test2"
            SET "Value" = 'It''s a nested ''single-quoted'' string';
        END;
        $outer$;
        """)]
        [InlineData("""
        DO $outer$
        BEGIN
            UPDATE "Test"
            SET "Value" = $inner$It''s a ''nested'' string$inner$;

            UPDATE "Test2"
            SET "Value" = $inner$It''s a ''nested'' string$inner$;
        END;
        $outer$;
        """)]
        [InlineData("""
        DO $outer$
        BEGIN
            UPDATE "Test"
            SET "Value" = $inner$contains $value$another string$value$ inside$inner$;

            UPDATE "Test2"
            SET "Value" = $inner$contains $value$another string$value$ inside$inner$;
        END;
        $outer$;
        """)]
        [InlineData("""
        DO $$
        BEGIN
            UPDATE "Test"
            SET "Value" = $inner$contains $value$another string$value$ inside$inner$;

            UPDATE "Test2"
            SET "Value" = $inner$contains $value$another string$value$ inside$inner$;
        END;
        $$;
        """)]
        public void Dollar_Quoted_String(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
DO '
BEGIN
    UPDATE "Test"
    SET "Value" = ''nested string'';

    UPDATE "Test2"
    SET "Value" = ''nested string'';
END;
';
""")]
        [InlineData("""
DO 'BEGIN
    UPDATE "Test"
    SET "Value" = ''It''''s a nested string'';

    UPDATE "Test2"
    SET "Value" = ''It''''s a nested string'';
END;';
""")]
        [InlineData("""
DO 'BEGIN
    UPDATE "Test"
    SET "Value" = ''It''''s a ''''nested'''' string'';

    UPDATE "Test2"
    SET "Value" = ''It''''s a ''''nested'''' string'';
END;';
""")]
        [InlineData("""
DO 'BEGIN
    UPDATE "Test"
    SET "Value" = ''contains ''''another string'''' inside'';

    UPDATE "Test2"
    SET "Value" = ''contains ''''another string'''' inside'';
END;';
""")]
        [InlineData("""
DO LANGUAGE plpgsql 'BEGIN
    UPDATE "Test"
    SET "Value" = ''nested string'';

    UPDATE "Test2"
    SET "Value" = ''nested string'';
END;';
""")]
        public void Single_Quoted_String(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
DO e'
BEGIN
    UPDATE "Test"
    SET "Value" = e''nested string'';

    UPDATE "Test2"
    SET "Value" = ''nested string'';
END;
';
""")]
        [InlineData("""
DO e'BEGIN
    UPDATE "Test"
    SET "Value" = e''It''''s a nested string'';

    UPDATE "Test2"
    SET "Value" = e''It''''s a nested string'';
END;';
""")]
        [InlineData("""
DO e'BEGIN
    UPDATE "Test"
    SET "Value" = e''It''''s a ''''nested'''' string'';

    UPDATE "Test2"
    SET "Value" = e''It''''s a ''''nested'''' string'';
END;';
""")]
        [InlineData("""
DO e'BEGIN
    UPDATE "Test"
    SET "Value" = e''contains ''''another string'''' inside'';

    UPDATE "Test2"
    SET "Value" = e''contains ''''another string'''' inside'';
END;';
""")]
        [InlineData("""
DO LANGUAGE plpgsql E'BEGIN
    UPDATE "Test"
    SET "Value" = e''nested string'';

    UPDATE "Test2"
    SET "Value" = e''nested string'';
END;';
""")]
        public void Single_Quoted_Escaped_String(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
DO U&'
BEGIN
    UPDATE "Test"
    SET "Value" = ''nested string'';

    UPDATE "Test2"
    SET "Value" = ''nested string'';
END;
';
""")]
        [InlineData("""
DO U&'BEGIN
    UPDATE "Test"
    SET "Value" = ''It''''s a nested string'';

    UPDATE "Test2"
    SET "Value" = ''It''''s a nested string'';
END;';
""")]
        [InlineData("""
DO U&'BEGIN
    UPDATE "Test"
    SET "Value" = ''It''''s a ''''nested'''' string'';

    UPDATE "Test2"
    SET "Value" = ''It''''s a ''''nested'''' string'';
END;';
""")]
        [InlineData("""
DO U&'BEGIN
    UPDATE "Test"
    SET "Value" = ''contains ''''another string'''' inside'';

    UPDATE "Test2"
    SET "Value" = ''contains ''''another string'''' inside'';
END;' UESCAPE '!';
""")]
        [InlineData("""
DO LANGUAGE plpgsql U&'BEGIN
    UPDATE "Test"
    SET "Value" = ''nested string'';

    UPDATE "Test2"
    SET "Value" = ''nested string'';
END;' UESCAPE '!';
""")]
        public void Unicode_Escaped_With_Single_Quoted_String(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
DO U&'
BEGIN
    UPDATE "Test"
    SET "Value" = U&''nested string'';

    UPDATE "Test2"
    SET "Value" = U&''nested string'';
END;
';
""")]
        [InlineData("""
DO U&'BEGIN
    UPDATE "Test"
    SET "Value" = U&''It''''s a nested string'' UESCAPE ''!'';

    UPDATE "Test2"
    SET "Value" = U&''It''''s a nested string'';
END;';
""")]
        [InlineData("""
DO U&'BEGIN
    UPDATE "Test"
    SET "Value" = U&''It''''s a ''''nested'''' string'' UESCAPE ''!'';

    UPDATE "Test2"
    SET "Value" = U&''It''''s a ''''nested'''' string'';
END;' UESCAPE '!';
""")]
        [InlineData("""
DO U&'BEGIN
    UPDATE "Test"
    SET "Value" = U&''contains ''''another string'''' inside'' UESCAPE ''!'';

    UPDATE "Test2"
    SET "Value" = U&''contains ''''another string'''' inside'';
END;' UESCAPE '!';
""")]
        [InlineData("""
DO LANGUAGE plpgsql U&'BEGIN
    UPDATE "Test"
    SET "Value" = U&''nested string'' UESCAPE ''!'';

    UPDATE "Test2"
    SET "Value" = U&''nested string'';
END;';
""")]
        public void Unicode_Escaped_Single_Quoted_String(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }
    }
}
