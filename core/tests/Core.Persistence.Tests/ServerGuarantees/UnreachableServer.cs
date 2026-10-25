using System.Net;
using System.Net.Sockets;
using Npgsql;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>A data source that points at a loopback port the system handed out and nobody listens on, with a marked password.</summary>
internal sealed class UnreachableServer : IAsyncDisposable
{
    public const string Password = "ZQX-SENHA-7731";

    public UnreachableServer()
    {
        ConnectionString = new NpgsqlConnectionStringBuilder
        {
            Host = IPAddress.Loopback.ToString(),
            Port = FreePort(),
            Database = "zqx_database",
            Username = "zqx_app",
            Password = Password,
        }.ConnectionString;
        DataSource = NpgsqlDataSource.Create(ConnectionString);
    }

    public string ConnectionString { get; }

    public NpgsqlDataSource DataSource { get; }

    public ValueTask DisposeAsync() => DataSource.DisposeAsync();

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
