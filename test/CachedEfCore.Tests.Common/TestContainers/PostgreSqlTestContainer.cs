using Npgsql;
using System.Diagnostics;
using Testcontainers.PostgreSql;
using Xunit;

namespace CachedEfCore.Tests.Common.TestContainers
{
    [DebuggerDisplay("{_databaseName}")]
    public class PostgreSqlTestContainer : IAsyncLifetime
    {
        private static readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

        private static uint _dbs = 0;

        private readonly string _databaseName = $"postgre_sql_db_{Interlocked.Increment(ref _dbs)}";
        public string ConnectionString { get; private set; } = null!;

        private static string ConnectionStringForDatabase(string database)
        {
            var builder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
            {
                IncludeErrorDetail = true,
                Database = database,
            };

            return builder.ConnectionString;
        }

        public async ValueTask InitializeAsync()
        {
            await _container.StartAsync();

            ConnectionString = ConnectionStringForDatabase(_databaseName);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
