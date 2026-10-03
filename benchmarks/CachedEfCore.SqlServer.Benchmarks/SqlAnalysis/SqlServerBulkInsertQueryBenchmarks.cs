using BenchmarkDotNet.Attributes;
using CachedEfCore.SqlServer.SqlAnalysis;
using Microsoft.VSDiagnostics;
using System;
using System.Collections.Generic;

namespace CachedEfCore.SqlServer.Benchmarks.SqlAnalysis
{
    [CPUUsageDiagnoser]
    [MemoryDiagnoser(true)]
    public class SqlServerBulkInsertQueryBenchmarks
    {
        private SqlServerParser _sqlServerParser = null!;

        [GlobalSetup]
        public void Setup()
        {
            _sqlServerParser = new SqlServerParser();
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Bulk_Insert_Should_Get_Table_Name()
        {
            var identifiers = _sqlServerParser.Parse("BULK INSERT CachedEfCoreDb.dbo.Test FROM 'file.csv'");

            return identifiers;
        }

        [Benchmark]
        public HashSet<ReadOnlyMemory<char>> Bulk_Insert_With_Clause()
        {
            var identifiers = _sqlServerParser.Parse("""
            BULK INSERT Test 
            FROM 'file.csv' 
            WITH ( 
                DATAFILETYPE = 'char', 
                KEEPNULLS, 
                TABLOCK, 
                MAXERRORS = 10, 
                FIRSTROW = 2
            )
            """);

            return identifiers;
        }
    }
}
