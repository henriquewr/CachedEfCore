using BenchmarkDotNet.Attributes;
using CachedEfCore.SqlServer.SqlAnalysis;
using Microsoft.VSDiagnostics;
using System;
using System.Collections.Generic;

namespace CachedEfCore.SqlServer.Benchmarks.SqlAnalysis
{
    [CPUUsageDiagnoser]
    [MemoryDiagnoser(true)]
    public class SqlServerTruncateQueryBenchmarks
    {
        private SqlServerParser _sqlServerParser = null!;

        [GlobalSetup]
        public void Setup()
        {
            _sqlServerParser = new SqlServerParser();
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> TruncateTable()
        {
            var identifiers = _sqlServerParser.Parse("TRUNCATE TABLE Test");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> TruncateTable_Database_Schema_Table()
        {
            var identifiers = _sqlServerParser.Parse("TRUNCATE TABLE CachedEfCoreDb.dbo.Test");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> TruncateTable_Schema_Table()
        {
            var identifiers = _sqlServerParser.Parse("TRUNCATE TABLE dbo.Test");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> TruncateTable_With()
        {
            var identifiers = _sqlServerParser.Parse("TRUNCATE TABLE CachedEfCoreDb.dbo.Test WITH (PARTITIONS (1, 2 TO 4, 5));");

            return identifiers;
        }
    }
}
