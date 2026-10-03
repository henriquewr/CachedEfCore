using BenchmarkDotNet.Attributes;
using CachedEfCore.SqlServer.SqlAnalysis;
using Microsoft.VSDiagnostics;
using System;
using System.Collections.Generic;

namespace CachedEfCore.SqlServer.Benchmarks.SqlAnalysis
{
    [CPUUsageDiagnoser]
    [MemoryDiagnoser(true)]
    public class SqlServerUpdateQueryBenchmarks
    {
        private SqlServerParser _sqlServerParser = null!;

        [GlobalSetup]
        public void Setup()
        {
            _sqlServerParser = new SqlServerParser();
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Top()
        {
            var identifiers = _sqlServerParser.Parse("UPDATE TOP (SELECT 1) Test SET Status = 1;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Top_Percent()
        {
            var identifiers = _sqlServerParser.Parse("UPDATE TOP (SELECT 1) PERCENT Test SET Status = 1;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> With_Hint()
        {
            var identifiers = _sqlServerParser.Parse("UPDATE Test WITH (ROWLOCK, UPDLOCK) SET Status = 1;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Multiple_Set()
        {
            var identifiers = _sqlServerParser.Parse("UPDATE Test SET Status = 1, Value = 3, Other = 56;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Set_String_Literal()
        {
            var identifiers = _sqlServerParser.Parse("UPDATE u SET Status = '1' FROM Test u");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Set_Func()
        {
            var identifiers = _sqlServerParser.Parse("UPDATE u SET Status = ABS(-1) FROM Test u");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Set_Subquery()
        {
            var identifiers = _sqlServerParser.Parse("UPDATE u SET Status = (SELECT 1) FROM Test u");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Assignment_Operators()
        {
            var identifiers = _sqlServerParser.Parse("UPDATE u SET Status += 1 FROM Test u;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Set_Write()
        {
            var identifiers = _sqlServerParser.Parse("UPDATE u SET Value.WRITE('SQL ', 6, 0), Value2.WRITE('test', NULL, 0) FROM Test u WHERE Id = 1;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Update_Value_Keyword()
        {
            // VALUE is a keyword
            var identifiers = _sqlServerParser.Parse("UPDATE u SET Value = Value OUTPUT inserted.Value NewValue FROM Test u;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Output()
        {
            var identifiers = _sqlServerParser.Parse("UPDATE u SET u.Value = 1 OUTPUT deleted.Value AS OldValue, inserted.Value NewValue FROM Test u;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Output_Into()
        {
            var identifiers = _sqlServerParser.Parse("UPDATE u SET u.Value = 1 OUTPUT deleted.Value AS OldValue, inserted.Value NewValue INTO Test2 FROM Test u;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Option()
        {
            var identifiers = _sqlServerParser.Parse("UPDATE Test SET Value = Value OPTION (RECOMPILE);");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> From_Non_Used_Table()
        {
            var identifiers = _sqlServerParser.Parse("UPDATE Test SET u.Value = 1 OUTPUT 2 OldValue FROM UnusedTable u;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Set_Case_Expression()
        {
            var identifiers = _sqlServerParser.Parse("""
            UPDATE u 
                SET u.Value = 
                    CASE
                        WHEN A = 1 THEN 'test'
                        WHEN A = 2 THEN 'test2'
                        ELSE u.Value
                    END 
            FROM Test AS u
            WHERE u.Id = 1;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Join()
        {
            var identifiers = _sqlServerParser.Parse("UPDATE u SET Value = Value FROM Test u JOIN Test2 test2 ON u.Id = test2.Id WHERE Id = 1;");

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
            UPDATE u
            SET u.Value = u.Value + 1
            OUTPUT inserted.Value AS NewValue,
                   deleted.Value AS OldValue,
                   inserted.Value2 AS Value2
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
            UPDATE u
            SET Value = Value + 1
            OUTPUT inserted.Value AS NewValue,
                   deleted.Value AS OldValue,
                   inserted.Value2 AS Value2
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
            UPDATE Target
            SET Value = Value + 1
            OUTPUT inserted.Id
            INTO Test2;
            """);

            return identifiers;
        }
    }
}
