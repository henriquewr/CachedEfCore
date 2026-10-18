using BenchmarkDotNet.Attributes;
using CachedEfCore.PostgreSQL.SqlAnalysis;
using Microsoft.VSDiagnostics;
using System;
using System.Collections.Generic;

namespace CachedEfCore.PostgreSQL.Benchmarks.SqlAnalysis
{
    [CPUUsageDiagnoser]
    [MemoryDiagnoser(true)]
    public class PostgreSqlDoQueryBenchmarks
    {
        private PostgreSqlParser _postgreSqlParser = null!;

        [GlobalSetup]
        public void Setup()
        {
            _postgreSqlParser = new PostgreSqlParser();
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Do_Language()
        {
            var identifiers = _postgreSqlParser.Parse("""
            DO LANGUAGE plpgsql $$
            BEGIN
                UPDATE "Test"
                SET "Value" = 1;
            END;
            $$;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Dollar_Quoted_String()
        {
            var identifiers = _postgreSqlParser.Parse("""
            DO $$
            BEGIN
                UPDATE "Test"
                SET "Value" = $inner$contains $value$another string$value$ inside$inner$;

                UPDATE "Test2"
                SET "Value" = $inner$contains $value$another string$value$ inside$inner$;
            END;
            $$;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Single_Quoted_String()
        {
            var identifiers = _postgreSqlParser.Parse("""
            DO 'BEGIN
                UPDATE "Test"
                SET "Value" = ''contains ''''another string'''' inside'';
            
                UPDATE "Test2"
                SET "Value" = ''contains ''''another string'''' inside'';
            END;';
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Unicode_Escaped_With_Single_Quoted_String()
        {
            var identifiers = _postgreSqlParser.Parse("""
            DO U&'BEGIN
                UPDATE "Test"
                SET "Value" = ''contains ''''another string'''' inside'';
            
                UPDATE "Test2"
                SET "Value" = ''contains ''''another string'''' inside'';
            END;' UESCAPE '!';
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Unicode_Escaped_Single_Quoted_String()
        {
            var identifiers = _postgreSqlParser.Parse("""
            DO U&'BEGIN
                UPDATE "Test"
                SET "Value" = U&''contains ''''another string'''' inside'' UESCAPE ''!'';
            
                UPDATE "Test2"
                SET "Value" = U&''contains ''''another string'''' inside'';
            END;' UESCAPE '!';
            """);

            return identifiers;
        }
    }
}
