using CachedEfCore.SqlAnalysis.SqlServer;
using CachedEfCore.Tests.Common.Fixtures;
using System.Linq;
using Xunit;

namespace CachedEfCore.SqlAnalisys.Tests.Parsing
{
    public class MultipleQueriesParsingTests : SqlServerParsingTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly ServiceProviderFixture _serviceProviderFixture;
        private readonly SqlServerParser _sqlServerParser;
        public MultipleQueriesParsingTests(ServiceProviderFixture serviceProviderFixture)
        {
            _serviceProviderFixture = serviceProviderFixture;
            _sqlServerParser = new SqlServerParser();
        }

        [Theory]
        [InlineData("""
        BEGIN TRANSACTION;

        INSERT INTO OldUsers(Id, Name)
        VALUES (1, 'Test');

        UPDATE Users
        SET Name = 'Updated'
        WHERE Id = 1;

        DELETE FROM Orders
        WHERE UserId = 1;

        COMMIT TRANSACTION;
        """, "OldUsers,Users,Orders")]
        [InlineData("""
        BEGIN TRY

            BEGIN TRANSACTION;

            INSERT INTO Products(Id, Name)
            VALUES (1, 'Notebook');

            UPDATE Inventory
            SET Quantity = Quantity - 1
            WHERE ProductId = 1;

            COMMIT TRANSACTION;

        END TRY
        BEGIN CATCH

            IF @@TRANCOUNT > 0
                ROLLBACK TRANSACTION;

            THROW;

        END CATCH
        """, "Products,Inventory")]
        [InlineData("""
        BEGIN TRY

            BEGIN TRANSACTION;

            DELETE FROM OrderItems
            WHERE OrderId = 10;

            DELETE FROM Orders
            WHERE Id = 10;

            COMMIT TRANSACTION;

        END TRY
        BEGIN CATCH

            IF @@TRANCOUNT > 0
                ROLLBACK TRANSACTION;

        END CATCH
        """, "OrderItems,Orders")]
        [InlineData("""
        BEGIN TRANSACTION;

        WITH ActiveUsers AS
        (
            SELECT Id
            FROM Users
            WHERE Active = 1
        )
        UPDATE Test
        SET LastLogin = GETDATE()
        WHERE Id IN
        (
            SELECT Id
            FROM ActiveUsers
        );

        COMMIT TRANSACTION;
        """, "Test")]
        [InlineData("""
        BEGIN TRANSACTION;

        DECLARE @UserId INT = 1;

        SELECT *
        FROM Test
        WHERE Id = @UserId;

        SAVE TRANSACTION BeforeUpdate;

        UPDATE Test
        SET Name = 'Test'
        WHERE Id = @UserId;

        ROLLBACK TRANSACTION BeforeUpdate;

        COMMIT TRANSACTION;
        """, "Test")]
        [InlineData("""
        BEGIN TRY

            BEGIN TRANSACTION;

            EXEC dbo.ProcessOrders @BatchId = 100;

            INSERT INTO Test(Message, CreatedAt)
            VALUES ('Processed batch', GETDATE());

            COMMIT TRANSACTION;

        END TRY
        BEGIN CATCH

            IF XACT_STATE() <> 0
                ROLLBACK TRANSACTION;

            THROW;

        END CATCH
        """, "Test")]
        [InlineData("""
        BEGIN TRY

            BEGIN TRANSACTION;

            EXEC dbo.ProcessOrders @BatchId = 100;

            INSERT INTO Test(Message, CreatedAt)
            VALUES ('Processed batch', GETDATE());

            MERGE TestMerge AS target
            USING Source AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Value.WRITE('SQL ', 6, 0), Value2.WRITE('test', NULL, 0)

            COMMIT TRANSACTION;

        END TRY
        BEGIN CATCH

            IF XACT_STATE() <> 0
                ROLLBACK TRANSACTION;

            THROW;

        END CATCH
        """, "Test,TestMerge")]
        public void Parse_Multiple_Queries_SqlServer(string sql, string tables)
        {
            var identifiers = _sqlServerParser.Parse(sql).Select(x => x.ToString()).Order().ToArray();

            var tablesArr = tables.Split(',').Order().ToArray();

            Assert.Equal(tablesArr, identifiers);
        }
    }
}
