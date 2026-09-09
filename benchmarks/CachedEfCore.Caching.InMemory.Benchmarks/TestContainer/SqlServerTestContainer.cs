using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Testcontainers.MsSql;

namespace CachedEfCore.Caching.InMemory.Benchmarks.TestContainer
{
    [DebuggerDisplay("{_databaseName}")]
    public class SqlServerTestContainer
    {
        private static readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

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
            await _container.StartAsync();

            ConnectionString = ConnectionStringForDatabase(_databaseName);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
