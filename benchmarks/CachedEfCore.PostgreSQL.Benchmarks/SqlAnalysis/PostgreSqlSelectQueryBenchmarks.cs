using BenchmarkDotNet.Attributes;
using CachedEfCore.PostgreSQL.SqlAnalysis;
using Microsoft.VSDiagnostics;
using System;
using System.Collections.Generic;

namespace CachedEfCore.PostgreSQL.Benchmarks.SqlAnalysis
{
    [CPUUsageDiagnoser]
    [MemoryDiagnoser(true)]
    public class PostgreSqlSelectQueryBenchmarks
    {
        private PostgreSqlParser _postgreSqlParser = null!;

        [GlobalSetup]
        public void Setup()
        {
            _postgreSqlParser = new PostgreSqlParser();
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Select_From()
        {
            var identifiers = _postgreSqlParser.Parse("""
            SELECT * FROM public.Test AS t;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Select_Distinct()
        {
            var identifiers = _postgreSqlParser.Parse("""
            SELECT DISTINCT ON (Name) Id, Name FROM Test ORDER BY Name, Id;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Select_All()
        {
            var identifiers = _postgreSqlParser.Parse("""
            SELECT ALL Name FROM Test WHERE Id > 0;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Select_Join()
        {
            var identifiers = _postgreSqlParser.Parse("""
            SELECT * FROM Test RIGHT OUTER JOIN Test2 ON Test.Id = Test2.Id;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Select_Cte()
        {
            var identifiers = _postgreSqlParser.Parse("""
            WITH Cte AS (
                SELECT * FROM Test
            ),
            Cte2 AS (
                SELECT * FROM Test2
            )
            SELECT * FROM Cte JOIN Cte2 ON Cte.Id = Cte2.Id;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Select_Recursive()
        {
            var identifiers = _postgreSqlParser.Parse("""
            WITH RECURSIVE Cte AS (
                SELECT Id, ParentId FROM Test WHERE ParentId IS NULL
                UNION ALL
                SELECT t.Id, t.ParentId
                FROM Test t
                JOIN Cte c ON t.ParentId = c.Id
            )
            SELECT * FROM Cte;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Select_Where()
        {
            var identifiers = _postgreSqlParser.Parse("""
            SELECT * FROM Test WHERE EXISTS (SELECT 1 FROM Test2);
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Select_GroupBy()
        {
            var identifiers = _postgreSqlParser.Parse("""
            SELECT Name, COUNT(*) FROM Test GROUP BY Name HAVING COUNT(*) > 1;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Select_OrderBy()
        {
            var identifiers = _postgreSqlParser.Parse("""
            SELECT * FROM Test ORDER BY Name NULLS FIRST;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Select_LimitOffsetFetch()
        {
            var identifiers = _postgreSqlParser.Parse("""
            SELECT * FROM Test FETCH FIRST 10 ROWS WITH TIES;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Select_SetOperation()
        {
            var identifiers = _postgreSqlParser.Parse("""
            SELECT Id FROM Test INTERSECT ALL SELECT Id FROM Test2;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Select_For_Update_Skip_Locked()
        {
            var identifiers = _postgreSqlParser.Parse("""
            SELECT * FROM Test FOR UPDATE SKIP LOCKED;
            """);

            return identifiers;
        }
        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Select_For_No_Key_Update()
        {
            var identifiers = _postgreSqlParser.Parse("""
            SELECT * FROM Test FOR NO KEY UPDATE;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Select_TableSample()
        {
            var identifiers = _postgreSqlParser.Parse("""
            SELECT* FROM Test TABLESAMPLE SYSTEM(10) REPEATABLE(42);
            """); 
            
            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Select_Values()
        {
            var identifiers = _postgreSqlParser.Parse("""
            SELECT * FROM (VALUES (1, 'A'), (2, 'B')) AS t(Id, Name);
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Select_LateralAndTableFunctions()
        {
            var identifiers = _postgreSqlParser.Parse("""
            SELECT * FROM Test t CROSS JOIN LATERAL (SELECT t.Id) AS x;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Select_Table()
        {
            var identifiers = _postgreSqlParser.Parse("""
            TABLE Test;
            """);

            return identifiers;
        }
    }
}
