using BenchmarkDotNet.Attributes;
using CachedEfCore.PostgreSQL.SqlAnalysis;
using Microsoft.VSDiagnostics;
using System;
using System.Collections.Generic;

namespace CachedEfCore.PostgreSQL.Benchmarks.SqlAnalysis
{
    [CPUUsageDiagnoser]
    [MemoryDiagnoser(true)]
    public class PostgreSqlExecuteQueryBenchmarks
    {
        private PostgreSqlParser _postgreSqlParser = null!;

        [GlobalSetup]
        public void Setup()
        {
            _postgreSqlParser = new PostgreSqlParser();
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Do_Language_Execute()
        {
            var identifiers = _postgreSqlParser.Parse("""
            DO LANGUAGE plpgsql $$
            BEGIN
                EXECUTE $execute$
                    UPDATE "Test"
                    SET "Value" = 1;
                $execute$;
            END;
            $$;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Dollar_Quoted_String_Execute()
        {
            var identifiers = _postgreSqlParser.Parse("""
            DO $$
            BEGIN
                EXECUTE $execute$
                    UPDATE "Test"
                    SET "Value" = $inner$contains $value$another string$value$ inside$inner$;
            
                    UPDATE "Test2"
                    SET "Value" = $inner$contains $value$another string$value$ inside$inner$;
                $execute$;
            END;
            $$;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Single_Quoted_String_Execute()
        {
            var identifiers = _postgreSqlParser.Parse("""
            DO '
            BEGIN
                EXECUTE ''
                    UPDATE "Test"
                    SET "Value" = ''''nested string''''
                '';
            
                EXECUTE ''
                    UPDATE "Test2"
                    SET "Value" = ''''nested string''''
                '';
            END;
            ';
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Unicode_Escaped_With_Single_Quoted_String_Execute()
        {
            var identifiers = _postgreSqlParser.Parse("""
            DO U&'BEGIN
                EXECUTE ''UPDATE "Test" SET "Value" = ''''contains ''''''''''another string'''''''' inside'''''';
                EXECUTE ''UPDATE "Test2" SET "Value" = ''''contains ''''''''''another string'''''''' inside'''''';
            END;' UESCAPE '!';
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Unicode_Escaped_Single_Quoted_String_Execute()
        {
            var identifiers = _postgreSqlParser.Parse("""
            DO U&'BEGIN
                EXECUTE ''UPDATE "Test" SET "Value" = U&''''It''''''''s a ''''''''nested'''''''' string'''' UESCAPE ''''!!'''';'';
                EXECUTE ''UPDATE "Test2" SET "Value" = U&''''It''''''''s a ''''''''nested'''''''' string'''';'';
            END;' UESCAPE '!';
            """);

            return identifiers;
        }
    }
}
