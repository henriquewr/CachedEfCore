using CachedEfCore.PostgreSQL.SqlAnalysis;
using CachedEfCore.Tests.Common.Fixtures;
using System.Linq;
using Xunit;

namespace CachedEfCore.PostgreSQL.Tests.SqlAnalisys.Parsing
{
    public class MultipleQueriesParsingTests : PostgreSqlParsingTestBase, IClassFixture<ServiceProviderFixture>
    {
        private readonly PostgreSqlParser _postgreSqlParser;
        public MultipleQueriesParsingTests(ServiceProviderFixture serviceProviderFixture) : base(serviceProviderFixture)
        {
            _postgreSqlParser = new PostgreSqlParser();
        }

        [Theory]
        [InlineData("""
        BEGIN;

        INSERT INTO OldUsers (Id, Name)
        VALUES (1, 'Test');

        UPDATE Users
        SET Name = 'Updated'
        WHERE Id = 1;

        DELETE FROM Orders
        WHERE UserId = 1;

        COMMIT;
        """, "OldUsers,Users,Orders")]
        [InlineData("""
        DO $$
        BEGIN
            INSERT INTO Products (Id, Name)
            VALUES (1, 'Notebook');

            UPDATE Inventory
            SET Quantity = Quantity - 1
            WHERE ProductId = 1;

        EXCEPTION
            WHEN OTHERS THEN
                RAISE;
        END;
        $$;
        """, "Products,Inventory")]
        [InlineData("""
        DO $$
        BEGIN
            DELETE FROM OrderItems
            WHERE OrderId = 10;

            DELETE FROM Orders
            WHERE Id = 10;

        EXCEPTION
            WHEN OTHERS THEN
                RAISE NOTICE 'Erro ao excluir o pedido: %', SQLERRM;
                RAISE;
        END;
        $$;
        """, "OrderItems,Orders")]
        [InlineData("""
        BEGIN;

        WITH ActiveUsers AS
        (
            SELECT Id
            FROM Users
            WHERE Active = 1
        )
        UPDATE Test
        SET LastLogin = CURRENT_TIMESTAMP
        WHERE Id IN
        (
            SELECT Id
            FROM ActiveUsers
        );

        COMMIT;
        """, "Test")]
        [InlineData("""
        BEGIN;

        DO $$
        DECLARE
            UserId INT := 1;
        BEGIN
        END;
        $$;

        SELECT *
        FROM Test
        WHERE Id = 1;

        SAVEPOINT BeforeUpdate;

        UPDATE Test
        SET Name = 'Test'
        WHERE Id = 1;

        ROLLBACK TO SAVEPOINT BeforeUpdate;

        COMMIT;
        """, "Test")]
        [InlineData("""
        DO $$
        BEGIN
            BEGIN
                CALL ProcessOrders(100);

                INSERT INTO Test (Message, CreatedAt)
                VALUES ('Processed batch', CURRENT_TIMESTAMP);

            EXCEPTION
                WHEN OTHERS THEN
                    RAISE;
            END;
        END;
        $$;
        """, "Test")]
        [InlineData("""
        DO $$
        BEGIN
            BEGIN
                CALL ProcessOrders(100);

                INSERT INTO Test (Message, CreatedAt)
                VALUES ('Processed batch', CURRENT_TIMESTAMP);

                MERGE INTO TestMerge AS target
                USING Source AS source
                ON target.Id = source.Id
                WHEN MATCHED THEN
                    UPDATE SET
                        Value = overlay(target.Value placing 'SQL ' from 7 for 4),
                        Value2 = overlay(target.Value2 placing 'test' from 1 for 0);

            EXCEPTION
                WHEN OTHERS THEN
                    RAISE;
            END;
        END;
        $$;
        """, "Test,TestMerge")]
        public void Parse_Multiple_Queries_PostgreSql(string sql, string tables)
        {
            var identifiers = _postgreSqlParser.Parse(sql).Select(x => x.ToString()).Order().ToArray();

            var tablesArr = tables.Split(',').Order().ToArray();

            Assert.Equal(tablesArr, identifiers);
        }
    }
}
