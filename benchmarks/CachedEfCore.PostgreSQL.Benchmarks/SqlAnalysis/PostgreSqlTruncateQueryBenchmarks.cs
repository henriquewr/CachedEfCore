using BenchmarkDotNet.Attributes;
using CachedEfCore.PostgreSQL.SqlAnalysis;
using Microsoft.VSDiagnostics;
using System;
using System.Collections.Generic;

namespace CachedEfCore.PostgreSQL.Benchmarks.SqlAnalysis
{
    [CPUUsageDiagnoser]
    [MemoryDiagnoser(true)]
    public class PostgreSqlTruncateQueryBenchmarks
    {
        private PostgreSqlParser _postgreSqlParser = null!;

        [GlobalSetup]
        public void Setup()
        {
            _postgreSqlParser = new PostgreSqlParser();
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Truncate()
        {
            var identifiers = _postgreSqlParser.Parse("""
            TRUNCATE Test
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Truncate_Table()
        {
            var identifiers = _postgreSqlParser.Parse("""
            TRUNCATE TABLE Test
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Truncate_Star()
        {
            var identifiers = _postgreSqlParser.Parse("""
            TRUNCATE Test *
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Truncate_Restart_Identity()
        {
            var identifiers = _postgreSqlParser.Parse("""
            TRUNCATE Test RESTART IDENTITY
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Truncate_Continue_Identity()
        {
            var identifiers = _postgreSqlParser.Parse("""
            TRUNCATE Test CONTINUE IDENTITY
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Truncate_Cascade()
        {
            var identifiers = _postgreSqlParser.Parse("""
            TRUNCATE Test CASCADE
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Truncate_Restrict()
        {
            var identifiers = _postgreSqlParser.Parse("""
            TRUNCATE Test RESTRICT
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Truncate_Multiple_Tables()
        {
            var identifiers = _postgreSqlParser.Parse("""
            TRUNCATE TABLE ONLY Test, Test2 *, Test3 CASCADE", "Test,Test2,Test3
            """);

            return identifiers;
        }
    }
}
