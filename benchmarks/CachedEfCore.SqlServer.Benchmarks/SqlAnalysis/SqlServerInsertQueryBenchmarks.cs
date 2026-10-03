using BenchmarkDotNet.Attributes;
using CachedEfCore.SqlServer.SqlAnalysis;
using Microsoft.VSDiagnostics;
using System;
using System.Collections.Generic;

namespace CachedEfCore.SqlServer.Benchmarks.SqlAnalysis
{
    [CPUUsageDiagnoser]
    [MemoryDiagnoser(true)]
    public class SqlServerInsertQueryBenchmarks
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
            var identifiers = _sqlServerParser.Parse("INSERT TOP (SELECT 1) INTO Test VALUES (1), (')');");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Top_Percent()
        {
            var identifiers = _sqlServerParser.Parse("INSERT TOP (SELECT 1) PERCENT INTO Test VALUES (1);");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Insert()
        {
            var identifiers = _sqlServerParser.Parse("INSERT INTO Test (Value), (Value2) VALUES (1, ')'), (2, ')');");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Default_Values()
        {
            var identifiers = _sqlServerParser.Parse("INSERT Test DEFAULT VALUES;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Output()
        {
            var identifiers = _sqlServerParser.Parse("INSERT INTO Test OUTPUT inserted.Value SELECT u.Value FROM OtherTable AS u");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Output_Into()
        {
            var identifiers = _sqlServerParser.Parse("INSERT INTO Test OUTPUT inserted.Value INTO Test2 SELECT Value FROM u.OtherTable AS u");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> With()
        {
            var identifiers = _sqlServerParser.Parse("INSERT Test WITH (ROWLOCK, HOLDLOCK) OUTPUT inserted.Value + 2 INTO Test2 EXEC dbo.Something;");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Dml_Table_Source_Merge()
        {
            var sql = """
            INSERT INTO Test (Value)
            SELECT Value
            FROM
            (
                MERGE Test2 t
                USING Source s ON t.Id = s.Id
                WHEN MATCHED THEN
                    UPDATE SET Value = s.Value
                OUTPUT inserted.Value
            ) AS x(Value);
            """;

            var identifiers = _sqlServerParser.Parse(sql);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Dml_Table_Source_Delete()
        {
            var sql = """
            INSERT INTO Test (Value)
            SELECT Value
            FROM
            (
                DELETE FROM Test2
                OUTPUT deleted.Value
            ) AS x(Value);
            """;

            var identifiers = _sqlServerParser.Parse(sql);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Dml_Table_Source_Update()
        {
            var sql = """
            INSERT INTO Test (Value)
            SELECT Value
            FROM
            (
                UPDATE Test2
                SET Value = 'Updated'
                OUTPUT inserted.Value
            ) AS x(Value);
            """;

            var identifiers = _sqlServerParser.Parse(sql);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Dml_Table_Source_Insert()
        {
            var sql = """
            INSERT INTO Test (Value)
            SELECT Value
            FROM
            (
                INSERT INTO Test2 (Value)
                OUTPUT inserted.Value
                VALUES ('Inserted')
            ) AS x(Value);
            """;

            var identifiers = _sqlServerParser.Parse(sql);

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Dml_Table_Source()
        {
            var identifiers = _sqlServerParser.Parse("""
            INSERT INTO Test (Value)
            SELECT Value
            FROM
            (
                INSERT INTO Test2 (Value)
                OUTPUT inserted.Value
                VALUES ('Inserted')
            ) table1 
            WHERE 1 = 1 
            OPTION (RECOMPILE);
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
                FROM Test2
            )
            INSERT INTO Test (Value, Value2)
            SELECT Value, Value2
            FROM Cte;
            """);

            return identifiers;
        }
    }
}
