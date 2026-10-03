using BenchmarkDotNet.Attributes;
using CachedEfCore.SqlServer.SqlAnalysis;
using Microsoft.VSDiagnostics;
using System;
using System.Collections.Generic;

namespace CachedEfCore.SqlServer.Benchmarks.SqlAnalysis
{
    [CPUUsageDiagnoser]
    [MemoryDiagnoser(true)]
    public class SqlServerDeleteQueryBenchmarks
    {
        private SqlServerParser _sqlServerParser = null!;

        [GlobalSetup]
        public void Setup()
        {
            _sqlServerParser = new SqlServerParser();
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Top_Without_Percent()
        {
            var identifiers = _sqlServerParser.Parse("DELETE TOP (SELECT 1) FROM dbo.Test;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Top_Percent()
        {
            var identifiers = _sqlServerParser.Parse("DELETE TOP (SELECT 1) PERCENT FROM Test;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Delete_Alias()
        {
            var identifiers = _sqlServerParser.Parse("DELETE TOP (1) u FROM Test u;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Option()
        {
            var identifiers = _sqlServerParser.Parse("DELETE u FROM Test AS u OPTION (RECOMPILE);");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Output()
        {
            var identifiers = _sqlServerParser.Parse("DELETE u OUTPUT deleted.Value + 2 OldValue FROM Test u;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Output_Into()
        {
            var identifiers = _sqlServerParser.Parse("DELETE u OUTPUT deleted.Value AS OldValue, deleted.Value2 AS Value2 INTO Test2 FROM Test u;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Cte()
        {
            var identifiers = _sqlServerParser.Parse("""
            WITH Cte AS
            (
                SELECT *
                FROM Test
            )
            DELETE u
            OUTPUT deleted.Value AS OldValue,
                    deleted.Value2 AS Value2
            INTO Test2
            FROM Cte u;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Unused_Cte()
        {
            var identifiers = _sqlServerParser.Parse("""
            WITH Cte AS
            (
                SELECT *
                FROM Test
            )
            DELETE u
            OUTPUT deleted.Value AS OldValue,
                    deleted.Value2 AS Value2
            INTO Test2
            FROM Test3 u;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Multiple_Ctes()
        {
            var identifiers = _sqlServerParser.Parse("""
            WITH Filtered AS
            (
                SELECT *
                FROM Test
                WHERE IsActive = 0
            ),
            Target AS
            (
                SELECT *
                FROM Filtered
                WHERE Value2 IS NOT NULL
            )
            DELETE u
            OUTPUT deleted.Id
            INTO Test2
            FROM Target u;
            """);

            return identifiers;
        }
    }
}
