using System.Net;
using System.Net.Sockets;
using Npgsql;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>A loopback listener whose backlog takes the connection and never answers, behind a data source that waits one second.</summary>
internal sealed class SilentServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);

    public SilentServer()
    {
        listener.Start();
        ConnectionString = new NpgsqlConnectionStringBuilder
        {
            Host = IPAddress.Loopback.ToString(),
            Port = ((IPEndPoint)listener.LocalEndpoint).Port,
            Database = "zqx_database",
            Username = "zqx_app",
            Password = UnreachableServer.Password,
            Timeout = 1,
        }.ConnectionString;
        DataSource = NpgsqlDataSource.Create(ConnectionString);
    }

    public string ConnectionString { get; }

    public NpgsqlDataSource DataSource { get; }

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        listener.Dispose();
    }
}
