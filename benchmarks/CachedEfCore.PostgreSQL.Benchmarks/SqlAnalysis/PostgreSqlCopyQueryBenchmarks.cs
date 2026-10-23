using BenchmarkDotNet.Attributes;
using CachedEfCore.PostgreSQL.SqlAnalysis;
using Microsoft.VSDiagnostics;
using System;
using System.Collections.Generic;

namespace CachedEfCore.PostgreSQL.Benchmarks.SqlAnalysis
{
    [CPUUsageDiagnoser]
    [MemoryDiagnoser(true)]
    public class PostgreSqlCopyQueryBenchmarks
    {
        private PostgreSqlParser _postgreSqlParser = null!;

        [GlobalSetup]
        public void Setup()
        {
            _postgreSqlParser = new PostgreSqlParser();
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Copy_To_Without_State_Changing()
        {
            var identifiers = _postgreSqlParser.Parse("""
                COPY (SELECT id, name FROM Test WHERE id > 10)
                TO PROGRAM 'gzip > /tmp/test.csv.gz' WITH (FORMAT csv);
                """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Copy_From()
        {
            var identifiers = _postgreSqlParser.Parse("""
                COPY Test (id, name) FROM '/tmp/test.csv' WITH (FORMAT csv, HEADER true);
                """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Copy_To_Query()
        {
            var identifiers = _postgreSqlParser.Parse("""
                COPY (UPDATE Test SET name = 'Chicken' WHERE id = 1 RETURNING id, name)
                TO STDOUT WITH (FORMAT csv, HEADER);
                """);

            return identifiers;
        }
    }
}
