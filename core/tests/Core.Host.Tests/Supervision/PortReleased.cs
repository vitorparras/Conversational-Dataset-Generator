using System.Net;
using System.Net.Sockets;

namespace Core.Host.Tests.Supervision;

/// <summary>Waits for the loopback port of a killed service to be let go: the interpreter that listened on it is a child of the process killed, and leaves only once it sees its input end.</summary>
internal static class PortReleased
{
    /// <summary>Whether the port came to be free within the limit; it returns as soon as it is, so a loaded machine costs time only when it needs it.</summary>
    public static Task<bool> UntilAsync(int port, CancellationToken cancellationToken) =>
        Poll.UntilAsync(() => Task.FromResult(IsFree(port)), Poll.LossLimit, cancellationToken);

    private static bool IsFree(int port)
    {
        try
        {
            using var probe = new TcpListener(IPAddress.Loopback, port);
            probe.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
