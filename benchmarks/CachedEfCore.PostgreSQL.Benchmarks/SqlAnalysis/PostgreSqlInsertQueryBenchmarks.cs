using BenchmarkDotNet.Attributes;
using CachedEfCore.PostgreSQL.SqlAnalysis;
using Microsoft.VSDiagnostics;
using System;
using System.Collections.Generic;

namespace CachedEfCore.PostgreSQL.Benchmarks.SqlAnalysis
{
    [CPUUsageDiagnoser]
    [MemoryDiagnoser(true)]
    public class PostgreSqlInsertQueryBenchmarks
    {
        private PostgreSqlParser _postgreSqlParser = null!;

        [GlobalSetup]
        public void Setup()
        {
            _postgreSqlParser = new PostgreSqlParser();
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Insert()
        {
            var identifiers = _postgreSqlParser.Parse("INSERT INTO Test (Value, Value2) VALUES (1, ')'), (2, ')');");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Insert_Alias()
        {
            var identifiers = _postgreSqlParser.Parse("""
            INSERT INTO Test AS t (Value, Value2) VALUES (1, ')'), (2, ')');
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Insert_Overriding()
        {
            var identifiers = _postgreSqlParser.Parse("""
            INSERT INTO Test AS t OVERRIDING USER VALUE VALUES (1), (2);
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> On_Conflict_Parenthesis()
        {
            var identifiers = _postgreSqlParser.Parse("""
            INSERT INTO Test (id, name, age)
            VALUES (1, 'Gertrudez', 5)
            ON CONFLICT (lower(name))
            DO UPDATE
            SET name = EXCLUDED.name;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> On_Conflict_Where()
        {
            var identifiers = _postgreSqlParser.Parse("""
            INSERT INTO Test (id, name, age, active)
            VALUES (2, 'Geraldo', 5, false)
            ON CONFLICT (name) WHERE active = false
            DO NOTHING;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> On_Conflict_On_Constraint()
        {
            var identifiers = _postgreSqlParser.Parse("""
            INSERT INTO Test (id, name, age)
            VALUES (2, 'Geraldo', 5)
            ON CONFLICT ON CONSTRAINT name_constraint
            DO NOTHING;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Default_Values()
        {
            var identifiers = _postgreSqlParser.Parse("""
            INSERT INTO Test DEFAULT VALUES;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte()
        {
            var identifiers = _postgreSqlParser.Parse("""
            WITH Cte AS
            (
                SELECT *
                FROM Test2
            )
            INSERT INTO Test (Value, Value2)
            SELECT Value, Value2
            FROM Cte;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte_Recursive()
        {
            var identifiers = _postgreSqlParser.Parse("""
            WITH RECURSIVE Cte AS
            (
                SELECT *
                FROM Test2
            )
            INSERT INTO Test (Value, Value2)
            SELECT Value, Value2
            FROM Cte;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte_With_State_Changing_Query()
        {
            var identifiers = _postgreSqlParser.Parse("""
            WITH Cte AS (
                INSERT INTO Test (name)
                VALUES ('Chicken')
                RETURNING id, name
            )
            INSERT INTO Test2 (user_id, name)
            SELECT id, name
            FROM Cte;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte_Multiple()
        {
            var identifiers = _postgreSqlParser.Parse("""
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
            """);

            return identifiers;
        }
        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte_As_Not_Materialized()
        {
            var identifiers = _postgreSqlParser.Parse("""
            WITH Cte AS NOT MATERIALIZED (
                INSERT INTO Test (name)
                VALUES ('Chicken')
                RETURNING id, name
            )
            INSERT INTO Test2 (user_id, name)
            SELECT id, name
            FROM Cte;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte_As_Materialized()
        {
            var identifiers = _postgreSqlParser.Parse("""
            WITH Cte AS MATERIALIZED (
                INSERT INTO Test (name)
                VALUES ('Chicken')
                RETURNING id, name
            )
            INSERT INTO Test2 (user_id, name)
            SELECT id, name
            FROM Cte;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte_Search()
        {
            var identifiers = _postgreSqlParser.Parse("""
            WITH Cte AS (
                INSERT INTO Test (name)
                VALUES ('Chicken')
                RETURNING id, name
            )
            SEARCH BREADTH FIRST BY id, name SET SearchOrder
            INSERT INTO Test2 (user_id, name)
            SELECT id, name
            FROM Cte;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte_Cycle()
        {
            var identifiers = _postgreSqlParser.Parse("""
            WITH Cte AS (
                INSERT INTO Test (name)
                VALUES ('Chicken')
                RETURNING id, name
            )
            CYCLE id, name SET IsCycle TO true DEFAULT false USING CyclePath
            INSERT INTO Test2 (user_id, name)
            SELECT id, name
            FROM Cte;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Nested_Cte()
        {
            var identifiers = _postgreSqlParser.Parse("""
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
            """);

            return identifiers;
        }
    }
}
