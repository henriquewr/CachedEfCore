using CachedEfCore.DependencyInjection;
using CachedEfCore.SqlAnalysis.SqlServer;
using CachedEfCore.Tests.Common.Fixtures;
using System;
using System.Linq;
using Xunit;

namespace CachedEfCore.SqlAnalisys.Tests.Parsing.MergeParsing
{
    public class MergeSqlServerParsingTests : SqlServerParsingTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;
        private readonly SqlServerParser _sqlServerParser;

        public MergeSqlServerParsingTests(ServiceProviderFixture serviceProviderFixture)
        {
            _serviceProviderFixture = serviceProviderFixture;
            _sqlServerParser = new SqlServerParser();
        }

        protected virtual IServiceProvider CreateProvider()
           => _serviceProviderFixture.CreateProvider(services =>
           {
               services.AddCachedEfCore<SqlServerQueryEntityExtractor>();
           });

        [Theory]
        [InlineData("""
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
            """)]
        public void Merge(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
            MERGE TOP (SELECT 1) Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status = 1;
            """)]
        [InlineData("""
            MERGE TOP(10) Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status = 1;
            """)]
        [InlineData("""
            MERGE TOP(((10))) INTO Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status = 1;
            """)]
        [InlineData("""
            MERGE TOP (((10))) INTO Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status = 1;
            """)]
        public void Top(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
            MERGE TOP (SELECT 1) PERCENT Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status = 1;
            """)]
        [InlineData("""
            MERGE TOP(10) PERCENT Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status = 1;
            """)]
        [InlineData("""
            MERGE TOP(((10))) PERCENT INTO Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status = 1;
            """)]
        [InlineData("""
            MERGE TOP (((10))) PERCENT INTO Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status = 1;
            """)]
        public void Top_Percent(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status = 1, Value = 3, Other = 56
            """)]
        public void Update_Multiple_Set(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Update_Set_String_Literal()
        {
            var identifiers = _sqlServerParser.Parse("""
                MERGE Test AS target
                USING Source AS source
                ON target.Id = source.Id
                WHEN MATCHED THEN
                    UPDATE SET Status = '1'
                """);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }


        [Fact]
        public void Update_Set_Func()
        {
            var identifiers = _sqlServerParser.Parse("""
                MERGE Test AS target
                USING Source AS source
                ON target.Id = source.Id
                WHEN MATCHED THEN
                    UPDATE SET Status = ABS(-1)
                """);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Fact]
        public void Update_Set_Subquery()
        {
            var identifiers = _sqlServerParser.Parse("""
                MERGE Test AS target
                USING Source AS source
                ON target.Id = source.Id
                WHEN MATCHED THEN
                    UPDATE SET Status += 1
                """);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status += 1
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status -= 1
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status *= 1
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status /= 1
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status %= 1
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status &= 1
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status ^= 1
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Status |= 1
            """)]
        public void Update_Assignment_Operators(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Value.WRITE('SQL ', 6, 0)
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Value.WRITE('SQL ', 6, 0), Value2.WRITE('test', NULL, 0)
            """)]
        public void Update_Set_Write(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Value = Value
            """)]
        public void Update_Value_Keyword(string sql)
        {
            // VALUE is a keyword
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
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
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Value = 
                    CASE
                        WHEN A = 1 THEN 'test'
                    END
            """)]
        public void Set_Case_Expression(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
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
            """)]
        public void Multiple_When_Matched(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);
            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT deleted.Value AS OldValue;
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT 1 OldValue;
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT 1 + 2 OldValue;
            """)]
        [InlineData("""
            MERGE TOP (1) Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT 1 + 2 OldValue;
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT deleted.Value + 2 OldValue;
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT 'a' OldValue;
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT ABS(-1) OldValue;
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT ABS(-1) + 2 OldValue;
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT ABS(-1) + 2;
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT deleted.Value OldValue;
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT $action, inserted.Id;
            """)]
        public void Merge_Output(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT deleted.Value AS OldValue
            INTO Test2;
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT 1 OldValue
            INTO Test2;
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT 1 + 2 OldValue
            INTO Test2;
            """)]
        [InlineData("""
            MERGE TOP (1) Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT 1 + 2 OldValue
            INTO Test2;
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT deleted.Value + 2 OldValue
            INTO Test2;
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT 'a' OldValue
            INTO Test2;
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT ABS(-1) OldValue
            INTO Test2;
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT ABS(-1) + 2 OldValue
            INTO Test2;
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT ABS(-1) + 2
            INTO Test2;
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT deleted.Value OldValue
            INTO Test2;
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT deleted.Value OldValue
            INTO dbo.Test2;
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN DELETE
            OUTPUT $action, inserted.Id
            INTO Test2;
            """)]
        public void Merge_Output_Into(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal(["Test", "Test2"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET target.Value = source.Value
            OPTION (RECOMPILE);
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                DELETE
            OPTION (RECOMPILE);
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN NOT MATCHED THEN
                INSERT (Id, Value)
                VALUES (source.Id, source.Value)
            OPTION (RECOMPILE);
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET target.Value = source.Value
            WHEN NOT MATCHED THEN
                INSERT (Id, Value)
                VALUES (source.Id, source.Value)
            OPTION (RECOMPILE);
            """)]
        [InlineData("""
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
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN NOT MATCHED BY SOURCE THEN
                DELETE
            OPTION (MAXDOP 1);
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN NOT MATCHED BY SOURCE AND target.IsActive = 1 THEN
                DELETE
            OPTION (FAST 10);
            """)]
        public void Merge_Option(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal("Test", Assert.Single(identifiers).ToString());
        }

        [Theory]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET target.Value = source.Value
            OUTPUT inserted.Id
            INTO Audit
            OPTION (RECOMPILE);
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                DELETE
            OUTPUT deleted.Id
            INTO Audit
            OPTION (MAXDOP 1);
            """)]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN NOT MATCHED THEN
                INSERT (Id, Value)
                VALUES (source.Id, source.Value)
            OUTPUT $action, inserted.Id
            INTO Audit
            OPTION (HASH JOIN);
            """)]
        public void Merge_Output_Into_Option(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal(["Audit", "Test"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
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
            """)]
        public void Merge_On_Case(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal(["Audit", "Test"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
            MERGE Test AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN NOT MATCHED AND
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
            """)]

        [InlineData("""
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
            """)]
        public void Merge_Not_Matched_By_Target_And_Case(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal(["Audit", "Test"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
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
            """)]
        public void Merge_Not_Matched_By_Source_And_Case(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal(["Audit", "Test"], identifiers.Select(x => x.ToString()).Order());
        }

        [Theory]
        [InlineData("""
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
            """)]
        public void Merge_Matched_And_Case(string sql)
        {
            var identifiers = _sqlServerParser.Parse(sql);

            Assert.Equal(["Audit", "Test"], identifiers.Select(x => x.ToString()).Order());
        }
    }
}
