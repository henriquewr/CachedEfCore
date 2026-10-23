using CachedEfCore.PostgreSQL.SqlAnalysis;
using CachedEfCore.Tests.Common.Fixtures;
using System.Linq;
using Xunit;

namespace CachedEfCore.PostgreSQL.Tests.SqlAnalisys.Parsing.ExecuteParsing
{
    public class ExecutePostgreSqlParsingTests : PostgreSqlParsingTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly PostgreSqlParser _postgreSqlParser;

        public ExecutePostgreSqlParsingTests(ServiceProviderFixture serviceProviderFixture) : base(serviceProviderFixture)
        {
            _postgreSqlParser = new PostgreSqlParser();
        }

        [Theory]
        [InlineData("""
DO LANGUAGE plpgsql $$
BEGIN
    EXECUTE $execute$
        UPDATE "Test"
        SET "Value" = 1;
    $execute$;
END;
$$;
""")]
        public void Do_Language_Execute(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
DO $outer$
BEGIN
    EXECUTE $execute$
        UPDATE "Test"
        SET "Value" = $inner$nested string$inner$;

        UPDATE "Test2"
        SET "Value" = $inner$nested string$inner$;
    $execute$;
END;
$outer$;
""")]
        [InlineData("""
DO $outer$
BEGIN
    EXECUTE $execute$
        UPDATE "Test"
        SET "Value" = 'It''s a nested ''single-quoted'' string';

        UPDATE "Test2"
        SET "Value" = 'It''s a nested ''single-quoted'' string';
    $execute$;
END;
$outer$;
""")]
        [InlineData("""
DO $outer$
BEGIN
    EXECUTE $execute$
        UPDATE "Test"
        SET "Value" = $inner$It''s a ''nested'' string$inner$;

        UPDATE "Test2"
        SET "Value" = $inner$It''s a ''nested'' string$inner$;
    $execute$;
END;
$outer$;
""")]
        [InlineData("""
DO $outer$
BEGIN
    EXECUTE $execute$
        UPDATE "Test"
        SET "Value" = $inner$contains $value$another string$value$ inside$inner$;

        UPDATE "Test2"
        SET "Value" = $inner$contains $value$another string$value$ inside$inner$;
    $execute$;
END;
$outer$;
""")]
        [InlineData("""
DO $$
BEGIN
    EXECUTE $execute$
        UPDATE "Test"
        SET "Value" = $inner$contains $value$another string$value$ inside$inner$;

        UPDATE "Test2"
        SET "Value" = $inner$contains $value$another string$value$ inside$inner$;
    $execute$;
END;
$$;
""")]
        public void Dollar_Quoted_String_Execute(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
DO '
BEGIN
    EXECUTE ''
        UPDATE "Test"
        SET "Value" = ''''nested string''''
    '';

    EXECUTE ''
        UPDATE "Test2"
        SET "Value" = ''''nested string''''
    '';
END;
';
""")]
        [InlineData("""
DO 'BEGIN
    EXECUTE ''UPDATE "Test" SET "Value" = ''''It''''''''s a nested string'''''';

    EXECUTE ''UPDATE "Test2" SET "Value" = ''''It''''''''s a nested string'''''';
END;';
""")]
        public void Single_Quoted_String_Execute(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
DO E'
BEGIN
    EXECUTE E''
        UPDATE "Test"
        SET "Value" = ''''nested string''''
    '';

    EXECUTE E''
        UPDATE "Test2"
        SET "Value" = ''''nested string''''
    '';
END;
';
""")]
        [InlineData("""
DO E'BEGIN
    EXECUTE e''UPDATE "Test" SET "Value" = ''''It''''''''s a nested string'''''';

    EXECUTE e''UPDATE "Test2" SET "Value" = ''''It''''''''s a nested string'''''';
END;';
""")]
        public void Single_Quoted_Escaped_String_Execute(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
DO U&'
BEGIN
    EXECUTE ''UPDATE "Test" SET "Value" = ''''nested string'''''';
    EXECUTE ''UPDATE "Test2" SET "Value" = ''''nested string'''''';
END;
';
""")]
        [InlineData("""
DO U&'BEGIN
    EXECUTE ''UPDATE "Test" SET "Value" = ''''It''''''''s a nested string'''''';
    EXECUTE ''UPDATE "Test2" SET "Value" = ''''It''''''''s a nested string'''''';
END;';
""")]
        [InlineData("""
DO U&'BEGIN
    EXECUTE ''UPDATE "Test" SET "Value" = ''''It''''''''s a ''''''''''nested'''''''' string'''''';
    EXECUTE ''UPDATE "Test2" SET "Value" = ''''It''''''''s a ''''''''''nested'''''''' string'''''';
END;';
""")]
        [InlineData("""
DO U&'BEGIN
    EXECUTE ''UPDATE "Test" SET "Value" = ''''contains ''''''''''another string'''''''' inside'''''';
    EXECUTE ''UPDATE "Test2" SET "Value" = ''''contains ''''''''''another string'''''''' inside'''''';
END;' UESCAPE '!';
""")]
        [InlineData("""
DO LANGUAGE plpgsql U&'BEGIN
    EXECUTE ''UPDATE "Test" SET "Value" = ''''nested string'''''';
    EXECUTE ''UPDATE "Test2" SET "Value" = ''''nested string'''''';
END;' UESCAPE '!';
""")]
        public void Unicode_Escaped_With_Single_Quoted_String_Execute(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
DO U&'
BEGIN
    EXECUTE ''UPDATE "Test" SET "Value" = U&''''nested string'''''';
    EXECUTE ''UPDATE "Test2" SET "Value" = U&''''nested string'''''';
END;
';
""")]
        [InlineData("""
DO U&'BEGIN
    EXECUTE ''UPDATE "Test" SET "Value" = U&''''It''''''''s a nested string'''' UESCAPE ''''!'''''';
    EXECUTE ''UPDATE "Test2" SET "Value" = U&''''It''''''''s a nested string'''''';
END;';
""")]
        [InlineData("""
DO U&'BEGIN
    EXECUTE ''UPDATE "Test" SET "Value" = U&''''It''''''''s a ''''''''nested'''''''' string'''' UESCAPE ''''!!'''';'';
    EXECUTE ''UPDATE "Test2" SET "Value" = U&''''It''''''''s a ''''''''nested'''''''' string'''';'';
END;' UESCAPE '!';
""")]
        [InlineData("""
DO U&'BEGIN
    EXECUTE ''UPDATE "Test" SET "Value" = U&''''contains ''''''''''another string'''''''' inside'''' UESCAPE ''''!'''''';
    EXECUTE ''UPDATE "Test2" SET "Value" = U&''''contains ''''''''''another string'''''''' inside'''''';
END;' UESCAPE '@';
""")]
        [InlineData("""
DO LANGUAGE plpgsql U&'BEGIN
    EXECUTE ''UPDATE "Test" SET "Value" = U&''''nested string'''' UESCAPE ''''!'''''';
    EXECUTE ''UPDATE "Test2" SET "Value" = U&''''nested string'''''';
END;';
""")]
        public void Unicode_Escaped_Single_Quoted_String_Execute(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }
    }
}
