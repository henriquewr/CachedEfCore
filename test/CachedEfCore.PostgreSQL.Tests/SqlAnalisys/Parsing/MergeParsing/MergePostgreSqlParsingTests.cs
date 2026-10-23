using CachedEfCore.PostgreSQL.SqlAnalysis;
using CachedEfCore.Tests.Common.Fixtures;
using System.Linq;
using Xunit;

namespace CachedEfCore.PostgreSQL.Tests.SqlAnalisys.Parsing.MergeParsing
{
    public class MergePostgreSqlParsingTests : PostgreSqlParsingTestBase, IClassFixture<ServiceProviderFixture>, ICteParsing
    {
        private readonly PostgreSqlParser _postgreSqlParser;

        public MergePostgreSqlParsingTests(ServiceProviderFixture serviceProviderFixture) : base(serviceProviderFixture)
        {
            _postgreSqlParser = new PostgreSqlParser();
        }

        [Theory]
        [InlineData("""
            MERGE INTO Test AS t
            USING (
                VALUES
                    (@p20, 0),
                    (@p30, 1),
                    (@p40, 2),
                    (@p50, 3),
                    (@p60, 4)
            ) AS i (Value, _Position)
            ON FALSE
            WHEN NOT MATCHED THEN
                INSERT (Value)
                VALUES (i.Value)
            RETURNING t.Value, i._Position;
            """)]
        public void Merge(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
            MERGE INTO Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET
                    Status = 1,
                    Value = 3,
                    Other = 56;
            """)]
        public void Update_Multiple_Set(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Update_Set_String_Literal()
        {
            var identifiers = _postgreSqlParser.Parse("""
                MERGE INTO Test AS target
                USING Source AS source
                ON target.Id = source.Id
                WHEN MATCHED THEN
                    UPDATE SET Status = '1';
                """);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }


        [Fact]
        public void Update_Set_Func()
        {
            var identifiers = _postgreSqlParser.Parse("""
                MERGE INTO Test AS target
                USING Source AS source
                ON target.Id = source.Id
                WHEN MATCHED THEN
                    UPDATE SET Status = ABS(-1);
                """);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Update_Set_Subquery()
        {
            var identifiers = _postgreSqlParser.Parse("""
                MERGE INTO Test AS target
                USING Source AS source
                ON target.Id = source.Id
                WHEN MATCHED THEN
                    UPDATE SET Status = target.Status + 1;
                """);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN
            UPDATE SET Status = target.Status + 1
        """)]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN
            UPDATE SET Status = target.Status - 1
        """)]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN
            UPDATE SET Status = target.Status * 1
        """)]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN
            UPDATE SET Status = target.Status / 1
        """)]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN
            UPDATE SET Status = target.Status % 1
        """)]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN
            UPDATE SET Status = target.Status & 1
        """)]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN
            UPDATE SET Status = target.Status # 1
        """)]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN
            UPDATE SET Status = target.Status | 1
        """)]
        public void Update_Assignment_Operators(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN
            UPDATE SET Value = Value;
        """)]
        public void Update_Value_Keyword(string sql)
        {
            // VALUE is a keyword
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN
            UPDATE SET Value = 
                CASE
                    WHEN A = 1 THEN 'test'
                    WHEN A = 2 THEN 'test2'
                    ELSE Value
                END;
        """)]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN
            UPDATE SET Value = 
                CASE
                    WHEN A = 1 THEN 'test'
                END;
        """)]
        public void Set_Case_Expression(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id

        WHEN MATCHED
            AND source.IsDeleted = 1
        THEN
            DELETE

        WHEN MATCHED
            AND source.IsDeleted = 0
        THEN
            UPDATE SET
                Name = source.Name,
                Status = source.Status

        WHEN NOT MATCHED BY TARGET
            AND source.IsActive = 1
        THEN
            INSERT (Id, Name, Status)
            VALUES (source.Id, source.Name, source.Status)

        WHEN NOT MATCHED BY TARGET
            AND source.IsActive = 0
        THEN
            INSERT (Id, Name, Status)
            VALUES (source.Id, source.Name, 0)

        WHEN NOT MATCHED BY SOURCE
            AND target.IsArchived = 1
        THEN
            DELETE

        WHEN NOT MATCHED BY SOURCE
            AND target.IsArchived = 0
        THEN
            UPDATE SET
                Status = -1;
        """)]
        public void Multiple_When_Matched(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);
            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN DELETE
        RETURNING Value AS OldValue;
        """)]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN DELETE
        RETURNING 1 AS OldValue;
        """)]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN DELETE
        RETURNING 1 + 2 AS OldValue;
        """)]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN DELETE
        RETURNING Value + 2 AS OldValue;
        """)]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN DELETE
        RETURNING 'a' AS OldValue;
        """)]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN DELETE
        RETURNING ABS(-1) AS OldValue;
        """)]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN DELETE
        RETURNING ABS(-1) + 2 AS OldValue;
        """)]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN DELETE
        RETURNING ABS(-1) + 2;
        """)]
        public void Merge_Returning(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON CASE
               WHEN source.Id > 0
                   THEN CASE WHEN target.Id = source.Id THEN 1 ELSE 0 END
               ELSE 0
           END = 1
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (Id, Value)
            VALUES (source.Id, source.Value);
        """)]
        public void Merge_On_Case(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN NOT MATCHED BY TARGET AND
           CASE
               WHEN source.Value IS NOT NULL THEN 1
               ELSE 0
           END = 1
        THEN
            INSERT (Id, Value)
            VALUES (source.Id, source.Value);
        """)]
        public void Merge_Not_Matched_By_Target_And_Case(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN NOT MATCHED BY SOURCE AND
            CASE
                WHEN target.Value IS NOT NULL THEN 1
                ELSE 0
            END = 1
        THEN
            UPDATE SET Value = NULL;
        """)]
        public void Merge_Not_Matched_By_Source_And_Case(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        MERGE INTO Test AS target
        USING Source AS source
        ON target.Id = source.Id
        WHEN MATCHED AND
            CASE
                WHEN source.Value IS NOT NULL THEN 1
                ELSE 0
            END = 1
        THEN
            UPDATE SET Value = source.Value;
        """)]
        public void Merge_Matched_And_Case(string sql)
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
        MERGE INTO Test AS target
        USING Cte AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN
            UPDATE SET
                Value = source.Value,
                Value2 = source.Value2
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (Value, Value2)
            VALUES (source.Value, source.Value2);
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
        MERGE INTO Test AS target
        USING Cte AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN
            UPDATE SET
                Value = source.Value,
                Value2 = source.Value2
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (Value, Value2)
            VALUES (source.Value, source.Value2);
        """)]
        public void Cte_Recursive(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
        WITH Cte AS (
            MERGE INTO Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Value = source.Value
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (Value)
                VALUES (source.Value)
            RETURNING Id, Value
        )
        MERGE INTO Test2 AS target
        USING Cte AS source
        ON target.user_id = source.id
        WHEN MATCHED THEN
            UPDATE SET name = source.value
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (user_id, name)
            VALUES (source.id, source.value);
        """)]
        public void Cte_With_State_Changing_Query(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
        WITH Cte1 AS (
            MERGE INTO Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Value = source.Value
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (Value)
                VALUES (source.Value)
            RETURNING Id, Value
        ),
        Cte2 AS (
            MERGE INTO Test2 AS target
            USING Source2 AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Name = source.Name
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (Name)
                VALUES (source.Name)
            RETURNING Id, Name
        )
        MERGE INTO Test3 AS target
        USING Cte1 AS source
        ON target.user_id = source.Id
        WHEN MATCHED THEN
            UPDATE SET name = source.Value
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (user_id, name)
            VALUES (source.Id, source.Value);
        """)]
        public void Cte_Multiple(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2", "Test3"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
        WITH Cte AS NOT MATERIALIZED (
            MERGE INTO Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Value = source.Value
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (Value)
                VALUES (source.Value)
            RETURNING Id, Value
        )
        MERGE INTO Test2 AS target
        USING Cte AS source
        ON target.user_id = source.Id
        WHEN MATCHED THEN
            UPDATE SET name = source.Value
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (user_id, name)
            VALUES (source.Id, source.Value);
        """)]
        public void Cte_As_Not_Materialized(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
        WITH Cte AS MATERIALIZED (
            MERGE INTO Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Value = source.Value
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (Value)
                VALUES (source.Value)
            RETURNING Id, Value
        )
        MERGE INTO Test2 AS target
        USING Cte AS source
        ON target.user_id = source.Id
        WHEN MATCHED THEN
            UPDATE SET name = source.Value
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (user_id, name)
            VALUES (source.Id, source.Value);
        """)]
        public void Cte_As_Materialized(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
        WITH Cte AS (
            MERGE INTO Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Value = source.Value
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (Value)
                VALUES (source.Value)
            RETURNING Id, Value
        )
        SEARCH DEPTH FIRST BY Id SET SearchOrder
        MERGE INTO Test2 AS target
        USING Cte AS source
        ON target.user_id = source.Id
        WHEN MATCHED THEN
            UPDATE SET name = source.Value
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (user_id, name)
            VALUES (source.Id, source.Value);
        """)]
        [InlineData("""
        WITH Cte AS (
            MERGE INTO Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Value = source.Value
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (Value)
                VALUES (source.Value)
            RETURNING Id, Value
        )
        SEARCH BREADTH FIRST BY Id SET SearchOrder
        MERGE INTO Test2 AS target
        USING Cte AS source
        ON target.user_id = source.Id
        WHEN MATCHED THEN
            UPDATE SET name = source.Value
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (user_id, name)
            VALUES (source.Id, source.Value);
        """)]
        [InlineData("""
        WITH Cte AS (
            MERGE INTO Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Value = source.Value
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (Value)
                VALUES (source.Value)
            RETURNING Id, Value
        )
        SEARCH BREADTH FIRST BY Id, Value SET SearchOrder
        MERGE INTO Test2 AS target
        USING Cte AS source
        ON target.user_id = source.Id
        WHEN MATCHED THEN
            UPDATE SET name = source.Value
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (user_id, name)
            VALUES (source.Id, source.Value);
        """)]
        public void Cte_Search(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
        WITH Cte AS (
            MERGE INTO Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Value = source.Value
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (Value)
                VALUES (source.Value)
            RETURNING Id, Value
        )
        CYCLE Id SET IsCycle USING CyclePath
        MERGE INTO Test2 AS target
        USING Cte AS source
        ON target.user_id = source.Id
        WHEN MATCHED THEN
            UPDATE SET name = source.Value
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (user_id, name)
            VALUES (source.Id, source.Value);
        """)]
        [InlineData("""
        WITH Cte AS (
            MERGE INTO Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Value = source.Value
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (Value)
                VALUES (source.Value)
            RETURNING Id, Value
        )
        CYCLE Id SET IsCycle TO true DEFAULT false USING CyclePath
        MERGE INTO Test2 AS target
        USING Cte AS source
        ON target.user_id = source.Id
        WHEN MATCHED THEN
            UPDATE SET name = source.Value
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (user_id, name)
            VALUES (source.Id, source.Value);
        """)]
        [InlineData("""
        WITH Cte AS (
            MERGE INTO Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Value = source.Value
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (Value)
                VALUES (source.Value)
            RETURNING Id, Value
        )
        CYCLE Id, Value SET IsCycle TO true DEFAULT false USING CyclePath
        MERGE INTO Test2 AS target
        USING Cte AS source
        ON target.user_id = source.Id
        WHEN MATCHED THEN
            UPDATE SET name = source.Value
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (user_id, name)
            VALUES (source.Id, source.Value);
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
                MERGE INTO Test AS target
                USING Source AS source
                ON target.Id = source.Id
                WHEN MATCHED THEN
                    UPDATE SET Value = source.Value
                WHEN NOT MATCHED BY TARGET THEN
                    INSERT (Value)
                    VALUES (source.Value)
                RETURNING Id, Value
            )
            MERGE INTO Test2 AS target
            USING InnerCte AS source
            ON target.user_id = source.Id
            WHEN MATCHED THEN
                UPDATE SET name = source.Value
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (user_id, name)
                VALUES (source.Id, source.Value)
            RETURNING target.user_id AS Id, target.name AS Name
        )
        MERGE INTO Test3 AS target
        USING OuterCte AS source
        ON target.user_id = source.Id
        WHEN MATCHED THEN
            UPDATE SET name = source.Name
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (user_id, name)
            VALUES (source.Id, source.Name);
        """)]
        public void Nested_Cte(string sql)
        {
            var identifiers = _postgreSqlParser.Parse(sql);

            Assert.Equal(["Test", "Test2", "Test3"], identifiers.Select(x => x.ToString()).Order());
        }
    }
}
