using System.Net;
using System.Net.Sockets;

namespace Core.Tests.Support.Net;

/// <summary>A loopback port the test holds without listening on it: a connection to it is refused, and no other listener can be given it.</summary>
public sealed class ClosedPort : IDisposable
{
    private readonly Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    public ClosedPort()
    {
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        Number = ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    public int Number { get; }

    public void Dispose() => socket.Dispose();
}
