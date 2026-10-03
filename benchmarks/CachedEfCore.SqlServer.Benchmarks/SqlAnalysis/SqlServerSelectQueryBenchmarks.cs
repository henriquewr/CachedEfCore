using BenchmarkDotNet.Attributes;
using CachedEfCore.SqlServer.SqlAnalysis;
using Microsoft.VSDiagnostics;
using System;
using System.Collections.Generic;

namespace CachedEfCore.SqlServer.Benchmarks.SqlAnalysis
{
    [CPUUsageDiagnoser]
    [MemoryDiagnoser(true)]
    public class SqlServerSelectQueryBenchmarks
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
            var identifiers = _sqlServerParser.Parse("SELECT TOP (1) id, value FROM Test WHERE id = 1 AND value = 1;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Top_With_Percent()
        {
            var identifiers = _sqlServerParser.Parse("SELECT TOP (12) PERCENT id, value FROM Test WHERE id = 1 AND value = 1;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Regular_Select()
        {
            var identifiers = _sqlServerParser.Parse("SELECT id, value FROM Test WHERE id = 1 AND value = 1;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Select_Distinct()
        {
            var identifiers = _sqlServerParser.Parse("SELECT DISTINCT Country FROM Test;");

            return identifiers;
        }
    }
}
