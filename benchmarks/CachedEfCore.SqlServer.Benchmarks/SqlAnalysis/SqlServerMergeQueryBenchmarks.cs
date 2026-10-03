using BenchmarkDotNet.Attributes;
using CachedEfCore.SqlServer.SqlAnalysis;
using Microsoft.VSDiagnostics;
using System;
using System.Collections.Generic;

namespace CachedEfCore.SqlServer.Benchmarks.SqlAnalysis
{
    [CPUUsageDiagnoser]
    [MemoryDiagnoser(true)]
    public class SqlServerMergeQueryBenchmarks
    {
        private SqlServerParser _sqlServerParser = null!;

        [GlobalSetup]
        public void Setup()
        {
            _sqlServerParser = new SqlServerParser();
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Merge()
        {
            var identifiers = _sqlServerParser.Parse("""
            MERGE [Test] USING (
            VALUES 
                (@p20, 0),
                (@p30, 1),
                (@p40, 2),
                (@p50, 3),
                (@p60, 4)
            ) AS i ([Value], _Position) ON 1=0
            WHEN NOT MATCHED THEN
            INSERT ([Value])
            VALUES (i.[Value])
            OUTPUT INSERTED.[Value], i._Position;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Top()
        {
            var identifiers = _sqlServerParser.Parse("""
            MERGE TOP (((10))) INTO Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status = 1;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Top_Percent()
        {
            var identifiers = _sqlServerParser.Parse("""
            MERGE TOP(((10))) PERCENT INTO Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status = 1;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Update_Multiple_Set()
        {
            var identifiers = _sqlServerParser.Parse("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status = 1, Value = 3, Other = 56
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Update_Set_String_Literal()
        {
            var identifiers = _sqlServerParser.Parse("""
                MERGE Test AS target
                USING Source AS source
                ON target.Id = source.Id
                WHEN MATCHED THEN
                    UPDATE SET Status = '1'
                """);

            return identifiers;
        }


        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Update_Set_Func()
        {
            var identifiers = _sqlServerParser.Parse("""
                MERGE Test AS target
                USING Source AS source
                ON target.Id = source.Id
                WHEN MATCHED THEN
                    UPDATE SET Status = ABS(-1)
                """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Update_Set_Subquery()
        {
            var identifiers = _sqlServerParser.Parse("""
                MERGE Test AS target
                USING Source AS source
                ON target.Id = source.Id
                WHEN MATCHED THEN
                    UPDATE SET Status += 1
                """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Update_Assignment_Operators()
        {
            var identifiers = _sqlServerParser.Parse("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status += 1
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Update_Set_Write()
        {
            var identifiers = _sqlServerParser.Parse("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Value.WRITE('SQL ', 6, 0), Value2.WRITE('test', NULL, 0)
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Update_Value_Keyword()
        {
            // VALUE is a keyword
            var identifiers = _sqlServerParser.Parse("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Value = Value
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Set_Case_Expression()
        {
            var identifiers = _sqlServerParser.Parse("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Value = 
                    CASE
                        WHEN A = 1 THEN 'test'
                        WHEN A = 2 THEN 'test2'
                        ELSE Value
                    END
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Multiple_When_Matched()
        {
            var identifiers = _sqlServerParser.Parse("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id

            WHEN MATCHED
                AND source.IsDeleted = 1
            THEN
                DELETE

            WHEN MATCHED
                AND source.IsDeleted = 0
            THEN
                UPDATE SET
                    target.Name = source.Name,
                    target.Status = source.Status

            WHEN NOT MATCHED
                AND source.IsActive = 1
            THEN
                INSERT (Id, Name, Status)
                VALUES (source.Id, source.Name, source.Status)

            WHEN NOT MATCHED BY TARGET
                AND source.IsActive = 0
            THEN
                INSERT (Id, Name, Status)
                VALUES (source.Id, source.Name, 0)

            WHEN NOT MATCHED BY SOURCE
                AND target.IsArchived = 1
            THEN
                DELETE

            WHEN NOT MATCHED BY SOURCE
                AND target.IsArchived = 0
            THEN
                UPDATE SET
                    target.Status = -1;
            """);
            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Merge_Output()
        {
            var identifiers = _sqlServerParser.Parse("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT deleted.Value + 2 OldValue;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Merge_Output_Into()
        {
            var identifiers = _sqlServerParser.Parse("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT deleted.Value + 2 OldValue
            INTO Test2;
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Merge_Option()
        {
            var identifiers = _sqlServerParser.Parse("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED AND source.IsDeleted = 1 THEN
                DELETE
            WHEN MATCHED THEN
                UPDATE SET target.Value = source.Value
            WHEN NOT MATCHED THEN
                INSERT (Id, Value)
                VALUES (source.Id, source.Value)
            OPTION (HASH JOIN);
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Merge_Output_Into_Option()
        {
            var identifiers = _sqlServerParser.Parse("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN NOT MATCHED THEN
                INSERT (Id, Value)
                VALUES (source.Id, source.Value)
            OUTPUT $action, inserted.Id
            INTO Audit
            OPTION (HASH JOIN);
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Merge_On_Case()
        {
            var identifiers = _sqlServerParser.Parse("""
            MERGE Test AS target
            USING Source AS source
            ON CASE
                   WHEN source.Id > 0
                       THEN CASE WHEN target.Id = source.Id THEN 1 ELSE 0 END
                   ELSE 0
               END = 1
            WHEN NOT MATCHED THEN
                INSERT (Id, Value)
                VALUES (source.Id, source.Value)
            OUTPUT $action, inserted.Id
            INTO Audit
            OPTION (HASH JOIN);
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Merge_Not_Matched_By_Target_And_Case()
        {
            var identifiers = _sqlServerParser.Parse("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN NOT MATCHED BY TARGET AND
               CASE
                   WHEN source.Value IS NOT NULL THEN 1
                   ELSE 0
               END = 1
            THEN
                INSERT (Id, Value)
                VALUES (source.Id, source.Value)
            OUTPUT $action, inserted.Id
            INTO Audit
            OPTION (HASH JOIN);
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Merge_Not_Matched_By_Source_And_Case()
        {
            var identifiers = _sqlServerParser.Parse("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN NOT MATCHED BY SOURCE AND
                CASE
                    WHEN source.Value IS NOT NULL THEN 1
                    ELSE 0
                END = 1
            THEN
                UPDATE SET target.Value = source.Value
            OUTPUT $action, inserted.Id
            INTO Audit
            OPTION (HASH JOIN);
            """);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Merge_Matched_And_Case()
        {
            var identifiers = _sqlServerParser.Parse("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED AND
                CASE
                    WHEN source.Value IS NOT NULL THEN 1
                    ELSE 0
                END = 1
            THEN
                UPDATE SET target.Value = source.Value
            OUTPUT $action, inserted.Id
            INTO Audit
            OPTION (HASH JOIN);
            """);

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
            MERGE Cte AS t
            USING Test3 AS s
            ON t.Id = s.Id
            WHEN MATCHED THEN
                UPDATE SET t.Value = t.Value + 1
            OUTPUT inserted.Value AS NewValue,
                   deleted.Value AS OldValue,
                   inserted.Value2 AS Value2
            INTO Test2;
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
                FROM UnusedTable
            )
            MERGE Test AS t
            USING Test3 AS s
            ON t.Id = s.Id
            WHEN MATCHED THEN
                UPDATE SET t.Value = t.Value + 1
            OUTPUT inserted.Value AS NewValue,
                   deleted.Value AS OldValue,
                   inserted.Value2 AS Value2
            INTO Test2;
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
            MERGE Target AS t
            USING Test3 AS s
            ON t.Id = s.Id
            WHEN MATCHED THEN
                UPDATE SET t.Value = t.Value + 1
            OUTPUT inserted.Id
            INTO Test2;
            """);

            return identifiers;
        }
    }
}
