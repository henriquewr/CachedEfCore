using Microsoft.Data.SqlClient;
using System.Diagnostics;
using Testcontainers.MsSql;
using Xunit;

namespace CachedEfCore.Tests.Common.TestContainers
{
    [DebuggerDisplay("{_databaseName}")]
    public class SqlServerTestContainer : IAsyncLifetime
    {
        private static readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
        private static uint _dbs = 0;
        private readonly string _databaseName = $"sql_server_db_{Interlocked.Increment(ref _dbs)}";
        public string ConnectionString { get; private set; } = null!;

        private static string ConnectionStringForDatabase(string database)
        {
            var builder = new SqlConnectionStringBuilder(_container.GetConnectionString())
            {
                InitialCatalog = database,
                PacketSize = 32768,
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

                        await using var connection = new SqlConnection(ConnectionStringForDatabase("master"));

                        await connection.OpenAsync();

                        await using var command = connection.CreateCommand();
                        command.CommandText = $"CREATE DATABASE [{_databaseName}]";

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
