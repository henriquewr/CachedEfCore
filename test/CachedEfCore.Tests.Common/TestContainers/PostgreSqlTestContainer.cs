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
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
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
            if (!Volatile.Read(ref _isInitialized))
            {
                try
                {
                    await _semaphoreSlim.WaitAsync().ConfigureAwait(false);

                    if (!_isInitialized)
                    {
                        await _container.StartAsync();

                        await using var connection = new NpgsqlConnection(ConnectionStringForDatabase("postgres"));

                        await connection.OpenAsync();

                        await using var command = connection.CreateCommand();
                        command.CommandText = $"CREATE DATABASE \"{_databaseName}\"";

                        await command.ExecuteNonQueryAsync();
                    }

                    _isInitialized = true;
                }
                finally
                {
                    _semaphoreSlim.Release();
                }
            }

            ConnectionString = ConnectionStringForDatabase(_databaseName);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
