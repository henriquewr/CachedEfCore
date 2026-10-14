using CachedEfCore.PostgreSQL.SqlAnalysis;
using CachedEfCore.Tests.Common.Fixtures;
using System.Linq;
using Xunit;

namespace CachedEfCore.PostgreSQL.Tests.SqlAnalisys.Parsing.InsertParsing
{
    public class InsertPostgreSqlParsingTests : PostgreSqlParsingTestBase, IClassFixture<ServiceProviderFixture>, ICteParsing
    {
        private readonly PostgreSqlParser _postgreSqlParser;

        public InsertPostgreSqlParsingTests(ServiceProviderFixture serviceProviderFixture) : base(serviceProviderFixture)
        {
            _postgreSqlParser = new PostgreSqlParser();
        }

        [Theory]
        [InlineData("INSERT INTO Test VALUES (1);")]
        [InlineData("INSERT INTO Test VALUES (1), (2);")]
        [InlineData("INSERT INTO Test (Value) VALUES (1);")]
        [InlineData("INSERT INTO Test (Value) VALUES (1), (2);")]
        [InlineData("INSERT INTO Test (Value, Value2) VALUES (1, ')'), (2, ')');")]
        [InlineData("INSERT INTO public.Test VALUES (1);")]
        [InlineData("INSERT INTO public. Test VALUES (1);")]
        [InlineData("INSERT INTO public . Test VALUES (1);")]
        [InlineData("INSERT INTO public .Test VALUES (1);")]
        [InlineData(@"INSERT INTO ""Test"" VALUES (1);")]
        public void Insert(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("INSERT INTO Test AS t VALUES (1);")]
        [InlineData("INSERT INTO Test AS t VALUES (1), (2);")]
        [InlineData("INSERT INTO Test AS t (Value) VALUES (1);")]
        [InlineData("INSERT INTO Test AS t (Value) VALUES (1), (2);")]
        [InlineData("INSERT INTO Test AS t (Value, Value2) VALUES (1, ')'), (2, ')');")]
        [InlineData("INSERT INTO public.Test AS t VALUES (1);")]
        [InlineData(@"INSERT INTO ""Test"" AS ""alias"" VALUES (1);")]
        public void Insert_Alias(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("INSERT INTO Test AS t OVERRIDING SYSTEM VALUE VALUES (1);")]
        [InlineData("INSERT INTO Test AS t OVERRIDING USER VALUE VALUES (1), (2);")]
        public void Insert_Overriding(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        INSERT INTO Test (id, name, age)
        VALUES (1, 'Maricota', 5)
        ON CONFLICT (id)
        DO UPDATE
        SET name = EXCLUDED.name;
        """)]
        [InlineData("""
        INSERT INTO Test (id, name, age)
        VALUES (1, 'Gertrudez', 5)
        ON CONFLICT (lower(name))
        DO UPDATE
        SET name = EXCLUDED.name;
        """)]
        [InlineData("""
        INSERT INTO Test (id, name, age)
        VALUES (2, 'Geraldo', 5)
        ON CONFLICT (name COLLATE "C")
        DO NOTHING;
        """)]
        [InlineData("""
        INSERT INTO Test (id, name, age)
        VALUES (3, 'Mariazinha', 1)
        ON CONFLICT (name text_pattern_ops)
        DO NOTHING;
        """)]
        [InlineData("""
        INSERT INTO Test (id, name, age)
        VALUES (3, 'Cristina', 1)
        ON CONFLICT (name, age)
        DO NOTHING;
        """)]
        public void On_Conflict_Parenthesis(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        INSERT INTO Test (id, name, age, active)
        VALUES (2, 'Geraldo', 5, false)
        ON CONFLICT (name) WHERE active = false
        DO NOTHING;
        """)]
        public void On_Conflict_Where(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        INSERT INTO Test (id, name, age)
        VALUES (2, 'Geraldo', 5)
        ON CONFLICT ON CONSTRAINT name_constraint
        DO NOTHING;
        """)]
        public void On_Conflict_On_Constraint(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Default_Values()
        {
            var identifiers = _postgreSqlParser.Parse("INSERT INTO Test DEFAULT VALUES;");

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        WITH Cte AS
        (
            SELECT *
            FROM Test2
        )
        INSERT INTO Test (Value, Value2)
        SELECT Value, Value2
        FROM Cte;
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
        INSERT INTO Test (Value, Value2)
        SELECT Value, Value2
        FROM Cte;
        """)]
        public void Cte_Recursive(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        WITH Cte AS (
            INSERT INTO Test (name)
            VALUES ('Chicken')
            RETURNING id, name
        )
        INSERT INTO Test2 (user_id, name)
        SELECT id, name
        FROM Cte;
        """)]
        public void Cte_With_State_Changing_Query(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
        WITH Cte AS (
            INSERT INTO Test (name)
            VALUES ('Chicken')
            RETURNING id, name
        ),
        Cte AS (
            INSERT INTO Test2 (name)
            VALUES ('Chicken')
            RETURNING id, name
        )
        INSERT INTO Test3 (user_id, name)
        SELECT id, name
        FROM Cte;
        """)]
        public void Cte_Multiple(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2", "Test3"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
        WITH Cte AS NOT MATERIALIZED (
            INSERT INTO Test (name)
            VALUES ('Chicken')
            RETURNING id, name
        )
        INSERT INTO Test2 (user_id, name)
        SELECT id, name
        FROM Cte;
        """)]
        public void Cte_As_Not_Materialized(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
        WITH Cte AS MATERIALIZED (
            INSERT INTO Test (name)
            VALUES ('Chicken')
            RETURNING id, name
        )
        INSERT INTO Test2 (user_id, name)
        SELECT id, name
        FROM Cte;
        """)]
        public void Cte_As_Materialized(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
        WITH Cte AS (
            INSERT INTO Test (name)
            VALUES ('Chicken')
            RETURNING id, name
        )
        SEARCH DEPTH FIRST BY id SET SearchOrder
        INSERT INTO Test2 (user_id, name)
        SELECT id, name
        FROM Cte;
        """)]
        [InlineData("""
        WITH Cte AS (
            INSERT INTO Test (name)
            VALUES ('Chicken')
            RETURNING id, name
        )
        SEARCH BREADTH FIRST BY id SET SearchOrder
        INSERT INTO Test2 (user_id, name)
        SELECT id, name
        FROM Cte;
        """)]
        [InlineData("""
        WITH Cte AS (
            INSERT INTO Test (name)
            VALUES ('Chicken')
            RETURNING id, name
        )
        SEARCH BREADTH FIRST BY id, name SET SearchOrder
        INSERT INTO Test2 (user_id, name)
        SELECT id, name
        FROM Cte;
        """)]
        public void Cte_Search(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
        WITH Cte AS (
            INSERT INTO Test (name)
            VALUES ('Chicken')
            RETURNING id, name
        )
        CYCLE id SET IsCycle USING CyclePath
        INSERT INTO Test2 (user_id, name)
        SELECT id, name
        FROM Cte;
        """)]
        [InlineData("""
        WITH Cte AS (
            INSERT INTO Test (name)
            VALUES ('Chicken')
            RETURNING id, name
        )
        CYCLE id SET IsCycle TO true DEFAULT false USING CyclePath
        INSERT INTO Test2 (user_id, name)
        SELECT id, name
        FROM Cte;
        """)]
        [InlineData("""
        WITH Cte AS (
            INSERT INTO Test (name)
            VALUES ('Chicken')
            RETURNING id, name
        )
        CYCLE id, name SET IsCycle TO true DEFAULT false USING CyclePath
        INSERT INTO Test2 (user_id, name)
        SELECT id, name
        FROM Cte;
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
                INSERT INTO Test (name)
                VALUES ('Chicken')
                RETURNING id, name
            )
            INSERT INTO Test2 (user_id, name)
            SELECT id, name
            FROM InnerCte
            RETURNING id, name
        )
        INSERT INTO Test3 (user_id, name)
        SELECT id, name
        FROM OuterCte;
        """)]
        public void Nested_Cte(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2", "Test3"], identifiers.Select(x => x.ToString()).Order());
        }
    }
}
