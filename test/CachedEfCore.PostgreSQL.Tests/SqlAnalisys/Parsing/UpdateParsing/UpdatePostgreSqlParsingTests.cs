using CachedEfCore.PostgreSQL.SqlAnalysis;
using CachedEfCore.Tests.Common.Fixtures;
using System.Linq;
using Xunit;

namespace CachedEfCore.PostgreSQL.Tests.SqlAnalisys.Parsing.UpdateParsing
{
    public class UpdatePostgreSqlParsingTests : PostgreSqlParsingTestBase, IClassFixture<ServiceProviderFixture>, ICteParsing
    {
        private readonly PostgreSqlParser _postgreSqlParser;

        public UpdatePostgreSqlParsingTests(ServiceProviderFixture serviceProviderFixture) : base(serviceProviderFixture)
        {
            _postgreSqlParser = new PostgreSqlParser();
        }

        [Theory]
        [InlineData("""
        UPDATE Test
        SET Value = 10
        WHERE Id = 1;
        """)]
        [InlineData("""
        UPDATE Test
        SET Value = Value + 1
        WHERE Id IN (SELECT Id FROM OtherTest)
        RETURNING Id, Value;
        """)]
        public void Update(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        UPDATE Test t
        SET Value = 10
        WHERE t.Id = 1;
        """)]
        [InlineData("""
        UPDATE Test AS t
        SET Value = 10
        WHERE t.Id = 1;
        """)]
        [InlineData("""
        UPDATE Test AS t
        SET Value = u.Value
        FROM UpdateSource AS u
        WHERE t.Id = u.Id;
        """)]
        public void Update_Alias(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        UPDATE ONLY Test
        SET Value = 10
        WHERE Id = 1;
        """)]
        [InlineData("""
        UPDATE ONLY Test AS t
        SET Value = 10
        WHERE t.Id = 1;
        """)]
        [InlineData("""
        UPDATE ONLY Test t
        SET Value = u.Value
        FROM UpdateSource AS u
        WHERE t.Id = u.Id;
        """)]
        [InlineData("""
        UPDATE ONLY Test
        SET Value = Value + 1
        WHERE Id IN (SELECT Id FROM OtherTest)
        RETURNING Id, Value;
        """)]
        public void Only(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        UPDATE Test*
        SET Value = 10
        WHERE Id = 1;
        """)]
        [InlineData("""
        UPDATE Test * AS t
        SET Value = t.Value + 1
        WHERE t.Id = 1;
        """)]
        [InlineData("""
        UPDATE Test* AS t
        SET Value = u.Value
        FROM UpdateSource AS u
        WHERE t.Id = u.Id;
        """)]
        public void Update_Star(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Set_Subquery()
        {
            var identifiers = _postgreSqlParser.Parse("UPDATE Test SET Status = (SELECT 1) FROM Test2 unused");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("UPDATE Test SET Status = Status + 1 FROM Table2 u;")]
        [InlineData("UPDATE Test SET Status = Status - 1 FROM Table2 u;")]
        [InlineData("UPDATE Test SET Status = Status * 1 FROM Table2 u;")]
        [InlineData("UPDATE Test SET Status = Status / 1 FROM Table2 u;")]
        [InlineData("UPDATE Test SET Status = Status % 1 FROM Table2 u;")]
        [InlineData("UPDATE Test SET Status = Status & 1 FROM Table2 u;")]
        [InlineData("UPDATE Test SET Status = Status ^ 1 FROM Table2 u;")]
        [InlineData("UPDATE Test SET Status = Status | 1 FROM Table2 u;")]
        public void Assignment_Operators(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("UPDATE Test SET Value = Value FROM Test u JOIN Test2 test2 ON u.Id = test2.Id WHERE Id = 1;")]
        public void Join(string sql)
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
        UPDATE Test
        SET Value = Cte.Value,
            Value2 = Cte.Value2
        FROM Cte
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
        UPDATE Test
        SET Value = Cte.Value,
            Value2 = Cte.Value2
        FROM Cte
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
            UPDATE Test
            SET Status = 1
            WHERE Id = 1
            RETURNING id, name
        )
        UPDATE Test2
        SET name = Updated.name
        FROM Updated
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
            UPDATE Test
            SET name = 'Chicken'
            WHERE id = 1
            RETURNING id, name
        ),
        Cte2 AS (
            UPDATE Test2
            SET name = 'Chicken'
            WHERE id = 1
            RETURNING id, name
        )
        UPDATE Test3
        SET name = Cte.name
        FROM Cte
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
            UPDATE Test
            SET name = 'Chicken'
            WHERE id = 1
            RETURNING id, name
        )
        UPDATE Test2
        SET name = Cte.name
        FROM Cte
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
            UPDATE Test
            SET name = 'Chicken'
            WHERE id = 1
            RETURNING id, name
        )
        UPDATE Test2
        SET name = Cte.name
        FROM Cte
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
            UPDATE Test
            SET name = 'Chicken'
            WHERE id = 1
            RETURNING id, name
        )
        SEARCH DEPTH FIRST BY id SET SearchOrder
        UPDATE Test2
        SET name = Cte.name
        FROM Cte
        WHERE Test2.user_id = Cte.id;
        """)]
        [InlineData("""
        WITH Cte AS (
            UPDATE Test
            SET name = 'Chicken'
            WHERE id = 1
            RETURNING id, name
        )
        SEARCH BREADTH FIRST BY id SET SearchOrder
        UPDATE Test2
        SET name = Cte.name
        FROM Cte
        WHERE Test2.user_id = Cte.id;
        """)]
        [InlineData("""
        WITH Cte AS (
            UPDATE Test
            SET name = 'Chicken'
            WHERE id = 1
            RETURNING id, name
        )
        SEARCH BREADTH FIRST BY id, name SET SearchOrder
        UPDATE Test2
        SET name = Cte.name
        FROM Cte
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
            UPDATE Test
            SET name = 'Chicken'
            WHERE id = 1
            RETURNING id, name
        )
        CYCLE id SET IsCycle USING CyclePath
        UPDATE Test2
        SET name = Cte.name
        FROM Cte
        WHERE Test2.user_id = Cte.id;
        """)]
        [InlineData("""
        WITH Cte AS (
            UPDATE Test
            SET name = 'Chicken'
            WHERE id = 1
            RETURNING id, name
        )
        CYCLE id SET IsCycle TO true DEFAULT false USING CyclePath
        UPDATE Test2
        SET name = Cte.name
        FROM Cte
        WHERE Test2.user_id = Cte.id;
        """)]
        [InlineData("""
        WITH Cte AS (
            UPDATE Test
            SET name = 'Chicken'
            WHERE id = 1
            RETURNING id, name
        )
        CYCLE id, name SET IsCycle TO true DEFAULT false USING CyclePath
        UPDATE Test2
        SET name = Cte.name
        FROM Cte
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
                UPDATE Test
                SET name = 'Chicken'
                WHERE id = 1
                RETURNING id, name
            )
            UPDATE Test2
            SET name = InnerCte.name
            FROM InnerCte
            WHERE Test2.user_id = InnerCte.id
            RETURNING Test2.user_id AS id, Test2.name
        )
        UPDATE Test3
        SET name = OuterCte.name
        FROM OuterCte
        WHERE Test3.user_id = OuterCte.id;
        """)]
        public void Nested_Cte(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2", "Test3"], identifiers.Select(x => x.ToString()).Order());
        }
    }
}
