using CachedEfCore.PostgreSQL.SqlAnalysis;
using CachedEfCore.Tests.Common.Fixtures;
using System.Linq;
using Xunit;

namespace CachedEfCore.PostgreSQL.Tests.SqlAnalisys.Parsing.DeleteParsing
{
    public class DeletePostgreSqlParsingTests : PostgreSqlParsingTestBase, IClassFixture<ServiceProviderFixture>, ICteParsing
    {
        private readonly PostgreSqlParser _postgreSqlParser;

        public DeletePostgreSqlParsingTests(ServiceProviderFixture serviceProviderFixture) : base(serviceProviderFixture)
        {
            _postgreSqlParser = new PostgreSqlParser();
        }

        [Theory]
        [InlineData("DELETE FROM Test;")]
        public void Delete(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("DELETE FROM Test *;")]
        public void Delete_Star(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("DELETE FROM ONLY Test;")]
        public void Delete_Only(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("DELETE FROM Test AS t WHERE t.Id = 1;")]
        [InlineData("DELETE FROM Test t WHERE t.Id = 1;")]
        public void Delete_Alias(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        DELETE FROM Test
        USING Test2
        WHERE Test.Id = Test2.Id;
        """)]
        [InlineData("""
        DELETE FROM Test AS t
        USING Test2 AS t2, Test3 AS t3
        WHERE t.Id = t2.Id
          AND t2.Id = t3.Id;
        """)]
        public void Delete_Using(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        DELETE FROM Test
        WHERE CURRENT OF test_cursor;
        """)]
        public void Delete_Where_Current_Of(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        DELETE FROM Test
        RETURNING Id, Value AS DeletedValue;
        """)]
        [InlineData("""
        DELETE FROM Test
        RETURNING WITH (OLD AS old_row) old_row.*;
        """)]
        [InlineData("""
        DELETE FROM Test
        RETURNING WITH (OLD AS old_row, NEW AS new_row) old_row.Id, new_row.Value;
        """)]
        public void Delete_Returning(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        WITH Cte AS
        (
            SELECT *
            FROM Test2
        )
        DELETE FROM Test
        USING Cte
        WHERE Test.Id = Cte.Id;
        """)]
        public void Cte(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        WITH RECURSIVE Cte AS
        (
            SELECT *
            FROM Test2
        )
        DELETE FROM Test
        USING Cte
        WHERE Test.Id = Cte.Id;
        """)]
        public void Cte_Recursive(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        WITH Updated AS (
            DELETE FROM Test
            WHERE Id = 1
            RETURNING id, name
        )
        DELETE FROM Test2
        USING Updated
        WHERE Test2.user_id = Updated.id;
        """)]
        public void Cte_With_State_Changing_Query(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
        WITH Cte AS (
            DELETE FROM Test
            WHERE id = 1
            RETURNING id, name
        ),
        Cte2 AS (
            DELETE FROM Test2
            WHERE id = 1
            RETURNING id, name
        )
        DELETE FROM Test3
        USING Cte
        WHERE Test3.user_id = Cte.id;
        """)]
        public void Cte_Multiple(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2", "Test3"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
        WITH Cte AS NOT MATERIALIZED (
            DELETE FROM Test
            WHERE id = 1
            RETURNING id, name
        )
        DELETE FROM Test2
        USING Cte
        WHERE Test2.user_id = Cte.id;
        """)]
        public void Cte_As_Not_Materialized(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
        WITH Cte AS MATERIALIZED (
            DELETE FROM Test
            WHERE id = 1
            RETURNING id, name
        )
        DELETE FROM Test2
        USING Cte
        WHERE Test2.user_id = Cte.id;
        """)]
        public void Cte_As_Materialized(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
        WITH Cte AS (
            DELETE FROM Test
            WHERE id = 1
            RETURNING id, name
        )
        SEARCH DEPTH FIRST BY id SET SearchOrder
        DELETE FROM Test2
        USING Cte
        WHERE Test2.user_id = Cte.id;
        """)]
        [InlineData("""
        WITH Cte AS (
            DELETE FROM Test
            WHERE id = 1
            RETURNING id, name
        )
        SEARCH BREADTH FIRST BY id SET SearchOrder
        DELETE FROM Test2
        USING Cte
        WHERE Test2.user_id = Cte.id;
        """)]
        [InlineData("""
        WITH Cte AS (
            DELETE FROM Test
            WHERE id = 1
            RETURNING id, name
        )
        SEARCH BREADTH FIRST BY id, name SET SearchOrder
        DELETE FROM Test2
        USING Cte
        WHERE Test2.user_id = Cte.id;
        """)]
        public void Cte_Search(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
        WITH Cte AS (
            DELETE FROM Test
            WHERE id = 1
            RETURNING id, name
        )
        CYCLE id SET IsCycle USING CyclePath
        DELETE FROM Test2
        USING Cte
        WHERE Test2.user_id = Cte.id;
        """)]
        [InlineData("""
        WITH Cte AS (
            DELETE FROM Test
            WHERE id = 1
            RETURNING id, name
        )
        CYCLE id SET IsCycle TO true DEFAULT false USING CyclePath
        DELETE FROM Test2
        USING Cte
        WHERE Test2.user_id = Cte.id;
        """)]
        [InlineData("""
        WITH Cte AS (
            DELETE FROM Test
            WHERE id = 1
            RETURNING id, name
        )
        CYCLE id, name SET IsCycle TO true DEFAULT false USING CyclePath
        DELETE FROM Test2
        USING Cte
        WHERE Test2.user_id = Cte.id;
        """)]
        public void Cte_Cycle(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
        WITH OuterCte AS (
            WITH InnerCte AS (
                DELETE FROM Test
                WHERE id = 1
                RETURNING id, name
            )
            DELETE FROM Test2
            USING InnerCte
            WHERE Test2.user_id = InnerCte.id
            RETURNING Test2.user_id AS id, Test2.name
        )
        DELETE FROM Test3
        USING OuterCte
        WHERE Test3.user_id = OuterCte.id;
        """)]
        public void Nested_Cte(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2", "Test3"], identifiers.Select(x => x.ToString()).Order());
        }
    }
}
