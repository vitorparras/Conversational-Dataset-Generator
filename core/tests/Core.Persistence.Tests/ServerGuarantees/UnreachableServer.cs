using System.Net;
using Core.Tests.Support.Net;
using Npgsql;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>A data source that points at a loopback port the test holds without listening on it, with a marked password.</summary>
internal sealed class UnreachableServer : IAsyncDisposable
{
    public const string Password = "ZQX-SENHA-7731";

    private readonly ClosedPort port = new();

    public UnreachableServer()
    {
        ConnectionString = new NpgsqlConnectionStringBuilder
        {
            Host = IPAddress.Loopback.ToString(),
            Port = port.Number,
            Database = "zqx_database",
            Username = "zqx_app",
            Password = Password,
        }.ConnectionString;
        DataSource = NpgsqlDataSource.Create(ConnectionString);
    }

    public string ConnectionString { get; }

    public NpgsqlDataSource DataSource { get; }

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        port.Dispose();
    }
}
