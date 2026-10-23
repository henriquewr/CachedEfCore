using BenchmarkDotNet.Attributes;
using CachedEfCore.PostgreSQL.SqlAnalysis;
using Microsoft.VSDiagnostics;
using System;
using System.Collections.Generic;

namespace CachedEfCore.PostgreSQL.Benchmarks.SqlAnalysis
{
    [CPUUsageDiagnoser]
    [MemoryDiagnoser(true)]
    public class PostgreSqlUpdateQueryBenchmarks
    {
        private PostgreSqlParser _postgreSqlParser = null!;

        [GlobalSetup]
        public void Setup()
        {
            _postgreSqlParser = new PostgreSqlParser();
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Update()
        {
            var identifiers = _postgreSqlParser.Parse("""
            UPDATE Test
            SET Value = Value + 1
            WHERE Id IN (SELECT Id FROM OtherTest)
            RETURNING Id, Value;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Update_Alias()
        {
            var identifiers = _postgreSqlParser.Parse("""
            UPDATE Test AS t
            SET Value = u.Value
            FROM UpdateSource AS u
            WHERE t.Id = u.Id;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Only()
        {
            var identifiers = _postgreSqlParser.Parse("""
            UPDATE ONLY Test
            SET Value = Value + 1
            WHERE Id IN (SELECT Id FROM OtherTest)
            RETURNING Id, Value;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Update_Star()
        {
            var identifiers = _postgreSqlParser.Parse("""
            UPDATE Test* AS t
            SET Value = u.Value
            FROM UpdateSource AS u
            WHERE t.Id = u.Id;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Set_Subquery()
        {
            var identifiers = _postgreSqlParser.Parse("""
            UPDATE Test SET Status = (SELECT 1) FROM Test2 unused
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Assignment_Operators()
        {
            var identifiers = _postgreSqlParser.Parse("""
            UPDATE Test SET Status = Status + 1 FROM Table2 u;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Join()
        {
            var identifiers = _postgreSqlParser.Parse("""
            UPDATE Test SET Value = Value FROM Test u JOIN Test2 test2 ON u.Id = test2.Id WHERE Id = 1;
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
            UPDATE Test
            SET Value = Cte.Value,
                Value2 = Cte.Value2
            FROM Cte
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
            UPDATE Test
            SET Value = Cte.Value,
                Value2 = Cte.Value2
            FROM Cte
            WHERE Test.Id = Cte.Id;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte_With_State_Changing_Query()
        {
            var identifiers = _postgreSqlParser.Parse("""
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
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte_Multiple()
        {
            var identifiers = _postgreSqlParser.Parse("""
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
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte_As_Not_Materialized()
        {
            var identifiers = _postgreSqlParser.Parse("""
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
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte_As_Materialized()
        {
            var identifiers = _postgreSqlParser.Parse("""
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
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte_Search()
        {
            var identifiers = _postgreSqlParser.Parse("""
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
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte_Cycle()
        {
            var identifiers = _postgreSqlParser.Parse("""
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
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Nested_Cte()
        {
            var identifiers = _postgreSqlParser.Parse("""
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
            """);

            return identifiers;
        }
    }
}
