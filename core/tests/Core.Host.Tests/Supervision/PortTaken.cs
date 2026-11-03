using System.Net.Sockets;

namespace Core.Host.Tests.Supervision;

/// <summary>
/// Takes over the loopback port of a killed service: the interpreter that listened on it is a child of the process killed, and leaves only once it sees its input end.
/// Testing that the port is free and binding it afterwards leaves a gap in which another process of the machine may take it, so the wait is for the bind itself to succeed.
/// </summary>
internal static class PortTaken
{
    /// <summary>The listener bound to the port as soon as it can be, or null when the limit passed; the caller owns what it gets.</summary>
    public static async Task<T?> UntilAsync<T>(int port, Func<int, T> bind, CancellationToken cancellationToken)
        where T : class
    {
        T? taken = null;
        _ = await Poll.UntilAsync(() => Task.FromResult((taken = TryBind(port, bind)) is not null), Poll.LossLimit, cancellationToken);
        return taken;
    }

    private static T? TryBind<T>(int port, Func<int, T> bind)
        where T : class
    {
        try
        {
            return bind(port);
        }
        catch (SocketException)
        {
            return null;
        }
    }
}
