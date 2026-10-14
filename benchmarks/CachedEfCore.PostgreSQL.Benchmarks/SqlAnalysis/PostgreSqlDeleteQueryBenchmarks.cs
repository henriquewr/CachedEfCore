using BenchmarkDotNet.Attributes;
using CachedEfCore.PostgreSQL.SqlAnalysis;
using Microsoft.VSDiagnostics;
using System;
using System.Collections.Generic;

namespace CachedEfCore.PostgreSQL.Benchmarks.SqlAnalysis
{
    [CPUUsageDiagnoser]
    [MemoryDiagnoser(true)]
    public class PostgreSqlDeleteQueryBenchmarks
    {
        private PostgreSqlParser _postgreSqlParser = null!;

        [GlobalSetup]
        public void Setup()
        {
            _postgreSqlParser = new PostgreSqlParser();
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Delete()
        {
            var identifiers = _postgreSqlParser.Parse("DELETE FROM Test;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Delete_Star()
        {
            var identifiers = _postgreSqlParser.Parse("""
            DELETE FROM Test *;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Delete_Only()
        {
            var identifiers = _postgreSqlParser.Parse("""
            DELETE FROM ONLY Test;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Delete_Alias()
        {
            var identifiers = _postgreSqlParser.Parse("""
            DELETE FROM Test AS t WHERE t.Id = 1;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Delete_Using()
        {
            var identifiers = _postgreSqlParser.Parse("""
            DELETE FROM Test AS t
            USING Test2 AS t2, Test3 AS t3
            WHERE t.Id = t2.Id
              AND t2.Id = t3.Id;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Delete_Where_Current_Of()
        {
            var identifiers = _postgreSqlParser.Parse("""
            DELETE FROM Test
            WHERE CURRENT OF test_cursor;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Delete_Returning()
        {
            var identifiers = _postgreSqlParser.Parse("""
            DELETE FROM Test
            RETURNING WITH (OLD AS old_row, NEW AS new_row) old_row.Id, new_row.Value;
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
            DELETE FROM Test
            USING Cte
            WHERE Test.Id = Cte.Id;
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
            DELETE FROM Test
            USING Cte
            WHERE Test.Id = Cte.Id;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte_With_State_Changing_Query()
        {
            var identifiers = _postgreSqlParser.Parse("""
            WITH Updated AS (
                DELETE FROM Test
                WHERE Id = 1
                RETURNING id, name
            )
            DELETE FROM Test2
            USING Updated
            WHERE Test2.user_id = Updated.id;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte_Multiple()
        {
            var identifiers = _postgreSqlParser.Parse("""
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
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte_As_Not_Materialized()
        {
            var identifiers = _postgreSqlParser.Parse("""
            WITH Cte AS NOT MATERIALIZED (
                DELETE FROM Test
                WHERE id = 1
                RETURNING id, name
            )
            DELETE FROM Test2
            USING Cte
            WHERE Test2.user_id = Cte.id;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte_As_Materialized()
        {
            var identifiers = _postgreSqlParser.Parse("""
            WITH Cte AS MATERIALIZED (
                DELETE FROM Test
                WHERE id = 1
                RETURNING id, name
            )
            DELETE FROM Test2
            USING Cte
            WHERE Test2.user_id = Cte.id;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte_Search()
        {
            var identifiers = _postgreSqlParser.Parse("""
            WITH Cte AS (
                DELETE FROM Test
                WHERE id = 1
                RETURNING id, name
            )
            SEARCH BREADTH FIRST BY id, name SET SearchOrder
            DELETE FROM Test2
            USING Cte
            WHERE Test2.user_id = Cte.id;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte_Cycle()
        {
            var identifiers = _postgreSqlParser.Parse("""
            WITH Cte AS (
                DELETE FROM Test
                WHERE id = 1
                RETURNING id, name
            )
            CYCLE id, name SET IsCycle TO true DEFAULT false USING CyclePath
            DELETE FROM Test2
            USING Cte
            WHERE Test2.user_id = Cte.id;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Nested_Cte()
        {
            var identifiers = _postgreSqlParser.Parse("""
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
            """);

            return identifiers;
        }
    }
}
